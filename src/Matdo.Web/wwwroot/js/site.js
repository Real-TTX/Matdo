// Matdo – UI-Interaktionen (ohne externe Abhängigkeiten)
(function () {
    'use strict';

    // ---------- Mobile-Sidebar ----------
    function toggleSidebar(open) {
        document.body.classList.toggle('sidebar-open', open);
    }
    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-toggle-sidebar]')) { toggleSidebar(true); }
        else if (e.target.closest('[data-close-sidebar]')) { toggleSidebar(false); }
    });

    // ---------- In-Page-Tabs ----------
    document.addEventListener('click', function (e) {
        var tab = e.target.closest('[data-tab-target]');
        if (!tab) return;
        var target = tab.getAttribute('data-tab-target');
        var bar = tab.closest('.tab-bar');
        if (bar) bar.querySelectorAll('.tab').forEach(function (t) { t.classList.remove('active'); });
        tab.classList.add('active');
        var scope = tab.closest('[data-tabs-scope]') || document;
        scope.querySelectorAll('.tab-panel').forEach(function (p) {
            p.classList.toggle('active', p.getAttribute('data-tab-panel') === target);
        });
    });

    // ---------- Anti-Forgery Token holen ----------
    function antiForgery() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // ---------- Aufgabe abhaken ----------
    document.addEventListener('click', function (e) {
        var check = e.target.closest('[data-complete-task]');
        if (!check) return;
        e.preventDefault();
        var id = check.getAttribute('data-complete-task');
        var completed = check.getAttribute('data-completed') !== 'true';
        fetch('/api/tasks/' + id + '/complete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiForgery() },
            body: JSON.stringify({ completed: completed })
        }).then(function (r) {
            if (r.ok) {
                var row = check.closest('.task-row');
                if (row) {
                    row.classList.toggle('done', completed);
                    check.setAttribute('data-completed', completed);
                    check.innerHTML = completed ? '✓' : '';
                    if (completed) setTimeout(function () { row.style.transition = 'opacity .3s'; row.style.opacity = '.4'; }, 100);
                    else row.style.opacity = '';
                }
            }
        });
    });

    // ---------- Kanban Drag & Drop ----------
    var dragEl = null;
    document.addEventListener('dragstart', function (e) {
        var card = e.target.closest('.kanban-card');
        if (!card) return;
        dragEl = card;
        card.classList.add('dragging');
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', card.getAttribute('data-task-id'));
    });
    document.addEventListener('dragend', function () {
        if (dragEl) dragEl.classList.remove('dragging');
        document.querySelectorAll('.kanban-cards.drop-hover').forEach(function (c) { c.classList.remove('drop-hover'); });
        dragEl = null;
    });
    document.addEventListener('dragover', function (e) {
        var zone = e.target.closest('.kanban-cards');
        if (!zone || !dragEl) return;
        e.preventDefault();
        zone.classList.add('drop-hover');
    });
    document.addEventListener('dragleave', function (e) {
        var zone = e.target.closest('.kanban-cards');
        if (zone && !zone.contains(e.relatedTarget)) zone.classList.remove('drop-hover');
    });
    document.addEventListener('drop', function (e) {
        var zone = e.target.closest('.kanban-cards');
        if (!zone || !dragEl) return;
        e.preventDefault();
        zone.classList.remove('drop-hover');
        zone.appendChild(dragEl);
        var taskId = dragEl.getAttribute('data-task-id');
        var columnId = zone.getAttribute('data-column-id');
        var position = Array.prototype.indexOf.call(zone.children, dragEl);
        // Spaltenzähler aktualisieren
        document.querySelectorAll('.kanban-col').forEach(function (col) {
            var cnt = col.querySelector('.col-count');
            var cards = col.querySelector('.kanban-cards');
            if (cnt && cards) cnt.textContent = cards.children.length;
        });
        fetch('/api/tasks/' + taskId + '/move', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiForgery() },
            body: JSON.stringify({ columnId: columnId ? parseInt(columnId, 10) : null, position: position })
        });
    });

    // ---------- Dropdown-Menü (Topbar-Aktionen etc.) ----------
    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-menu-toggle]');
        if (toggle) {
            e.preventDefault();
            var menu = toggle.closest('[data-menu]');
            var wasOpen = menu.classList.contains('open');
            document.querySelectorAll('[data-menu].open').forEach(function (m) { m.classList.remove('open'); });
            if (!wasOpen) menu.classList.add('open');
            return;
        }
        if (!e.target.closest('[data-menu]')) {
            document.querySelectorAll('[data-menu].open').forEach(function (m) { m.classList.remove('open'); });
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') document.querySelectorAll('[data-menu].open').forEach(function (m) { m.classList.remove('open'); });
    });

    // ---------- Aufgaben-Schnellaktionen (Duplizieren / Löschen) ----------
    document.addEventListener('click', function (e) {
        var del = e.target.closest('[data-task-delete]');
        if (del) {
            e.preventDefault();
            if (!window.confirm(del.getAttribute('data-confirm') || 'Wirklich löschen?')) return;
            fetch('/api/tasks/' + del.getAttribute('data-task-delete') + '/delete',
                { method: 'POST', headers: { 'RequestVerificationToken': antiForgery() } })
                .then(function (r) {
                    if (!r.ok) return;
                    var row = del.closest('.task-row');
                    if (row) row.remove(); else location.reload();
                });
            return;
        }
        var dup = e.target.closest('[data-task-duplicate]');
        if (dup) {
            e.preventDefault();
            fetch('/api/tasks/' + dup.getAttribute('data-task-duplicate') + '/duplicate',
                { method: 'POST', headers: { 'RequestVerificationToken': antiForgery() } })
                .then(function (r) { if (r.ok) location.reload(); });
            return;
        }
        var cp = e.target.closest('[data-copy-link]');
        if (cp) {
            e.preventDefault();
            var url = cp.getAttribute('data-copy-link');
            var done = function () { toast(cp.getAttribute('data-copied') || 'Link'); };
            if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(url).then(done, done);
            else { try { var ta = document.createElement('textarea'); ta.value = url; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove(); } catch (err) { } done(); }
            document.querySelectorAll('[data-menu].open').forEach(function (m) { m.classList.remove('open'); });
            return;
        }
    });

    // ---------- Bestätigung für Löschen ----------
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form.hasAttribute('data-confirm')) {
            if (!window.confirm(form.getAttribute('data-confirm') || 'Wirklich löschen?')) {
                e.preventDefault();
            }
        }
    });

    // ---------- Offline (PWA): Banner, lokale Erfassung, Auto-Sync ----------
    function offlineAttr(key, fallback) { var b = document.getElementById('offline-bar'); return (b && b.getAttribute('data-' + key)) || fallback; }

    function updateOfflineUI() {
        var bar = document.getElementById('offline-bar');
        if (!bar) return;
        if (navigator.onLine) { bar.hidden = true; return; }
        bar.hidden = false;
        if (window.MatdoOffline) window.MatdoOffline.count().then(function (n) {
            var c = document.getElementById('offline-count');
            if (c) c.textContent = n > 0 ? '· ' + n : '';
        });
    }

    function toast(msg) {
        var t = document.createElement('div');
        t.className = 'toast';
        t.textContent = msg;
        document.body.appendChild(t);
        requestAnimationFrame(function () { t.classList.add('show'); });
        setTimeout(function () { t.classList.remove('show'); setTimeout(function () { t.remove(); }, 300); }, 3200);
    }

    var flushing = false;
    function flushOffline() {
        if (flushing || !window.MatdoOffline || !navigator.onLine) return;
        flushing = true;
        window.MatdoOffline.flush(antiForgery())
            .then(function (n) { flushing = false; if (n > 0) location.reload(); })
            .catch(function () { flushing = false; });
    }

    window.addEventListener('online', function () { updateOfflineUI(); flushOffline(); });
    window.addEventListener('offline', updateOfflineUI);
    document.addEventListener('DOMContentLoaded', function () { updateOfflineUI(); flushOffline(); });

    // Composer offline: statt fehlschlagendem POST die Aufgabe lokal in die Warteschlange legen.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form.matches || !form.matches('form[data-composer]')) return;
        if (navigator.onLine || !window.MatdoOffline) return;
        var titleEl = form.querySelector('.title, [name="Input.Title"]');
        var title = titleEl ? titleEl.value.trim() : '';
        if (!title) return;
        e.preventDefault();
        var descEl = form.querySelector('[name="Input.Description"]');
        window.MatdoOffline.add(title, descEl ? descEl.value.trim() : '').then(function () {
            try { form.reset(); } catch (x) { }
            updateOfflineUI();
            toast(offlineAttr('saved', 'Offline gespeichert – wird bei Verbindung synchronisiert.'));
        });
    }, true);
})();
