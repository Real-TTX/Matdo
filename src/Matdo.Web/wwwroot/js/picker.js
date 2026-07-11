// Matdo – Projekt-Picker (Popover mit Suche; mobil Vollbild)
(function () {
    'use strict';

    function closeAll(except) {
        document.querySelectorAll('.ppick.open').forEach(function (p) {
            if (p !== except) { p.classList.remove('open'); }
        });
        if (!document.querySelector('.ppick.open')) document.body.classList.remove('ppick-open');
    }

    function open(ppick) {
        closeAll(ppick);
        ppick.classList.add('open');
        document.body.classList.add('ppick-open');
        var search = ppick.querySelector('[data-ppick-search]');
        if (search) { search.value = ''; filter(ppick, ''); setTimeout(function () { try { search.focus(); } catch (e) { } }, 30); }
        var sel = ppick.querySelector('.ppick-item.sel');
        if (sel) sel.scrollIntoView({ block: 'nearest' });
    }

    function close(ppick) { ppick.classList.remove('open'); if (!document.querySelector('.ppick.open')) document.body.classList.remove('ppick-open'); }

    function filter(ppick, q) {
        q = (q || '').trim().toLowerCase();
        ppick.querySelectorAll('.ppick-item').forEach(function (it) {
            var label = (it.getAttribute('data-label') || '').toLowerCase();
            it.style.display = (q === '' || label.indexOf(q) >= 0) ? '' : 'none';
        });
        // Abschnitts-Überschrift ausblenden, wenn kein Projekt sichtbar
        var section = ppick.querySelector('.ppick-section');
        if (section) {
            var anyProject = Array.prototype.some.call(ppick.querySelectorAll('.ppick-item[data-pid]:not([data-pid=""])'), function (it) { return it.style.display !== 'none' && it.getAttribute('data-pid'); });
            section.style.display = anyProject ? '' : 'none';
        }
    }

    function choose(ppick, item) {
        var val = item.getAttribute('data-pid') || '';
        var hidden = ppick.querySelector('[data-ppick-value]');
        // 'change' feuern, damit der Composer das Projekt-Badge aktualisiert.
        if (hidden) { hidden.value = val; hidden.dispatchEvent(new Event('change', { bubbles: true })); }
        // Button-Anzeige aus dem gewählten Eintrag übernehmen (Icon + Name)
        var cur = ppick.querySelector('.ppick-cur');
        var ico = item.querySelector('.proj-ico');
        var name = item.getAttribute('data-label') || '';
        if (cur) cur.innerHTML = (ico ? ico.outerHTML : '') + '<span class="ppick-label"></span>';
        var lbl = cur && cur.querySelector('.ppick-label'); if (lbl) lbl.textContent = name;
        ppick.querySelectorAll('.ppick-item').forEach(function (i) { i.classList.toggle('sel', i === item); });
        close(ppick);
    }

    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-ppick-toggle]');
        if (toggle) {
            e.preventDefault();
            var ppick = toggle.closest('.ppick');
            if (ppick.classList.contains('open')) close(ppick); else open(ppick);
            return;
        }
        if (e.target.closest('[data-ppick-close]')) { var pp = e.target.closest('.ppick'); if (pp) close(pp); return; }
        var item = e.target.closest('.ppick-item');
        if (item) { e.preventDefault(); choose(item.closest('.ppick'), item); return; }
        if (!e.target.closest('.ppick-pop') && !e.target.closest('[data-ppick-toggle]')) closeAll();
    });

    document.addEventListener('input', function (e) {
        var s = e.target.closest('[data-ppick-search]');
        if (s) filter(s.closest('.ppick'), s.value);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && document.querySelector('.ppick.open')) { e.stopImmediatePropagation(); closeAll(); }
    });

    // Nach Formular-Reset (Abbrechen) den Button-Text mit dem Hidden-Wert abgleichen
    document.addEventListener('reset', function (e) {
        var form = e.target;
        setTimeout(function () {
            form.querySelectorAll('.ppick').forEach(function (ppick) {
                var hidden = ppick.querySelector('[data-ppick-value]');
                var val = hidden ? hidden.value : '';
                var item = ppick.querySelector('.ppick-item[data-pid="' + val + '"]') || ppick.querySelector('.ppick-item[data-pid=""]');
                if (item) choose(ppick, item);
            });
        }, 0);
    });
})();
