import{api}from"../api-client.js";import{isOperational}from"../auth.js";import{el,field}from"../dom.js";import{badge,confirmAction,empty,table}from"../components.js";import{applyRecordProblem,clearErrors,problemView}from"../problem-details.js";import{date,time,text}from"../formatters.js";
const statuses=['Present','Absent','Late','Excused'];
export async function newSession(groupId){if(!isOperational())return problemView({title:"You do not have permission to perform this action."},403);return sessionForm(null,groupId)}
export async function sessionDetail(id){
 const r=await api(`/api/training-sessions/${id}`);
 if(!r.ok)return problemView(r.problem,r.status);
 let model=r.data;
 const summary=el("div"),attendance=el("div"),root=el("div",{},el("h1",{text:"Training Session"}),summary);
 async function showSaved(next){
  model=next;
  summary.replaceChildren(sessionSummary(model));
  attendance.replaceChildren(await roster(model));
 }
 summary.append(sessionSummary(model));
 if(isOperational())root.append(await sessionForm(model,model.trainingGroupId,true,showSaved));
 attendance.append(await roster(model));root.append(attendance);
 return root;
}
function sessionSummary(model){
 return el("section",{class:"details panel"},detail("Date",date(model.sessionDate)),detail("Time",`${time(model.startTime)}–${time(model.endTime)} (Africa/Casablanca time)`),detail("Location",text(model.location)),detail("Status",badge(model.status)),detail("Training Group",el("a",{href:`#/groups/${model.trainingGroupId}?tab=sessions`,text:"Open Group"})));
}
async function sessionForm(model,groupId,embedded=false,onSaved=async()=>{}){
 const creating=!model,root=el("section",{},el(embedded?"h2":"h1",{text:model?"Edit Session":"Schedule Session"})),messages=el("div"),actions=el("div");
 const d=field("sessionDate","Session date",{type:"date",required:true,value:model?.sessionDate}),s=field("startTime","Start time",{type:"time",required:true,value:model?.startTime?.slice(0,5)}),e=field("endTime","End time",{type:"time",required:true,value:model?.endTime?.slice(0,5)}),loc=field("location","Location",{maxlength:500,value:model?.location});
 let stale=false,busy=false;
 const save=el("button",{text:model?"Save Session":"Schedule Session"});
 const form=el("form",{class:"panel",onsubmit:async event=>{
  event.preventDefault();if(stale||busy||!form.reportValidity())return;
  if(e.input.value<=s.input.value){e.input.setCustomValidity("End time must be later than start time.");e.input.reportValidity();e.input.setCustomValidity("");return}
  const body={sessionDate:d.input.value,startTime:s.input.value,endTime:e.input.value,location:loc.input.value};
  if(model)body.rowVersion=model.rowVersion;
  await mutate(model?`/api/training-sessions/${model.id}`:`/api/training-groups/${groupId}/sessions`,model?'PUT':'POST',body);
 }},d.wrap,s.wrap,e.wrap,loc.wrap,el("p",{class:"muted",text:"Schedule values are in Africa/Casablanca time."}),el("div",{class:"actions"},save,!embedded?el("a",{class:"button secondary",href:`#/groups/${groupId}?tab=sessions`,text:"Cancel"}):null));
 function syncControls(){
  const locked=busy||stale||(model&&model.status!=='Scheduled');
  form.querySelectorAll('input,button').forEach(control=>control.disabled=!!locked);
  actions.querySelectorAll('button').forEach(button=>button.disabled=busy||stale);
 }
 async function accept(next,message){
  model=next;stale=false;clearErrors(form);
  d.input.value=model.sessionDate;s.input.value=model.startTime.slice(0,5);e.input.value=model.endTime.slice(0,5);loc.input.value=model.location||"";
  actions.replaceChildren(statusButtons(model,changeStatus));
  syncControls();await onSaved(model);
  messages.replaceChildren(el("div",{class:"alert alert-success",role:"status",text:message}));
 }
 async function reloadLatest(){
  if(busy)return;busy=true;syncControls();
  try{
   const r=await api(`/api/training-sessions/${model.id}`);
   if(r.ok)await accept(r.data,"Latest record loaded. Review it before saving.");
   else messages.replaceChildren(problemView(r.problem,r.status),el("button",{type:"button",class:"secondary",text:"Reload latest",onclick:reloadLatest}));
  }finally{busy=false;syncControls()}
 }
 async function mutate(path,method,body){
  if(busy||stale)return;busy=true;syncControls();messages.replaceChildren();
  try{
   const r=await api(path,{method,body});
   if(r.ok){if(creating)location.hash=`#/sessions/${r.data.id}`;else await accept(r.data,"Changes saved.")}
   else stale=await applyRecordProblem(form,messages,r,`/api/training-sessions/${model?.id}`,model?.rowVersion,reloadLatest);
  }finally{busy=false;syncControls()}
 }
 async function changeStatus(action){
  if(busy||stale)return;
  if(!await confirmAction({title:`${action} Session`,message:`Confirm ${action} for this Session?`,confirmText:action,danger:action==='cancel'}))return;
  await mutate(`/api/training-sessions/${model.id}/${action}`,'POST',{rowVersion:model.rowVersion});
 }
 root.append(messages,form,actions);if(model)actions.append(statusButtons(model,changeStatus));syncControls();return root;
}
function statusButtons(model,onAction){
 const wrap=el("div",{class:"actions panel"});
 if(model.status==='Scheduled')for(const action of ['complete','cancel'])wrap.append(el("button",{type:"button",class:action==='cancel'?'danger':'secondary',text:action[0].toUpperCase()+action.slice(1),onclick:()=>onAction(action)}));
 return wrap;
}
async function roster(model){const section=el("section",{},el("h2",{text:"Attendance roster"})),r=await api(`/api/training-sessions/${model.id}/attendance`);if(!r.ok){section.append(problemView(r.problem,r.status));return section}if(!r.data.length){section.append(empty("No applicable Enrollment roster."));return section}section.append(table("Session Attendance roster",[{label:"Student",render:x=>`${x.student.studentNumber} — ${x.student.fullName}`},{label:"Enrollment",render:x=>badge(x.enrollmentStatus)},{label:"Attendance",render:x=>{const cell=el("span");cell.replaceChildren(x.attendance?badge(x.attendance.status):badge("Not Recorded"));x.attendanceStatusCell=cell;return cell}},{label:"Correction note",render:x=>{const cell=el("span",{text:text(x.attendance?.correctionNote)});x.attendanceNoteCell=cell;return cell}},{label:"Action",render:x=>{const cell=el("div");x.attendanceActionCell=cell;cell.append(attendanceControl(model,x,section));return cell}}],r.data));return section}
function attendanceControl(model,row,section){
 if(!row.attendance&&model.status==='Cancelled')return "New entry unavailable";
 const studentName=row.student.fullName;
 const status=field(`status-${row.enrollmentId}`,"Status",{tag:"select",ariaLabel:`Attendance status for ${studentName}`,value:row.attendance?.status||"",items:[{value:"",label:"Select"},...statuses.map(x=>({value:x,label:x}))]});
 const note=row.attendance?field(`note-${row.enrollmentId}`,"Correction note",{tag:"textarea",ariaLabel:`Attendance correction note for ${studentName}`,maxlength:1000,value:row.attendance.correctionNote}):null;
 const feedback=el("div"),button=el("button",{class:"secondary","aria-label":`${row.attendance?"Correct":"Record"} attendance for ${studentName}`,text:row.attendance?"Correct":"Record",onclick:async()=>{
  if(button.disabled||!status.input.value)return;
  button.disabled=true;feedback.replaceChildren();
  const existing=row.attendance;
  const r=existing
   ?await api(`/api/attendance/${existing.id}`,{method:'PUT',body:{status:status.input.value,correctionNote:note.input.value,rowVersion:existing.rowVersion}})
   :await api(`/api/training-sessions/${model.id}/attendance`,{method:'POST',body:{enrollmentId:row.enrollmentId,status:status.input.value}});
  if(r.ok){row.attendance=r.data;row.attendanceStatusCell.replaceChildren(badge(r.data.status));row.attendanceNoteCell.textContent=text(r.data.correctionNote);row.attendanceActionCell.replaceChildren(attendanceControl(model,row,section));row.attendanceActionCell.append(el("div",{class:"alert alert-success",role:"status",text:"Attendance saved."}));return}
  else{const stale=await applyRecordProblem(null,feedback,r,`/api/training-sessions/${model.id}/attendance`,existing?.rowVersion,()=>location.reload(),rows=>rows.find(x=>x.attendance?.id===existing?.id)?.attendance);button.disabled=stale;return}
 }});
 return el("div",{class:"actions"},status.wrap,note?.wrap,button,feedback)
}
function detail(label,value){return el("dl",{class:"detail"},el("dt",{text:label}),el("dd",{},value?.nodeType?value:value))}
