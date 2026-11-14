window.whid={
 tagKeys(element){element.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===',')event.preventDefault();});},
 openDialog(element,dotnet){
  element._closeHandler=event=>{event.preventDefault();dotnet.invokeMethodAsync('RequestCloseAsync');};
  element.addEventListener('cancel',element._closeHandler);element.showModal();
 },
 closeDialog(element){element.removeEventListener('cancel',element._closeHandler);if(element.open)element.close();},
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
