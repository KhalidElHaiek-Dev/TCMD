import{api}from"../api-client.js";import{el,field}from"../dom.js";import{loading,empty,badge,table,confirmAction}from"../components.js";import{applyRecordProblem,clearErrors,problemView}from"../problem-details.js";import{text}from"../formatters.js";
const configs={
 students:{title:"Students",single:"Student",path:"students",search:"name, student number, phone, or email",fields:[f("fullName","Full name",200,true),f("phoneNumber","Phone number",50,true,"tel"),f("email","Email",320,false,"email")],columns:[c("Student number","studentNumber"),c("Name","fullName"),c("Phone","phoneNumber"),c("Email","email")]},
 instructors:{title:"Instructors",single:"Instructor",path:"instructors",search:"name, phone, or email",fields:[f("fullName","Full name",200,true),f("phoneNumber","Phone number",50,false,"tel"),f("email","Email",320,false,"email")],columns:[c("Name","fullName"),c("Phone","phoneNumber"),c("Email","email")]},
 courses:{title:"Courses",single:"Course",path:"courses",search:"code, name, or description",fields:[f("code","Code",50,true),f("name","Name",200,true),f("description","Description",2000,false,"text","textarea")],columns:[c("Code","code"),c("Name","name"),c("Description","description")]}
};function f(name,label,maxlength,required,type="text",tag="input"){return{name,label,maxlength,required,type,tag}}function c(label,key){return{label,key}}
const listStates=new Map();
export async function entityList(kind){const cfg=configs[kind],saved=listStates.get(kind)||{search:"",isActive:""},root=el("div",{},el("h1",{text:cfg.title})),message=el("div"),results=el("div");let controller;const search=field("search","Search",{placeholder:cfg.search,value:saved.search}),state=field("isActive","Status",{tag:"select",value:saved.isActive,items:[{value:"",label:"All"},{value:"true",label:"Active"},{value:"false",label:"Inactive"}]});const load=async()=>{controller?.abort();controller=new AbortController();saved.search=search.input.value.trim();saved.isActive=state.input.value;listStates.set(kind,saved);results.replaceChildren(loading());const params=new URLSearchParams();if(saved.search)params.set("search",saved.search);if(saved.isActive)params.set("isActive",saved.isActive);const response=await api(`/api/${cfg.path}?${params}`,{signal:controller.signal});if(response.aborted)return;if(!response.ok){results.replaceChildren(problemView(response.problem,response.status));return}const rows=response.data;results.replaceChildren(rows.length?table(cfg.title,[...cfg.columns.map(col=>({label:col.label,render:x=>text(x[col.key])})),{label:"Status",render:x=>badge(x.isActive?"Active":"Inactive")},{label:"",render:x=>el("a",{href:`#/${kind}/${x.id}`,text:"View"})}],rows):empty(`No ${cfg.title.toLowerCase()} found.`))};const form=el("form",{class:"toolbar",onsubmit:e=>{e.preventDefault();load()}},search.wrap,state.wrap,el("button",{text:"Search"}),el("a",{class:"button",href:`#/${kind}/new`,text:`Add ${cfg.single}`}));root.append(message,form,results);await load();return root}
export async function entityDetail(kind,id){const cfg=configs[kind],creating=id==="new",root=el("div",{},el("h1",{text:creating?`Add ${cfg.single}`:`${cfg.single} details`})),message=el("div");let model=null,stale=false,busy=false;if(!creating){root.append(loading());const response=await api(`/api/${cfg.path}/${id}`);root.lastChild.remove();if(!response.ok){root.append(problemView(response.problem,response.status));return root}model=response.data}
 const controls={},form=el("form",{class:"panel",novalidate:true});
 for(const spec of cfg.fields){const item=field(spec.name,spec.label,{value:model?.[spec.name]||"",maxlength:spec.maxlength,required:spec.required,type:spec.type,tag:spec.tag});controls[spec.name]=item.input;form.append(item.wrap)}
 const actions=el("div",{class:"actions"}),save=el("button",{type:"submit",text:creating?`Create ${cfg.single}`:"Save changes"});
 actions.append(save,el("a",{class:"button secondary",href:`#/${kind}`,text:"Back to list"}));
 function syncControls(){form.querySelectorAll('input,textarea,button').forEach(control=>control.disabled=busy||stale)}
 async function handle(result){stale=await applyRecordProblem(form,message,result,`/api/${cfg.path}/${id}`,model?.rowVersion,()=>location.reload())}
 if(model?.isActive)actions.append(el("button",{class:"danger",type:"button",text:"Deactivate",onclick:async()=>{
  if(busy||stale)return;
  if(!await confirmAction({title:`Deactivate ${cfg.single}`,message:`Deactivate this ${cfg.single.toLowerCase()}? Historical information will be preserved.`,confirmText:"Deactivate",danger:true}))return;
  if(busy||stale)return;busy=true;syncControls();message.replaceChildren();
  try{
   const result=await api(`/api/${cfg.path}/${id}/deactivate`,{method:"POST",body:{rowVersion:model.rowVersion}});
   if(result.ok){model=result.data;form.querySelectorAll('input,textarea').forEach(control=>control.disabled=true);actions.replaceChildren(el("a",{class:"button secondary",href:`#/${kind}`,text:"Back to list"}));root.querySelector(".details dd").replaceChildren(badge("Inactive"));message.append(el("div",{class:"alert alert-success",role:"status",text:`${cfg.single} deactivated.`}))}else await handle(result);
  }finally{busy=false;syncControls()}
 }}));
 form.append(actions);form.addEventListener("submit",async e=>{
  e.preventDefault();if(stale||busy||!form.reportValidity())return;message.replaceChildren();busy=true;syncControls();
  const body=Object.fromEntries(cfg.fields.map(x=>[x.name,controls[x.name].value]));if(!creating)body.rowVersion=model.rowVersion;
  try{
   const result=await api(creating?`/api/${cfg.path}`:`/api/${cfg.path}/${id}`,{method:creating?"POST":"PUT",body});
   if(result.ok){
    model=result.data;clearErrors(form);
    if(creating)location.hash=`#/${kind}/${model.id}`;
    else{for(const spec of cfg.fields)controls[spec.name].value=model[spec.name]??"";message.append(el("div",{class:"alert alert-success",role:"status",text:"Changes saved."}));stale=false}
   }else await handle(result);
  }finally{busy=false;syncControls()}
 });
 root.append(message);if(model)root.append(el("section",{class:"details panel"},detail("Status",badge(model.isActive?"Active":"Inactive")),model.studentNumber?detail("Student number",model.studentNumber):null));root.append(form);if(!creating&&kind==="students"){const [enrollments,attendance]=await Promise.all([relatedSection("Enrollment history",`/api/students/${id}/enrollments`,"No enrollment history.",x=>`${x.trainingGroup.name} — ${x.status}`,x=>`/groups/${x.trainingGroupId}`),relatedSection("Attendance history",`/api/students/${id}/attendance`,"No attendance history.",x=>`${x.session.trainingGroupName} · ${x.session.sessionDate} · ${x.attendance.status}`,x=>`/sessions/${x.session.id}`)]);root.append(enrollments,attendance)}if(!creating&&kind==="instructors")root.append(await relatedSection("Assigned groups",`/api/training-groups?primaryInstructorId=${id}`,"No assigned groups.",x=>`${x.name} — ${x.status}`,x=>`/groups/${x.id}`));if(!creating&&kind==="courses")root.append(await relatedSection("Associated groups",`/api/training-groups?courseId=${id}`,"No associated groups.",x=>`${x.name} — ${x.status}`,x=>`/groups/${x.id}`));return root}
async function relatedSection(title,url,none,label,link){const section=el("section",{},el("h2",{text:title}));const result=await api(url);if(!result.ok)section.append(problemView(result.problem,result.status));else if(!result.data.length)section.append(empty(none));else section.append(el("ul",{},result.data.map(x=>el("li",{},el("a",{href:`#${link(x)}`,text:label(x)})))));return section}
function detail(label,value){return el("dl",{class:"detail"},el("dt",{text:label}),el("dd",{},value?.nodeType?value:text(value)))}
