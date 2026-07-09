// Matdo Service Worker – App-Shell-Caching + Push-Benachrichtigungen
const CACHE = 'matdo-v1';
const APP_SHELL = [
    '/css/site.css',
    '/js/site.js',
    '/js/pwa.js',
    '/icons/icon.svg',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/favicon.ico'
];

self.addEventListener('install', (event) => {
    event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(APP_SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    // Statische Assets: Cache-first
    if (APP_SHELL.includes(url.pathname) || url.pathname.startsWith('/icons/') || url.pathname.startsWith('/css/') || url.pathname.startsWith('/js/')) {
        event.respondWith(
            caches.match(req).then((cached) => cached || fetch(req).then((resp) => {
                const copy = resp.clone();
                caches.open(CACHE).then((c) => c.put(req, copy));
                return resp;
            }))
        );
        return;
    }

    // Navigation/Seiten: Network-first mit Offline-Fallback
    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req).catch(() => caches.match('/offline.html').then((r) => r || new Response('Offline', { status: 503, headers: { 'Content-Type': 'text/plain' } })))
        );
    }
});

// ---- Push ----
self.addEventListener('push', (event) => {
    let data = { title: 'Matdo', body: 'Erinnerung', url: '/' };
    try { if (event.data) data = Object.assign(data, event.data.json()); } catch (e) { }
    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: '/icons/icon-192.png',
            badge: '/icons/icon-192.png',
            data: { url: data.url || '/' },
            vibrate: [80, 40, 80]
        })
    );
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const target = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((list) => {
            for (const client of list) {
                if ('focus' in client) { client.navigate(target); return client.focus(); }
            }
            return self.clients.openWindow(target);
        })
    );
});
