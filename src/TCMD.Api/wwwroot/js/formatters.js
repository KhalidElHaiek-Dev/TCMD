export const date = value => value ? value.split("-").reverse().join("/") : "—";
export const time = value => value ? value.slice(0,5) : "—";
export const timestamp = value => value ? new Intl.DateTimeFormat(undefined,{dateStyle:"medium",timeStyle:"short"}).format(new Date(value)) : "—";
export const statusClass = value => `status status-${String(value).toLowerCase().replaceAll(" ","-")}`;
export const text = value => value == null || value === "" ? "—" : String(value);
