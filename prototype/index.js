// Planning-only local data. Times are naive wall-clock values, without production time-zone handling.
const $ = id => document.getElementById(id);
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const dayMs = 86400000;
const clockStarted = Date.now();
const sampleNow = Date.UTC(2026, 9, 26, 11, 35);
const now = () => sampleNow + Date.now() - clockStarted;
const parse = value => Date.parse(value + 'Z');
const inputValue = value => new Date(value).toISOString().slice(0, 19);
const dayStart = value => Math.floor(value / dayMs) * dayMs;
const time = value => new Date(value).toISOString().slice(11, 16);
const dateLabel = value => new Intl.DateTimeFormat('cs', {day:'numeric',month:'numeric',timeZone:'UTC'}).format(value);
const weekDay = value => new Intl.DateTimeFormat('cs', {weekday:'short',timeZone:'UTC'}).format(value);
const duration = value => { const minutes = Math.max(0, Math.floor(value / 60000)); return `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} min`; };
const tags = value => [...new Map(value.split(',').map(x => x.trim()).filter(Boolean).map(x => [x.toLocaleLowerCase('cs'), x])).values()];
let entries = [
  {id:1,description:'Návrh aplikace',project:'WhatHaveIDone',tags:['plánování'],start:parse('2026-10-26T09:00:00'),end:parse('2026-10-26T10:30:00')},
  {id:2,description:'Konzultace',project:'RSM',tags:['schůzka'],start:parse('2026-10-26T10:00:00'),end:parse('2026-10-26T11:00:00')},
  {id:3,description:'',project:'',tags:[],start:parse('2026-10-26T13:00:00'),end:parse('2026-10-26T13:45:00')},
  {id:4,description:'Úprava přihlášení',project:'RSM',tags:['vývoj'],start:parse('2026-10-27T09:00:00'),end:parse('2026-10-27T11:00:00')}
];
let nextId = 5, selectedDay = dayStart(sampleNow), view = 'list', editingId = null, savedForm = '', toastTimer, autosaveTimer;
const narrow = matchMedia('(max-width:899px)');
const running = () => entries.find(e => e.end === null);
const end = e => e.end ?? now();
const overlaps = e => entries.some(other => other.id !== e.id && other.start < end(e) && end(other) > e.start);
const sum = (items, start, finish) => items.reduce((total, e) => total + Math.max(0, Math.min(end(e), finish) - Math.max(e.start, start)), 0);
function range() {
  if (narrow.matches) return [selectedDay, selectedDay + dayMs];
  const weekday = new Date(selectedDay).getUTCDay();
  const monday = selectedDay - ((weekday + 6) % 7) * dayMs;
  return [monday, monday + 7 * dayMs];
}
function notify(message) {
  clearTimeout(toastTimer); $('toast').textContent = message; $('toast').hidden = false;
  toastTimer = setTimeout(() => $('toast').hidden = true, 4000);
}
function render() {
  const [start, finish] = range();
  const items = entries.filter(e => e.start < finish && end(e) > start).sort((a,b) => a.start - b.start);
  $('calendar-view').hidden = view !== 'calendar'; $('list-view').hidden = view !== 'list';
  $('calendar-tab').setAttribute('aria-pressed', view === 'calendar'); $('list-tab').setAttribute('aria-pressed', view === 'list');
  $('period-title').textContent = narrow.matches ? `${weekDay(start)} ${dateLabel(start)} ${new Date(start).getUTCFullYear()}` : `${dateLabel(start)} – ${dateLabel(finish - dayMs)} ${new Date(finish - dayMs).getUTCFullYear()}`;
  $('period-total').innerHTML = `Celkem <strong>${duration(sum(items,start,finish))}</strong>`;
  const days = Array.from({length:7}, (_,i) => start + i * dayMs);
  $('day-heads').innerHTML = '<div class="day-head empty-head"></div>' + days.map((d,i) => `<div class="day-head ${i===0?'selected':''} ${d===dayStart(sampleNow)?'today':''}">${weekDay(d)}<strong>${dateLabel(d)}</strong><small>${duration(sum(items,d,d+dayMs))}</small></div>`).join('');
  $('calendar').innerHTML = '<div class="time-labels">' + Array.from({length:24},(_,h) => `<span style="top:${h*48}px">${String(h).padStart(2,'0')}:00</span>`).join('') + '</div>' + days.map((d,i) => {
    const blocks = items.filter(e => e.start < d+dayMs && end(e)>d).map(e => {
      const top = (Math.max(e.start,d)-d)/3600000*48;
      const height = Math.max(24,(Math.min(end(e),d+dayMs)-Math.max(e.start,d))/3600000*48);
      const conflicting = overlaps(e);
      const label = `${e.description||'Bez popisu'}, ${e.project||'Bez projektu'}, ${time(e.start)}–${e.end===null?'běží':time(e.end)}${conflicting?', překryv':''}`;
      // Put overlapping blocks in two lanes; production calendar owns general interval layout.
      const lane = conflicting ? `left:${e.id%2 ? 3 : 49}%;right:${e.id%2 ? 51 : 3}%;` : '';
      return `<button class="event ${e.project==='RSM'?'rsm':''} ${conflicting?'overlapping':''}" data-edit="${e.id}" style="top:${top}px;height:${height}px;${lane}" aria-label="${esc(label)}" title="${esc(label)}"><b>${esc(e.description||'Bez popisu')}</b>${height>=40?`<span class="event-time">${time(e.start)}${e.end===null?' · Běží':''}</span>`:''}${height>85?`<span>${esc(e.project||'Bez projektu')}</span>`:''}</button>`;
    }).join('');
    return `<div class="day-column ${i===0?'selected':''}" data-day="${d}"><button class="slot-target" data-create="${d}" aria-label="Přidat záznam ${dateLabel(d)}"></button>${blocks}</div>`;
  }).join('');
  if (!items.length) $('list-view').innerHTML = '<div class="empty"><h2>Žádné záznamy v tomto období</h2><p class="muted">Začněte časovačem nebo přidejte záznam ručně.</p><button class="primary" data-new>Přidat záznam</button></div>';
  else $('list-view').innerHTML = days.slice(0,narrow.matches?1:7).map(d => {
    const daily = items.filter(e => e.start<d+dayMs && end(e)>d);
    if (!daily.length) return '';
    return `<div class="entry-day"><strong>${weekDay(d)} ${dateLabel(d)}</strong><strong>${duration(sum(daily,d,d+dayMs))}</strong></div>` + daily.map(e => `<div class="entry-row"><button class="entry-open" data-edit="${e.id}"><strong>${esc(e.description||'Bez popisu')}</strong><small>${esc(e.project||'Bez projektu')}${e.tags.length?' · '+esc(e.tags.join(', ')):''}</small>${overlaps(e)?'<small class="warning-text">⚠ Překryv</small>':''}</button><div class="entry-meta"><b>${duration(end(e)-e.start)}</b>${time(e.start)}–${e.end===null?'běží':time(e.end)}</div><button class="icon" data-continue="${e.id}" aria-label="Pokračovat: ${esc(e.description||'Bez popisu')}" title="Pokračovat">▶</button></div>`).join('');
  }).join('');
  renderTimer();
}
function renderTimer() {
  const entry = running();
  $('timer-toggle').textContent = entry ? '■ Zastavit' : '▶ Spustit';
  $('timer-new').hidden = !entry;
  const seconds = entry ? Math.max(0,Math.floor((now()-entry.start)/1000)) : 0;
  $('elapsed').textContent = [Math.floor(seconds/3600),Math.floor(seconds/60)%60,seconds%60].map(x => String(x).padStart(2,'0')).join(':');
  $('running-info').hidden = !entry;
  if (entry) $('running-info').textContent = `Běží: ${entry.description||'Bez popisu'} · ${entry.project||'Bez projektu'} · od ${time(entry.start)}`;
}
function startTimer(values) {
  const previous = running(), instant = now();
  if (previous) previous.end = instant;
  entries.push({id:nextId++,description:values.description,project:values.project,tags:[...values.tags],start:instant,end:null});
  render(); notify(previous ? 'Předchozí časovač zastaven, nový spuštěn.' : 'Časovač spuštěn.');
}
function formState() { return ['description','project','tags','start','end'].map(name => $('entry-'+name).value).join('|'); }
function updateOverlap() {
  const candidate = {id:editingId, start:parse($('entry-start').value), end:$('entry-end').disabled?null:parse($('entry-end').value)};
  $('overlap').hidden = !Number.isFinite(candidate.start) || !overlaps(candidate);
}
function openEditor(id = null, start = selectedDay + 9*3600000) {
  clearTimeout(autosaveTimer);
  editingId = id;
  const e = entries.find(e => e.id===id) || {description:'',project:'',tags:[],start,end:start+3600000};
  $('editor-title').textContent = id===null ? 'Přidat záznam' : 'Upravit záznam';
  $('entry-description').value = e.description; $('entry-project').value = e.project; $('entry-tags').value = e.tags.join(', ');
  $('entry-start').value = inputValue(e.start); $('entry-end').value = e.end===null ? '' : inputValue(e.end);
  $('entry-end').disabled = e.end===null; $('entry-end').required = e.end!==null;
  $('delete-entry').hidden = id===null; $('form-error').hidden = true;
  $('create-entry').hidden = id!==null; $('save-status').textContent = id===null ? '' : 'Uloženo';
  savedForm = formState(); updateOverlap(); $('editor').showModal(); $('entry-description').focus();
}
function confirmation(title, message, action, callback) {
  const wasEditing = $('editor').open;
  if (wasEditing) $('editor').close();
  $('confirmation-title').textContent = title; $('confirmation-message').textContent = message;
  $('confirmation-accept').textContent = action; $('confirmation-cancel').textContent = 'Zpět';
  const resolve = accepted => { $('confirmation').close(); if (wasEditing) $('editor').showModal(); callback(accepted); };
  $('confirmation-accept').onclick = () => resolve(true); $('confirmation-cancel').onclick = () => resolve(false);
  $('confirmation').oncancel = e => { e.preventDefault(); resolve(false); };
  $('confirmation').showModal(); $('confirmation-cancel').focus();
}
function closeEditor(force = false) {
  clearTimeout(autosaveTimer);
  if (force || formState() === savedForm || saveEditor()) { $('editor').close(); $('add').focus(); return; }
  confirmation('Zahodit neuložené změny?', 'Neplatné změny se neuloží. Dříve uložené hodnoty zůstanou zachované.', 'Zahodit neuložené změny', accepted => { if (accepted) closeEditor(true); });
}
$('calendar-tab').onclick = () => setView('calendar'); $('list-tab').onclick = () => setView('list');
function setView(value) { view=value; render(); }
$('add').onclick = () => openEditor();
$('previous').onclick = () => {selectedDay-=(narrow.matches?1:7)*dayMs;render();};
$('next').onclick = () => {selectedDay+=(narrow.matches?1:7)*dayMs;render();};
$('today').onclick = () => {selectedDay=dayStart(sampleNow);render();};
$('timer-toggle').onclick = () => {
  if (running()) {running().end=now();render();notify('Časovač zastaven.');}
  else startTimer({description:$('timer-description').value.trim(),project:$('timer-project').value,tags:tags($('timer-tags').value)});
};
$('timer-new').onclick = () => startTimer({description:$('timer-description').value.trim(),project:$('timer-project').value,tags:tags($('timer-tags').value)});
document.addEventListener('click', event => {
  const target = event.target.closest('button'); if (!target) return;
  if (target.dataset.edit) openEditor(Number(target.dataset.edit));
  if (target.dataset.continue) {const source=entries.find(e=>e.id===Number(target.dataset.continue));startTimer(source);}
  if (target.hasAttribute('data-new')) openEditor();
  if (target.dataset.create) {
    const box=target.getBoundingClientRect(); const minutes=Math.floor((event.clientY-box.top)/48*60/5)*5;
    openEditor(null,Number(target.dataset.create)+Math.max(0,Math.min(23*60,minutes))*60000);
  }
});
$('close-editor').onclick = () => closeEditor(); $('finish-editor').onclick = () => closeEditor();
$('editor').addEventListener('cancel', event => {event.preventDefault();closeEditor();});
$('entry-form').addEventListener('input', () => {
  updateOverlap(); clearTimeout(autosaveTimer);
  $('save-status').textContent = formState()===savedForm ? (editingId===null?'':'Uloženo') : 'Neuloženo';
  autosaveTimer=setTimeout(()=>saveEditor(),500);
});
$('entry-form').addEventListener('change',()=>saveEditor());
function saveEditor(force = false) {
  clearTimeout(autosaveTimer);
  if (!$('editor').open) return false;
  if (!force && formState()===savedForm) { $('form-error').hidden=true; return true; }
  const start=parse($('entry-start').value), finish=$('entry-end').disabled?null:parse($('entry-end').value);
  if (!Number.isFinite(start) || (finish!==null && (!Number.isFinite(finish)||finish<=start))) {
    $('form-error').textContent='Vyplňte platný začátek a konec pozdější než začátek.'; $('form-error').hidden=false;
    $('save-status').textContent='Neuloženo'; return false;
  }
  const value={id:editingId??nextId++,description:$('entry-description').value.trim(),project:$('entry-project').value,tags:tags($('entry-tags').value),start,end:finish};
  const index=entries.findIndex(e=>e.id===editingId); if(index<0) entries.push(value); else entries[index]=value;
  editingId=value.id; savedForm=formState();
  $('editor-title').textContent='Upravit záznam'; $('create-entry').hidden=true; $('delete-entry').hidden=false;
  $('form-error').hidden=true; $('save-status').textContent='Uloženo'; updateOverlap();
  const knownTags=[...new Set(entries.flatMap(e=>e.tags))]; $('tags').innerHTML=knownTags.map(t=>`<option value="${esc(t)}">`).join('');
  render(); return true;
}
$('entry-form').onsubmit = event => {event.preventDefault();saveEditor(editingId===null);};
$('delete-entry').onclick = () => {
  clearTimeout(autosaveTimer);
  const value=entries.find(e=>e.id===editingId);
  confirmation('Smazat záznam?', `${value.description||'Bez popisu'} · ${dateLabel(value.start)} ${time(value.start)}. Záznam se odstraní i z přehledů.`, 'Smazat záznam', accepted => {
    if(accepted){entries=entries.filter(e=>e.id!==editingId);closeEditor(true);render();notify('Záznam smazán.');}
    else if(formState()!==savedForm) autosaveTimer=setTimeout(()=>saveEditor(),500);
  });
};
narrow.addEventListener('change',render);
render(); $('calendar-scroll').scrollTop=8*48;
setInterval(renderTimer,1000);
