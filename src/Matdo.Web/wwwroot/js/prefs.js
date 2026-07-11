// Matdo – Darstellungs-/Sprach-Einstellungen: sofort anwenden + speichern
(function () {
    'use strict';

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function save(payload, reload) {
        var el = document.getElementById('prefs-status');
        fetch('/api/prefs', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
            body: JSON.stringify(payload)
        }).then(function (r) {
            if (reload) { location.reload(); return; }
            if (el) { el.textContent = '✓'; setTimeout(function () { el.textContent = ''; }, 1200); }
        }).catch(function () { if (el) el.textContent = '⚠'; });
    }

    function activate(btn, attr) {
        document.querySelectorAll('[' + attr + ']').forEach(function (x) { x.classList.remove('active'); });
        btn.classList.add('active');
    }

    document.addEventListener('click', function (e) {
        var s = e.target.closest('[data-pref-scheme]');
        if (s) {
            var v = s.getAttribute('data-pref-scheme');
            document.documentElement.setAttribute('data-mode', v);
            activate(s, 'data-pref-scheme');
            save({ scheme: v }, false);
            return;
        }
        var t = e.target.closest('[data-pref-theme]');
        if (t) {
            var v = t.getAttribute('data-pref-theme');
            document.documentElement.setAttribute('data-theme', v);
            activate(t, 'data-pref-theme');
            var dot = t.querySelector('.sw-dot');
            var meta = document.querySelector('meta[name="theme-color"]');
            if (dot && meta) meta.setAttribute('content', getComputedStyle(dot).backgroundColor);
            save({ theme: v }, false);
            return;
        }
        var l = e.target.closest('[data-pref-lang]');
        if (l) {
            // Sprache ändert serverseitig gerenderte Texte -> neu laden.
            save({ lang: l.getAttribute('data-pref-lang') }, true);
            return;
        }
    });
})();
