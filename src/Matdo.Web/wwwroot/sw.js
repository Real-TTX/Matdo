// Matdo Service Worker – App-Shell-Caching + Push-Benachrichtigungen
const CACHE = 'matdo-v26';
const APP_SHELL = [
    '/offline.html',
    '/offline.js',
    '/css/site.css',
    '/js/site.js',
    '/js/composer.js',
    '/js/datepicker.js',
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

    // Statische Assets (css/js/icons): exakt-versioniert cachen.
    // Da das Layout ?v=HASH anhängt, ist jede neue Version ein NEUER Cache-Key ->
    // nach einem Deploy wird das Asset frisch geladen (kein „veraltetes JS" mehr).
    // Reihenfolge: exakter Cache-Treffer -> Netzwerk (und cachen) -> offline: Precache (ignoreSearch).
    if (APP_SHELL.includes(url.pathname) || url.pathname.startsWith('/icons/') || url.pathname.startsWith('/css/') || url.pathname.startsWith('/js/')) {
        event.respondWith(
            caches.open(CACHE).then((cache) =>
                cache.match(req).then((hit) => {
                    if (hit) return hit;
                    return fetch(req).then((resp) => {
                        if (resp && resp.ok) {
                            // Alte Versionen desselben Pfads entfernen (Cache wächst sonst unbegrenzt).
                            cache.keys().then((keys) => keys.forEach((k) => {
                                try { var ku = new URL(k.url); if (ku.pathname === url.pathname && k.url !== req.url) cache.delete(k); } catch (e) { }
                            }));
                            cache.put(req, resp.clone());
                        }
                        return resp;
                    }).catch(() => cache.match(req, { ignoreSearch: true }));
                })
            )
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
