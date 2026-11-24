window.whid={
 async start(){
  let culture=navigator.language.toLowerCase().startsWith('cs')?'cs':'en';
  try{
   const stored=localStorage.getItem('whid.language');
   if(stored==='cs'||stored==='en')culture=stored;
   const response=await fetch('api/v1/settings',{cache:'no-store'});
   if(response.ok){
    const {language}=await response.json();
    if(language==='cs'||language==='en'){
     culture=language;
     localStorage.setItem('whid.language',culture);
    }
   }
  }catch{}
  document.documentElement.lang=culture;
  await Blazor.start({applicationCulture:culture});
  this.registerWorker();
 },
 tagKeys(element){element.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===',')event.preventDefault();});},
 openDialog(element,dotnet){
  const requestClose=()=>dotnet.invokeMethodAsync('RequestCloseAsync');
  const listeners=new AbortController();
  const options={signal:listeners.signal};
  element._dialogListeners?.abort();
  element._dialogListeners=listeners;
  element.addEventListener('cancel',event=>{
   event.preventDefault();
   requestClose();
  },options);
  if(element.classList.contains('drawer')){
   const isBackdrop=event=>{
    if(event.target!==element)return false;
    const {left,right,top,bottom}=element.getBoundingClientRect();
    return event.clientX<left||event.clientX>=right||event.clientY<top||event.clientY>=bottom;
   };
   let startedOnBackdrop=false;
   element.addEventListener('pointerdown',event=>{
    startedOnBackdrop=isBackdrop(event);
   },options);
   element.addEventListener('click',event=>{
    const clickedBackdrop=startedOnBackdrop&&isBackdrop(event);
    startedOnBackdrop=false;
    if(clickedBackdrop)requestClose();
   },options);
  }
  element.showModal();
 },
 closeDialog(element){
  element._dialogListeners?.abort();
  if(element.open)element.close();
 },
 focusPopoverSearch(element,trigger){
  const input=element.querySelector('input');
  const highlight=option=>{
   element.querySelector('[data-select-active]')?.removeAttribute('data-select-active');
   if(!option){input.removeAttribute('aria-activedescendant');return;}
   option.setAttribute('data-select-active','');
   input.setAttribute('aria-activedescendant',option.id);
   option.scrollIntoView({block:'nearest'});
  };
  if(!element._pickerKeys){
   element._pickerKeys=true;
   element.addEventListener('input',()=>highlight(null),true);
   element.addEventListener('keydown',event=>{
    if(!['Escape','ArrowDown','ArrowUp','Enter'].includes(event.key))return;
    event.preventDefault();
    event.stopPropagation();
    if(event.key==='Escape'){this.closePopover(element,trigger);return;}
    const options=[...element.querySelectorAll('[role="option"]:not([aria-disabled="true"])')].filter(option=>option.getClientRects().length);
    const active=options.findIndex(option=>option.hasAttribute('data-select-active'));
    if(event.key==='Enter'){(options[active]??options[0])?.click();return;}
    const next=event.key==='ArrowDown'?Math.min(active+1,options.length-1):active<0?options.length-1:Math.max(active-1,0);
    highlight(options[next]);
   },true);
  }
  highlight(null);
  requestAnimationFrame(()=>{if(element.matches(':popover-open'))input.focus();});
 },
 closePopover(element,trigger){element.hidePopover();trigger.focus();},
 copy(text){return navigator.clipboard.writeText(text);},
 watchers:new Map(),
 isNarrow(){return window.innerWidth<800;},
 watchPage(dotnet){const id=crypto.randomUUID();const focus=()=>dotnet.invokeMethodAsync('RefreshAsync');const resize=()=>dotnet.invokeMethodAsync('ViewportAsync',window.innerWidth<800);window.addEventListener('focus',focus);window.addEventListener('resize',resize);this.watchers.set(id,{focus,resize});return id;},
 unwatchPage(id){const value=this.watchers.get(id);if(value){window.removeEventListener('focus',value.focus);window.removeEventListener('resize',value.resize);this.watchers.delete(id);}},
 async registerWorker(){if(!navigator.serviceWorker)return;let registration;try{registration=await navigator.serviceWorker.register('service-worker.js');}catch{return;}if(!registration)return;this.registration=registration;const notify=()=>window.dispatchEvent(new Event('whid:update'));if(registration.waiting)notify();registration.addEventListener('updatefound',()=>registration.installing?.addEventListener('statechange',()=>{if(registration.waiting&&navigator.serviceWorker.controller)notify();}));navigator.serviceWorker.addEventListener('controllerchange',()=>{if(this.requestedUpdate)location.reload();});},
 updateAvailable(){return !!this.registration?.waiting;},
 watchUpdates(dotnet){const handler=()=>dotnet.invokeMethodAsync('UpdateAvailableAsync');window.addEventListener('whid:update',handler);const id=crypto.randomUUID();this.watchers.set(id,{update:handler});return id;},
 unwatchUpdates(id){const item=this.watchers.get(id);if(item){window.removeEventListener('whid:update',item.update);this.watchers.delete(id);}},
 applyUpdate(){if(this.registration?.waiting){this.requestedUpdate=true;this.registration.waiting.postMessage('SKIP_WAITING');}else location.reload();},
 download(name,content){const url=URL.createObjectURL(new Blob([content],{type:'text/csv;charset=utf-8'}));const link=document.createElement('a');link.href=url;link.download=name;link.click();URL.revokeObjectURL(url);}
};
