// Matdo PWA – Service-Worker-Registrierung + Web-Push-Abo
(function () {
    'use strict';

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').catch(function (e) {
                console.warn('Service Worker Registrierung fehlgeschlagen:', e);
            });
        });
    }

    function urlBase64ToUint8Array(base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var raw = window.atob(base64);
        var arr = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; ++i) arr[i] = raw.charCodeAt(i);
        return arr;
    }

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function status(msg) {
        var el = document.getElementById('push-status');
        if (el) el.textContent = msg;
    }

    var enableBtn = document.getElementById('btn-enable-push');
    var disableBtn = document.getElementById('btn-disable-push');

    if (enableBtn) {
        enableBtn.addEventListener('click', async function () {
            try {
                if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                    status('Dieser Browser unterstützt keine Push-Benachrichtigungen.');
                    return;
                }
                var perm = await Notification.requestPermission();
                if (perm !== 'granted') { status('Berechtigung wurde nicht erteilt.'); return; }

                var reg = await navigator.serviceWorker.ready;
                var vapid = enableBtn.getAttribute('data-vapid');
                var sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(vapid)
                });
                var json = sub.toJSON();
                var resp = await fetch('/api/push/subscribe', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                    body: JSON.stringify({ endpoint: sub.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth })
                });
                if (!resp.ok) throw new Error('Server antwortete mit ' + resp.status);
                status('Benachrichtigungen sind aktiviert. ✓');
            } catch (e) {
                console.error(e);
                status('Aktivierung fehlgeschlagen: ' + e.message);
            }
        });
    }

    if (disableBtn) {
        disableBtn.addEventListener('click', async function () {
            try {
                var reg = await navigator.serviceWorker.ready;
                var sub = await reg.pushManager.getSubscription();
                if (sub) {
                    var endpoint = sub.endpoint;
                    // Zuerst lokal abmelden, dann den Server benachrichtigen (Zustände bleiben konsistent).
                    await sub.unsubscribe();
                    await fetch('/api/push/unsubscribe', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                        body: JSON.stringify({ endpoint: endpoint, p256dh: '', auth: '' })
                    });
                }
                status('Benachrichtigungen wurden deaktiviert.');
            } catch (e) {
                status('Deaktivierung fehlgeschlagen: ' + e.message);
            }
        });
    }
})();
