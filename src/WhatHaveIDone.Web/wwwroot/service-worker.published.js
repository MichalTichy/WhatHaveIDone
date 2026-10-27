self.importScripts('./service-worker-assets.js');
const cacheName='whid-'+self.assetsManifest.version;
const assets=self.assetsManifest.assets.filter(a=>!/service-worker|\.map$/.test(a.url));
self.addEventListener('message',event=>{if(event.data==='SKIP_WAITING')self.skipWaiting();});
self.addEventListener('install',event=>event.waitUntil(caches.open(cacheName).then(cache=>cache.addAll(assets.map(a=>new Request(a.url,{integrity:a.hash,cache:'no-cache'}))))));
self.addEventListener('activate',event=>event.waitUntil((async()=>{for(const key of await caches.keys())if(key.startsWith('whid-')&&key!==cacheName)await caches.delete(key);await self.clients.claim();})()));
self.addEventListener('fetch',event=>{
 const request=event.request,url=new URL(request.url);
 if(request.method!=='GET'||url.origin!==self.location.origin||url.pathname.startsWith('/api/')||url.pathname.startsWith('/auth/')||url.pathname.startsWith('/signin-'))return;
 event.respondWith((async()=>{const cache=await caches.open(cacheName);const key=request.mode==='navigate'?'index.html':request;return await cache.match(key)||fetch(request);})());
});
