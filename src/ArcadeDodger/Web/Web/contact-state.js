export const contactNumbers = new Map();
let sector;
export function resetContacts(id) {
  if (sector === id) return;
  sector = id;
  contactNumbers.clear();
}
export function contactNumber(key) {
  if (!contactNumbers.has(key)) contactNumbers.set(key, contactNumbers.size + 1);
  return contactNumbers.get(key);
}
