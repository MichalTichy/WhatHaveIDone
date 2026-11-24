// Development intentionally bypasses asset caching.
// The SDK versions this file on every build, so the update banner still needs the activation message.
self.addEventListener('message',event=>{if(event.data==='SKIP_WAITING')self.skipWaiting();});
self.addEventListener('fetch',() => {});
