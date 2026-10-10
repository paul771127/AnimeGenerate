// HomeChat service worker:把網頁本身存在裝置上,家裡電腦關機時也能打開看舊訊息。
// 聊天文字由網頁存在 IndexedDB(見 index.html 的 local 區塊);這裡只快取網頁本身和看過的圖片 / 語音。
const CACHE = "homechat-v3";
const FILES = "homechat-files";  // 看過的圖片、語音:上傳後不會變,存在裝置上離線也能看
const SHELL = ["/", "/icon.svg", "/manifest.webmanifest"];
const TIMEOUT_MS = 5000;

self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE && k !== FILES).map(k => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

function fetchWithTimeout(request) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), TIMEOUT_MS);
  return fetch(request, { signal: ctrl.signal }).finally(() => clearTimeout(timer));
}

self.addEventListener("fetch", event => {
  const req = event.request;
  const url = new URL(req.url);
  if (req.method !== "GET" || url.origin !== self.location.origin) return;
  if (url.pathname.startsWith("/api/files/") && !url.searchParams.has("download") && !req.headers.has("range")) {
    event.respondWith((async () => {
      const cache = await caches.open(FILES);
      const hit = await cache.match(req);
      if (hit) return hit;
      const res = await fetch(req);
      const type = res.headers.get("Content-Type") || "";
      if (res.status === 200 && /^(image|audio)\//.test(type)) cache.put(req, res.clone());
      return res;
    })());
    return;
  }
  if (url.pathname.startsWith("/api/")) return;
  const isPage = req.mode === "navigate";
  if (!isPage && !SHELL.includes(url.pathname)) return;
  const key = isPage ? "/" : url.pathname;  // /c/..、/add/.. 都是同一個網頁
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      // 先問家裡電腦(拿最新版),連不上才用裝置上存的
      const res = await fetchWithTimeout(req);
      if (res.ok) cache.put(key, res.clone());
      return res;
    } catch (_) {
      return (await cache.match(key)) || (await cache.match("/")) || Response.error();
    }
  })());
});
