// Matdo – Aufgaben im Dialog bearbeiten (per fetch geladen; mobil Vollbild).
// Effizienter beim Abarbeiten mehrerer Aufgaben: man bleibt auf der Liste.
(function () {
    'use strict';

    var host = null, currentEditUrl = null;

    function ensureHost() {
        if (host) return host;
        host = document.createElement('div');
        host.className = 'modal-host';
        host.innerHTML =
            '<div class="modal-backdrop" data-modal-close></div>' +
            '<div class="modal-dialog" role="dialog" aria-modal="true">' +
            '<button type="button" class="modal-x" data-modal-close aria-label="Schließen"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg></button>' +
            '<div class="modal-panel"></div></div>';
        document.body.appendChild(host);
        return host;
    }

    function withModal(url) { return url + (url.indexOf('?') >= 0 ? '&' : '?') + 'modal=1'; }

    function setPanel(html) {
        var panel = host.querySelector('.modal-panel');
        panel.innerHTML = html;
        // Formulare ohne action posten sonst auf die Listen-URL -> auf die Edit-URL zeigen.
        panel.querySelectorAll('form:not([action])').forEach(function (f) { f.setAttribute('action', currentEditUrl); });
        var focusable = panel.querySelector('input:not([type=hidden]), textarea');
        if (focusable) setTimeout(function () { try { focusable.focus(); } catch (e) { } }, 30);
    }

    function openModal(editUrl, desiredTab) {
        currentEditUrl = editUrl;
        ensureHost();
        return fetch(withModal(editUrl), { headers: { 'X-Requested-With': 'fetch' } })
            .then(function (r) { return r.text(); })
            .then(function (html) {
                setPanel(html);
                host.classList.add('open');
                document.body.classList.add('modal-lock');
                if (desiredTab) activateTab(desiredTab);
            })
            .catch(function () { window.location.href = editUrl; });
    }

    function activateTab(target) {
        var panel = host && host.querySelector('.modal-panel');
        if (!panel) return;
        var bar = panel.querySelector('.tab-bar');
        if (bar) bar.querySelectorAll('.tab').forEach(function (t) { t.classList.toggle('active', t.getAttribute('data-tab-target') === target); });
        panel.querySelectorAll('.tab-panel').forEach(function (p) { p.classList.toggle('active', p.getAttribute('data-tab-panel') === target); });
    }

    function closeModal() {
        if (host) { host.classList.remove('open'); host.querySelector('.modal-panel').innerHTML = ''; }
        document.body.classList.remove('modal-lock');
        currentEditUrl = null;
    }

    document.addEventListener('click', function (e) {
        var a = e.target.closest('a[href^="/Tasks/Edit"], a[data-modal]');
        if (a && e.button === 0 && !e.metaKey && !e.ctrlKey && !e.shiftKey) {
            e.preventDefault();
            openModal(a.getAttribute('href'));
            return;
        }
        if (e.target.closest('[data-modal-close]')) {
            // Nur eingreifen, wenn das Modal offen ist (sonst normalen Link/Button zulassen).
            if (host && host.classList.contains('open')) { e.preventDefault(); closeModal(); }
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || !host || !host.classList.contains('open')) return;
        if (document.querySelector('.ac-box') || document.querySelector('.ppick.open') || document.querySelector('.dp-pop')) return; // erst Overlay schließen
        closeModal();
    });

    // Formulare im Modal per fetch verarbeiten
    document.addEventListener('submit', function (e) {
        if (!host || !host.classList.contains('open')) return;
        var form = e.target;
        if (!host.contains(form)) return;
        e.preventDefault();

        var submitter = e.submitter;
        var action = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || currentEditUrl;
        var handler = (action.match(/[?&]handler=([^&]*)/) || [])[1] || '';
        var fd = new FormData(form, submitter);

        fetch(withModal(action), { method: 'POST', body: fd, headers: { 'X-Requested-With': 'fetch' } })
            .then(function (r) { return r.text().then(function (html) { return { redirected: r.redirected, html: html }; }); })
            .then(function (res) {
                if (!res.redirected) { setPanel(res.html); return; }   // Validierungsfehler / Re-Render
                if (form.hasAttribute('data-modal-reload') || handler === '' || /delete/i.test(handler)) { closeModal(); window.location.reload(); }
                else {
                    // Unter-Aktion -> Modal neu laden und den passenden Tab wieder aktivieren
                    var tabFor = { addsubtask: 'subtasks', addreminder: 'reminders', removereminder: 'reminders', share: 'share', unshare: 'share' };
                    openModal(currentEditUrl, tabFor[handler.toLowerCase()]);
                }
            })
            .catch(function () { form.submit(); });
    });
})();
