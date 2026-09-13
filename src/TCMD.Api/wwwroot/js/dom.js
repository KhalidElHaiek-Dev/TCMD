export function el(tag, attributes = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attributes)) {
    if (value == null || value === false) continue;
    if (key === "class") node.className = value;
    else if (key === "text") node.textContent = value;
    else if (key.startsWith("on") && typeof value === "function") node.addEventListener(key.slice(2).toLowerCase(), value);
    else if (value === true) node.setAttribute(key, "");
    else node.setAttribute(key, String(value));
  }
  for (const child of children.flat()) if (child != null) node.append(child.nodeType ? child : document.createTextNode(String(child)));
  return node;
}
export const clear = node => { while (node.firstChild) node.firstChild.remove(); };
export const announce = message => { const n=document.querySelector("#announcer"); n.textContent=""; setTimeout(()=>n.textContent=message,20); };
export function field(name, label, options={}) {
  const id=`field-${name}-${Math.random().toString(36).slice(2)}`;
  const input=el(options.tag||"input",{id,name,type:options.type||"text",value:options.value??null,maxlength:options.maxlength,required:options.required,autocomplete:options.autocomplete,placeholder:options.placeholder});
  if(options.tag==="select") for(const item of options.items||[]) input.append(el("option",{value:item.value,selected:String(item.value)===String(options.value),text:item.label}));
  const error=el("div",{class:"field-error",id:`${id}-error`});
  return {wrap:el("div",{class:"field"},el("label",{for:id,text:label}),input,options.help?el("small",{text:options.help}):null,error),input,error};
}
