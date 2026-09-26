import{api}from"../api-client.js";import{session,isOperational}from"../auth.js";import{el,field}from"../dom.js";import{loading,empty,badge,table,confirmAction}from"../components.js";import{applyRecordProblem,clearErrors,problemView}from"../problem-details.js";import{date,time,text}from"../formatters.js";
let groupPageCache=null;
const groupListState={search:"",status:""};
export function clearGroupPageCache(id){if(!id||groupPageCache?.id!==id)groupPageCache=null}
export async function groupList(){const root=el("div",{},el("h1",{text:isOperational()?"Training Groups":"My Groups"})),results=el("div"),search=field("search","Search",{value:groupListState.search}),status=field("status","Status",{tag:"select",value:groupListState.status,items:[{value:"",label:"All"},...['Planned','Active','Completed','Cancelled'].map(x=>({value:x,label:x}))]});let controller;const load=async()=>{controller?.abort();controller=new AbortController();groupListState.search=search.input.value.trim();groupListState.status=status.input.value;results.replaceChildren(loading());const p=new URLSearchParams();if(groupListState.search)p.set("search",groupListState.search);if(groupListState.status)p.set("status",groupListState.status);const r=await api(`/api/training-groups?${p}`,{signal:controller.signal});if(r.aborted)return;if(!r.ok){results.replaceChildren(problemView(r.problem,r.status));return}results.replaceChildren(r.data.length?table("Training Groups",[{label:"Name",render:x=>x.name},{label:"Dates",render:x=>`${date(x.plannedStartDate)} – ${date(x.plannedEndDate)}`},{label:"Status",render:x=>badge(x.status)},{label:"",render:x=>el("a",{href:`#/groups/${x.id}`,text:"View"})}],r.data):empty(isOperational()?"No Training Groups found.":"No Training Groups are currently assigned to you."))};const form=el("form",{class:"toolbar",onsubmit:e=>{e.preventDefault();load()}},search.wrap,status.wrap,el("button",{text:"Search"}));if(isOperational())form.append(el("a",{class:"button",href:"#/groups/new",text:"Add Training Group"}));root.append(form,results);await load();return root}
export async function groupDetail(id,query){const creating=id==="new",operational=isOperational(),root=el("div",{},el("h1",{text:creating?"Add Training Group":"Training Group"})),message=el("div");let model=null;if(!creating){if(groupPageCache?.id===id)model=groupPageCache.model;else{root.append(loading());const r=await api(`/api/training-groups/${id}`);root.lastChild.remove();if(!r.ok){root.append(problemView(r.problem,r.status));return root}model=r.data;groupPageCache={id,model,draft:null,references:null}}}if(creating&&!operational)return problemView({title:"You do not have permission to perform this action."},403);
 if(operational){let references=groupPageCache?.id===id?groupPageCache.references:null;if(!references){const [courses,instructors]=await Promise.all([api('/api/courses?isActive=true'),api('/api/instructors?isActive=true')]);if(!courses.ok||!instructors.ok){root.append(problemView((!courses.ok?courses:instructors).problem,500));return root}references={courses,instructors};if(!creating)groupPageCache.references=references}const {courses,instructors}=references;
 // Preserve saved relationships without offering other inactive records for assignment.
 for(const [records,path,currentId] of [[courses.data,"courses",model?.courseId],[instructors.data,"instructors",model?.primaryInstructorId]]){
  if(currentId&&!records.some(x=>x.id===currentId)){
   const current=await api(`/api/${path}/${currentId}`);
   if(!current.ok){root.append(problemView(current.problem,current.status));return root}
   records.push(current.data);
  }
 }
 const draft=groupPageCache?.id===id?groupPageCache.draft:null;const name=field("name","Group name",{required:true,maxlength:200,value:draft?.name??model?.name}),course=field("courseId","Course",{tag:"select",required:true,value:draft?.courseId??model?.courseId,items:[{value:"",label:"Select a course"},...courses.data.map(x=>({value:x.id,label:`${x.code} — ${x.name}${x.isActive?"":" (inactive, current)"}`,disabled:!x.isActive}))]}),instructor=field("primaryInstructorId","Primary Instructor",{tag:"select",value:draft?.primaryInstructorId??model?.primaryInstructorId??"",items:[{value:"",label:"Not assigned"},...instructors.data.map(x=>({value:x.id,label:`${x.fullName}${x.isActive?"":" (inactive, current)"}`,disabled:!x.isActive}))]}),start=field("plannedStartDate","Planned start",{type:"date",required:true,value:draft?.plannedStartDate??model?.plannedStartDate}),end=field("plannedEndDate","Planned end",{type:"date",required:true,value:draft?.plannedEndDate??model?.plannedEndDate});let stale=false,busy=false;
 const rememberDraft=()=>{if(groupPageCache?.id===id)groupPageCache.draft={name:name.input.value,courseId:course.input.value,primaryInstructorId:instructor.input.value,plannedStartDate:start.input.value,plannedEndDate:end.input.value}};
 for(const control of [name.input,course.input,instructor.input,start.input,end.input])control.addEventListener("input",rememberDraft);
 const details=el("div"),actions=el("div"),save=el("button",{text:creating?"Create Group":"Save changes"});
 const form=el("form",{class:"panel",onsubmit:async e=>{
  e.preventDefault();if(stale||busy||!form.reportValidity())return;
  const body={name:name.input.value,courseId:course.input.value,primaryInstructorId:instructor.input.value||null,plannedStartDate:start.input.value,plannedEndDate:end.input.value};
  if(model)body.rowVersion=model.rowVersion;
  await mutate(creating?'/api/training-groups':`/api/training-groups/${id}`,creating?'POST':'PUT',body);
 }},name.wrap,course.wrap,instructor.wrap,start.wrap,end.wrap,el("div",{class:"actions"},save,el("a",{class:"button secondary",href:"#/groups",text:"Back"})));
 function syncControls(){
  const locked=busy||stale||['Completed','Cancelled'].includes(model?.status);
  form.querySelectorAll('input,select,button').forEach(control=>control.disabled=locked);
  actions.querySelectorAll('button').forEach(button=>button.disabled=busy||stale);
 }
 function accept(next){
  model=next;stale=false;clearErrors(form);
  if(groupPageCache?.id===id){groupPageCache.model=next;groupPageCache.draft=null}
  name.input.value=model.name;course.input.value=model.courseId;instructor.input.value=model.primaryInstructorId||"";
  start.input.value=model.plannedStartDate;end.input.value=model.plannedEndDate;
  // A replaced inactive relationship is no longer a current option.
  for(const control of [course.input,instructor.input])for(const option of control.querySelectorAll('option:disabled'))if(option.value!==control.value)option.remove();
  courses.data=courses.data.filter(x=>x.isActive||x.id===model.courseId);instructors.data=instructors.data.filter(x=>x.isActive||x.id===model.primaryInstructorId);
  details.replaceChildren(summary(model));actions.replaceChildren(statusActions(model,changeStatus));
  message.replaceChildren(el("div",{class:"alert alert-success",role:"status",text:"Changes saved."}));
 }
 async function mutate(path,method,body){
  if(busy||stale)return;busy=true;syncControls();message.replaceChildren();
  try{
   const r=await api(path,{method,body});
   if(r.ok){if(creating){model=r.data;groupPageCache={id:model.id,model,draft:null,references};location.hash=`#/groups/${model.id}`}else accept(r.data)}
   else stale=await applyRecordProblem(form,message,r,`/api/training-groups/${id}`,model?.rowVersion,()=>location.reload());
  }finally{busy=false;syncControls()}
 }
 async function changeStatus(action){
  if(busy||stale)return;
  if(!await confirmAction({title:`${action} Training Group`,message:`Confirm ${action} for this Training Group?`,confirmText:action,danger:action==='cancel'}))return;
  await mutate(`/api/training-groups/${model.id}/${action}`,'POST',{rowVersion:model.rowVersion});
 }
 root.append(message,details,form,actions);if(model){details.append(summary(model));actions.append(statusActions(model,changeStatus))}syncControls();
 }else root.append(summary(model));
 if(model)root.append(tabs(id,query.get("tab")||"overview"),await tabContent(model,query.get("tab")||"overview",operational));return root;
}
function summary(m){return el("section",{class:"details panel"},detail("Name",m.name),detail("Status",badge(m.status)),detail("Dates",`${date(m.plannedStartDate)} – ${date(m.plannedEndDate)}`),detail("Course ID",m.courseId),detail("Primary Instructor ID",text(m.primaryInstructorId)))}
function detail(label,value){return el("dl",{class:"detail"},el("dt",{text:label}),el("dd",{},value?.nodeType?value:text(value)))}
function tabs(id,current){return el("nav",{class:"tabs","aria-label":"Training Group sections"},['overview','enrollments','sessions','attendance'].map(x=>el("a",{href:`#/groups/${id}?tab=${x}`,"aria-current":current===x?"page":null,text:x[0].toUpperCase()+x.slice(1)})))}
function statusActions(model,onAction){
 const wrap=el("div",{class:"actions panel"});
 const actions=model.status==='Planned'?['activate','cancel']:model.status==='Active'?['complete','cancel']:[];
 for(const action of actions)wrap.append(el("button",{type:"button",class:action==='cancel'?'danger':'secondary',text:action[0].toUpperCase()+action.slice(1),onclick:()=>onAction(action)}));
 return wrap;
}
async function tabContent(model,tab,operational){if(tab==='overview')return el("p",{class:"muted",text:"Select a section to manage membership, schedule, or attendance."});if(tab==='enrollments')return enrollments(model,operational);if(tab==='sessions')return sessions(model,operational);return attendance(model)}
async function enrollments(group,operational){const section=el("section"),render=async message=>{const next=await buildEnrollments(group,operational,section,render,message);section.replaceChildren(next)};await render();return section}
async function buildEnrollments(group,operational,section,render,message){const content=el("div",{},el("h2",{text:"Enrollments"}));if(message)content.append(el("div",{class:"alert alert-success",role:"status",text:message}));const r=await api(`/api/training-groups/${group.id}/enrollments`);if(!r.ok){content.append(problemView(r.problem,r.status));return content}if(operational){const students=await api('/api/students?isActive=true');if(!students.ok)content.append(problemView(students.problem,students.status));else{const pick=field("studentId","Active Student",{tag:"select",items:[{value:"",label:"Select a student"},...students.data.map(x=>({value:x.id,label:`${x.studentNumber} — ${x.fullName}`}))]}),submit=el("button",{text:"Enroll Student"});const form=el("form",{class:"toolbar",onsubmit:async e=>{e.preventDefault();if(submit.disabled||!pick.input.value)return;submit.disabled=true;const x=await api(`/api/training-groups/${group.id}/enrollments`,{method:'POST',body:{studentId:pick.input.value}});if(x.ok)await render("Student enrolled.");else{content.prepend(problemView(x.problem,x.status));submit.disabled=false}}},pick.wrap,submit);content.append(form)}}if(!r.data.length)content.append(empty("No enrollment history."));else content.append(table("Group enrollment roster",[{label:"Student",render:x=>`${x.student.studentNumber} — ${x.student.fullName}`},{label:"Student state",render:x=>badge(x.student.isActive?'Active':'Inactive')},{label:"Enrollment",render:x=>badge(x.status)},{label:"Actions",render:x=>operational?enrollmentActions(x,content,render):"Read only"}],r.data));return content}
function enrollmentActions(item,container,render){const wrap=el("div",{class:"actions"});const actions=item.status==='Active'?['complete','withdraw']:item.status==='Withdrawn'?['reactivate']:[];for(const action of actions){const button=el("button",{class:"secondary",text:action,onClick:async()=>{if(button.disabled||!await confirmAction({title:`${action} Enrollment`,message:`Confirm ${action} for ${item.student.fullName}?`,confirmText:action}))return;button.disabled=true;const r=await api(`/api/enrollments/${item.id}/${action}`,{method:'POST',body:{rowVersion:item.rowVersion}});if(r.ok)await render(action==='complete'?"Enrollment completed.":action==='withdraw'?"Enrollment withdrawn.":"Enrollment reactivated.");else{container.prepend(problemView(r.problem,r.status));button.disabled=false}}});wrap.append(button)}return wrap}
async function sessions(group,operational){const section=el("section",{},el("h2",{text:"Training Sessions"}));if(operational)section.append(el("a",{class:"button",href:`#/groups/${group.id}/sessions/new`,text:"Schedule Session"}));const r=await api(`/api/training-groups/${group.id}/sessions`);if(!r.ok)section.append(problemView(r.problem,r.status));else section.append(r.data.length?table("Sessions — Africa/Casablanca time",[{label:"Date",render:x=>date(x.sessionDate)},{label:"Time",render:x=>`${time(x.startTime)}–${time(x.endTime)}`},{label:"Location",render:x=>text(x.location)},{label:"Status",render:x=>badge(x.status)},{label:"",render:x=>el("a",{href:`#/sessions/${x.id}`,text:"View"})}],r.data):empty("No Sessions scheduled."));return section}
async function attendance(group){const section=el("section",{},el("h2",{text:"Attendance history"})),r=await api(`/api/training-groups/${group.id}/attendance`);if(!r.ok)section.append(problemView(r.problem,r.status));else section.append(r.data.length?table("Recorded Attendance",[{label:"Student",render:x=>`${x.student.studentNumber} — ${x.student.fullName}`},{label:"Session",render:x=>`${date(x.sessionDate)} ${time(x.startTime)}`},{label:"Attendance",render:x=>badge(x.attendance.status)},{label:"Note",render:x=>text(x.attendance.correctionNote)}],r.data):empty("No Attendance has been recorded."));return section}
