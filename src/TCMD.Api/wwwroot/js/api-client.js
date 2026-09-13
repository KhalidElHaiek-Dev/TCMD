let csrfToken=null;
let unauthorizedHandler=()=>{};
export function onUnauthorized(handler){unauthorizedHandler=handler}
export async function refreshAntiforgery(){const response=await fetch("/api/auth/antiforgery",{headers:{Accept:"application/json"}});if(!response.ok)throw new Error("Unable to initialize request security.");csrfToken=(await response.json()).requestToken}
export async function api(path,{method="GET",body,signal,login=false}={}){
  const upper=method.toUpperCase(),headers={Accept:"application/json"};
  if(body!==undefined)headers["Content-Type"]="application/json";
  if(!["GET","HEAD","OPTIONS"].includes(upper))headers["X-CSRF-TOKEN"]=csrfToken||"";
  try{
    const response=await fetch(path,{method:upper,headers,body:body===undefined?undefined:JSON.stringify(body),signal});
    const type=response.headers.get("content-type")||"";let data=null;
    if(response.status!==204&&type.includes("json")){try{data=await response.json()}catch{return {ok:false,status:response.status,problem:{title:"The server returned an unreadable response."}}}}
    if(response.status===401&&!login)unauthorizedHandler();
    return response.ok?{ok:true,status:response.status,data}:{ok:false,status:response.status,problem:normalizeProblem(data,response.status)};
  }catch(error){if(error.name==="AbortError")return {ok:false,aborted:true,status:0};return {ok:false,status:0,problem:{title:"TCMD could not reach the server.",detail:"Check the connection and try again."}}}
}
function normalizeProblem(value,status){if(value&&typeof value==="object")return {title:value.title||`Request failed (${status})`,detail:value.detail||null,errors:value.errors||{},traceId:value.traceId||null};return {title:`Request failed (${status})`,detail:null,errors:{},traceId:null}}
