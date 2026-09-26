import{el}from"./dom.js";
import{api}from"./api-client.js";
export function clearErrors(form){form?.querySelectorAll("[aria-invalid=true]").forEach(x=>{x.removeAttribute("aria-invalid");x.removeAttribute("aria-describedby")});form?.querySelectorAll(".field-error").forEach(x=>x.textContent="")}
export function problemView(problem,status){const box=el("div",{class:"alert alert-error validation-summary",role:"alert",tabindex:"-1"},el("strong",{text:problem?.title||"Request failed."}));if(problem?.detail)box.append(el("div",{text:problem.detail}));for(const messages of Object.values(problem?.errors||{}))for(const message of messages)box.append(el("div",{text:message}));if(problem?.traceId)box.append(el("small",{text:`Trace ID: ${problem.traceId}`}));if(status===403&&!problem?.title)box.textContent="You do not have permission to perform this action.";return box}
export function applyProblem(form,container,result){clearErrors(form);const view=problemView(result.problem,result.status);container.prepend(view);for(const[key,messages]of Object.entries(result.problem?.errors||{})){const input=form?.elements.namedItem(key);if(!input)continue;const error=input.closest(".field")?.querySelector(".field-error");if(error){error.textContent=messages.join(" ");input.setAttribute("aria-invalid","true");input.setAttribute("aria-describedby",error.id)}}view.focus();return view}
// These endpoints do not expose a distinct concurrency ProblemDetails code.
// Compare versions after a rejected write; never adopt this read or retry the write automatically.
export async function applyRecordProblem(form,container,result,path,rowVersion,onReload,selectRecord=data=>data){
  const view=applyProblem(form,container,result);
  if(result.status!==409||!rowVersion)return false;
  const current=await api(path);
  const record=current.ok?selectRecord(current.data):null;
  if(record?.rowVersion===rowVersion)return false;
  const verified=record?.rowVersion;
  view.append(
    el("p",{text:verified?"This record has changed. Reload latest to discard your unsaved edits and review the current record.":"The current version could not be checked. Reload latest before trying again; reloading will discard your unsaved edits."}),
    el("button",{class:"secondary",type:"button",text:"Reload latest",onclick:onReload}));
  return true;
}
