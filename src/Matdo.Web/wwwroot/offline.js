// Matdo – Offline-Warteschlange für Aufgaben (IndexedDB).
// Offline erfasste Aufgaben werden lokal gespeichert und beim Wiederverbinden
// über /api/tasks synchronisiert (landen im Eingang bzw. per Smart-Token zugeordnet).
(function () {
    'use strict';
    var DB = 'matdo-offline', STORE = 'queue', VERSION = 1;

    function openDb() {
        return new Promise(function (res, rej) {
            var r = indexedDB.open(DB, VERSION);
            r.onupgradeneeded = function () {
                if (!r.result.objectStoreNames.contains(STORE)) r.result.createObjectStore(STORE, { keyPath: 'id' });
            };
            r.onsuccess = function () { res(r.result); };
            r.onerror = function () { rej(r.error); };
        });
    }

    function store(mode) {
        return openDb().then(function (db) { return db.transaction(STORE, mode).objectStore(STORE); });
    }

    function add(title, description) {
        var item = { id: Date.now() + '-' + Math.random().toString(36).slice(2, 8), title: title, description: description || '', ts: Date.now() };
        return store('readwrite').then(function (st) {
            return new Promise(function (res, rej) { var r = st.add(item); r.onsuccess = function () { res(item); }; r.onerror = function () { rej(r.error); }; });
        });
    }

    function all() {
        return store('readonly').then(function (st) {
            return new Promise(function (res) { var r = st.getAll(); r.onsuccess = function () { res(r.result || []); }; r.onerror = function () { res([]); }; });
        }).catch(function () { return []; });
    }

    function remove(id) {
        return store('readwrite').then(function (st) {
            return new Promise(function (res) { var r = st.delete(id); r.onsuccess = function () { res(); }; r.onerror = function () { res(); }; });
        }).catch(function () { });
    }

    function count() { return all().then(function (a) { return a.length; }); }

    // Alle offenen Einträge zum Server schicken. Gibt die Anzahl synchronisierter Aufgaben zurück.
    function flush(token) {
        if (!navigator.onLine) return Promise.resolve(0);
        return all().then(function (items) {
            var done = 0;
            return items.reduce(function (chain, it) {
                return chain.then(function (stop) {
                    if (stop) return true;
                    return fetch('/api/tasks', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token || '' },
                        body: JSON.stringify({ title: it.title, description: it.description })
                    }).then(function (resp) {
                        if (resp.ok) { done++; return remove(it.id).then(function () { return false; }); }
                        if (resp.status === 401 || resp.status === 403) return true; // nicht eingeloggt / kein Token -> abbrechen
                        return false; // andere Fehler: Eintrag behalten, weiter
                    }).catch(function () { return true; }); // Verbindung weg -> abbrechen
                });
            }, Promise.resolve(false)).then(function () { return done; });
        });
    }

    window.MatdoOffline = { add: add, all: all, remove: remove, count: count, flush: flush };
})();
