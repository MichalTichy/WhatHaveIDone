import {Calendar} from '@fullcalendar/core';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin from '@fullcalendar/interaction';
import luxonPlugin from '@fullcalendar/luxon3';
import csLocale from '@fullcalendar/core/locales/cs';
import {DateTime} from 'luxon';
import {shortGapAt,snapToNeighbors} from './calendar-behavior.mjs';
const instances=new WeakMap();
export function create(element,dotnet,options){
 const pending=new Set();
 const intervals=except=>calendar.getEvents().filter(e=>e.id!==except&&!e.extendedProps.zeroDuration&&e.start&&e.end).map(e=>({start:+e.start,end:+e.end}));
 async function change(info,mode){
  const id=info.event.id;if(pending.has(id)){info.revert();return;}
  const snapped=snapToNeighbors(+info.event.start,+info.event.end,intervals(id),mode);
  info.event.setDates(new Date(snapped.start),new Date(snapped.end));
  pending.add(id);info.event.setProp('editable',false);
  try{if(!await dotnet.invokeMethodAsync('MoveAsync',id,info.event.startStr,info.event.endStr))info.revert();}
  catch{info.revert();}
  finally{pending.delete(id);calendar.getEventById(id)?.setProp('editable',true);}
 }
 const calendar=new Calendar(element,{
  plugins:[timeGridPlugin,interactionPlugin,luxonPlugin],locales:[csLocale],locale:options.locale,timeZone:options.timeZone,firstDay:options.firstDay,
  initialView:window.innerWidth<800?'timeGridDay':'timeGridWeek',initialDate:options.date,
  headerToolbar:false,height:650,allDaySlot:false,scrollTime:'08:00:00',nowIndicator:true,editable:true,selectable:true,
  slotDuration:'00:30:00',snapDuration:'00:05:00',eventOverlap:true,selectOverlap:true,longPressDelay:500,
  select(info){
   const local=DateTime.fromJSDate(info.start,{zone:calendar.getOption('timeZone')});
   let point=+info.start;
   // Use the click position inside the grid, since a five-minute snap can fall inside the preceding entry.
   const y=info.jsEvent?.clientY;
   const slot=[...element.querySelectorAll('.fc-timegrid-slot-lane[data-time]')].find(cell=>{const r=cell.getBoundingClientRect();return y>=r.top&&y<r.bottom;});
   if(slot){const r=slot.getBoundingClientRect(),[hour,minute]=slot.dataset.time.split(':').map(Number);point+=(hour*60+minute+(y-r.top)/r.height*30-local.hour*60-local.minute-local.second/60)*60*1000;}
   const gap=+info.end-+info.start<=5*60*1000?shortGapAt(point,intervals(),+local.startOf('day'),+local.plus({days:1}).startOf('day')):null;
   dotnet.invokeMethodAsync('SelectAsync',gap?new Date(gap.start).toISOString():info.startStr,gap?new Date(gap.end).toISOString():info.endStr,!!gap);calendar.unselect();
  },
  eventClick(info){if(!pending.has(info.event.id))dotnet.invokeMethodAsync('OpenAsync',info.event.id);},
  eventDrop:info=>change(info,'move'),eventResize:info=>change(info,+info.event.start!==+info.oldEvent.start?'resizeStart':'resizeEnd'),
  windowResize(){const view=window.innerWidth<800?'timeGridDay':'timeGridWeek';if(calendar.view.type!==view)calendar.changeView(view);}
 });
 instances.set(element,{calendar,pending});calendar.render();update(element,options);
}
export function update(element,options){const state=instances.get(element);if(!state)return;const {calendar,pending}=state;calendar.setOption('timeZone',options.timeZone);calendar.setOption('locale',options.locale);calendar.setOption('firstDay',options.firstDay);calendar.setOption('dayHeaderContent',arg=>{const label=document.createElement('span'),total=document.createElement('small');label.className='calendar-day';label.textContent=arg.text;total.className='muted';total.textContent=options.dayTotals[DateTime.fromJSDate(arg.date,{zone:options.timeZone}).toISODate()]??'0:00:00';label.append(total);return{domNodes:[label]};});calendar.gotoDate(options.date);calendar.removeAllEvents();calendar.addEventSource(options.events.map(event=>({...event,editable:!pending.has(event.id)&&event.editable})));}
export function dispose(element){instances.get(element)?.calendar.destroy();instances.delete(element);}
