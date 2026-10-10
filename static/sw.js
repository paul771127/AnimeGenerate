// HomeChat service worker:把網頁本身存在裝置上,家裡電腦關機時也能打開看舊訊息。
// 聊天文字由網頁存在 IndexedDB(見 index.html 的 local 區塊);這裡只快取網頁本身和看過的圖片 / 語音。
const CACHE = "homechat-v8";
const FILES = "homechat-files";  // 看過的圖片、語音:上傳後不會變,存在裝置上離線也能看
const SHELL = ["/", "/icon.svg", "/manifest.webmanifest", "/sticker-maker.js"];
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
  const isFile = url.pathname.startsWith("/api/files/") || url.pathname.startsWith("/api/sticker-files/")
    || url.pathname.startsWith("/api/avatar/");  // 大頭貼網址有版本號,換照片網址就變
  if (isFile && !url.searchParams.has("download") && !req.headers.has("range")) {
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

// ---------- 推播通知
// 家裡電腦送來的推播是空的(推播服務看不到訊息內容),收到後自己去問家裡電腦要顯示什麼
self.addEventListener("push", event => {
  event.waitUntil((async () => {
    let info = { title: "HomeChat", body: "有新訊息", tag: "hc-new", vibrate: true };
    try {
      const ctrl = new AbortController();
      const timer = setTimeout(() => ctrl.abort(), 8000);
      const res = await fetch("/api/notify", { credentials: "same-origin", signal: ctrl.signal });
      clearTimeout(timer);
      if (res.ok) info = await res.json();
    } catch (_) {}
    const call = !!info.call;
    await self.registration.showNotification(info.title, {
      body: info.body,
      tag: info.tag,
      renotify: true,
      icon: "/icon.svg",
      badge: "/icon.svg",
      silent: !info.vibrate,
      vibrate: info.vibrate ? (call ? [500, 300, 500, 300, 500, 300, 500] : [200, 100, 200]) : undefined,
      requireInteraction: call,
      data: { contact_id: info.contact_id || null, call: info.call || null },
    });
  })());
});

self.addEventListener("notificationclick", event => {
  event.notification.close();
  const data = event.notification.data || {};
  event.waitUntil((async () => {
    const wins = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    const msg = { type: "hc-open", contact_id: data.contact_id, call: data.call };
    for (const w of wins) {
      if (new URL(w.url).origin === self.location.origin) {
        await w.focus();
        w.postMessage(msg);
        return;
      }
    }
    const q = data.call ? "?call=1" : data.contact_id ? `?open=${data.contact_id}` : "";
    await self.clients.openWindow("/" + q);
  })());
});

// 推播網址換了(瀏覽器自己更新)時重新訂閱
self.addEventListener("pushsubscriptionchange", event => {
  event.waitUntil((async () => {
    try {
      const key = (await (await fetch("/api/push/key", { credentials: "same-origin" })).json()).key;
      const raw = atob(key.replace(/-/g, "+").replace(/_/g, "/"));
      const sub = await self.registration.pushManager.subscribe({
        userVisibleOnly: true, applicationServerKey: Uint8Array.from(raw, c => c.charCodeAt(0)),
      });
      await fetch("/api/push/subscribe", {
        method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subscription: sub.toJSON(), vibrate: true }),
      });
    } catch (_) {}
  })());
});
