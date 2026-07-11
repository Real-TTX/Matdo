// Matdo – Inline-Aufgaben-Composer (Todoist-artig)
// Eigenschaften werden inline über Popover gesetzt, als entfernbare Chips angezeigt.
(function () {
    'use strict';

    // Sprachabhängige Beschriftungen (aus <html lang>)
    var LANG = (document.documentElement.getAttribute('lang') || 'de').slice(0, 2).toLowerCase();
    var I18N = {
        de: { today: 'Heute', tomorrow: 'Morgen', months: ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez'], fmt: function (d, m) { return d + '. ' + m; } },
        en: { today: 'Today', tomorrow: 'Tomorrow', months: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'], fmt: function (d, m) { return m + ' ' + d; } }
    };
    var L = I18N[LANG] || I18N.de;

    function pad(n) { return (n < 10 ? '0' : '') + n; }
    function todayStr() { var d = new Date(); return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
    function addDaysStr(base, days) { var d = new Date(base + 'T00:00:00'); d.setDate(d.getDate() + days); return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }

    function fmtDate(val, time) {
        if (!val) return '';
        var t = todayStr();
        var label;
        if (val === t) label = L.today;
        else if (val === addDaysStr(t, 1)) label = L.tomorrow;
        else {
            var d = new Date(val + 'T00:00:00');
            label = L.fmt(d.getDate(), L.months[d.getMonth()]);
        }
        if (time) label += ' ' + time;
        return label;
    }

    function icon(name) {
        // Minimales Inline-SVG passend zu den Server-Icons
        var paths = {
            calendar: "<rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/>",
            flag: "<path d='M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z'/><line x1='4' x2='4' y1='22' y2='15'/>",
            bell: "<path d='M10.268 21a2 2 0 0 0 3.464 0'/><path d='M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326z'/>",
            tag: "<path d='M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z'/>",
            x: "<path d='M18 6 6 18M6 6l12 12'/>"
        };
        return "<svg width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" + (paths[name] || '') + "</svg>";
    }

    function get(form, sel) { return form.querySelector(sel); }
    function val(form, f) { var el = form.querySelector('[data-f="' + f + '"]'); return el ? el.value : ''; }

    function refreshChips(form) {
        var wrap = form.querySelector('[data-chips]');
        if (!wrap) return;
        var chips = [];

        // Projekt (aus dem Projekt-Picker) – als Badge anzeigen
        var ppick = form.querySelector('.ppick');
        if (ppick) {
            var pv = ppick.querySelector('[data-ppick-value]');
            var pid = pv ? pv.value : '';
            if (pid) {
                var pitem = ppick.querySelector('.ppick-item[data-pid="' + pid + '"]');
                if (pitem) chips.push({ key: 'project', proj: true, color: pitem.getAttribute('data-color') || 'currentColor', text: pitem.getAttribute('data-label') || '' });
            }
        }

        // Fälligkeit
        var dueDate = val(form, 'dueDate');
        if (dueDate) chips.push({ key: 'due', icon: 'calendar', text: fmtDate(dueDate, val(form, 'dueTime')), cls: 'chip-due' });

        // Priorität (nur wenn != 4)
        var pr = form.querySelector('[data-f="priority"]:checked');
        if (pr && pr.value !== '4') chips.push({ key: 'prio', icon: 'flag', text: 'Priorität ' + pr.value, cls: 'pri-' + pr.value });

        // Erinnerung
        var rt = form.querySelector('[data-rem-type]');
        if (rt && rt.value !== '') chips.push({ key: 'reminder', icon: 'bell', text: 'Erinnerung', cls: '' });

        // Deadline
        var dl = val(form, 'deadlineDate');
        if (dl) chips.push({ key: 'deadline', icon: 'flag', text: 'Deadline ' + fmtDate(dl, val(form, 'deadlineTime')), cls: '' });

        // Etiketten
        form.querySelectorAll('[data-f="label"]:checked').forEach(function (cb) {
            chips.push({ key: 'label:' + cb.value, icon: 'tag', text: cb.getAttribute('data-name'), color: cb.getAttribute('data-color') });
        });

        wrap.innerHTML = chips.map(function (c) {
            var sw = c.proj
                ? "<span class='phash' style='color:" + c.color + "'>#</span>"
                : c.color
                    ? "<span class='tag-swatch' style='background:" + c.color + "'></span>"
                    : "<span class='" + (c.cls || '') + "'>" + icon(c.icon) + "</span>";
            return "<button type='button' class='ck " + (c.cls || '') + "' data-chip-key='" + c.key + "' title='Klicken zum Entfernen'>" + sw + " <span>" + escapeHtml(c.text) + "</span> " + icon('x') + "</button>";
        }).join('');

        // Toolbar-Buttons hervorheben, wenn Eigenschaft gesetzt
        setActive(form, 'due', !!dueDate);
        setActive(form, 'prio', !!(pr && pr.value !== '4'));
        setActive(form, 'reminder', !!(rt && rt.value !== ''));
        setActive(form, 'deadline', !!dl);
        setActive(form, 'labels', form.querySelectorAll('[data-f="label"]:checked').length > 0);

        // Aktive Schnellwahl markieren (erneutes Drücken entfernt sie)
        markQuickDue(form);
        markPriority(form);
    }

    // Markiert den aktiven Fälligkeits-Schnellchip (Heute/Morgen/Nächste Woche)
    function markQuickDue(form) {
        var due = val(form, 'dueDate');
        var t = todayStr();
        var map = { today: t, tomorrow: addDaysStr(t, 1), nextweek: addDaysStr(t, 7) };
        form.querySelectorAll('[data-quick-due]').forEach(function (chip) {
            var mode = chip.getAttribute('data-quick-due');
            chip.classList.toggle('active', mode !== 'clear' && !!due && due === map[mode]);
        });
    }

    // Markiert die aktive Priorität (P4 = keine)
    function markPriority(form) {
        form.querySelectorAll('[data-f="priority"]').forEach(function (radio) {
            var chip = radio.closest('.chip');
            if (chip) chip.classList.toggle('active', radio.checked && radio.value !== '4');
        });
    }

    function setActive(form, pop, on) {
        var btn = form.querySelector('.cbtn[data-pop="' + pop + '"]');
        if (btn) btn.classList.toggle('active', on);
    }

    function escapeHtml(s) { return (s || '').replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    function clearProp(form, key) {
        if (key === 'due') { setVal(form, 'dueDate', ''); setVal(form, 'dueTime', ''); }
        else if (key === 'prio') { var p4 = form.querySelector('[data-f="priority"][value="4"]'); if (p4) p4.checked = true; }
        else if (key === 'reminder') { var rt = form.querySelector('[data-rem-type]'); if (rt) { rt.value = ''; toggleReminder(form); } }
        else if (key === 'deadline') { setVal(form, 'deadlineDate', ''); setVal(form, 'deadlineTime', ''); }
        else if (key === 'project') {
            // Projekt entfernen -> Picker auf „Eingang" (kein Projekt) zurücksetzen
            var pp = form.querySelector('.ppick');
            var inbox = pp && pp.querySelector('.ppick-item[data-pid=""]');
            if (inbox) inbox.click();               // picker.js setzt Wert + feuert change
            else { var hv = pp && pp.querySelector('[data-ppick-value]'); if (hv) hv.value = ''; }
        }
        else if (key.indexOf('label:') === 0) {
            var id = key.slice(6);
            var cb = form.querySelector('[data-f="label"][value="' + id + '"]');
            if (cb) cb.checked = false;
        }
        refreshChips(form);
    }

    function setVal(form, f, v) {
        var el = form.querySelector('[data-f="' + f + '"]');
        if (el && el.value !== v) { el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); }
    }

    function toggleReminder(form) {
        var rt = form.querySelector('[data-rem-type]');
        if (!rt) return;
        var abs = form.querySelector('[data-rem-abs]');
        var rel = form.querySelector('[data-rem-rel]');
        if (abs) abs.style.display = rt.value === '0' ? 'flex' : 'none';
        if (rel) rel.style.display = rt.value === '1' ? 'block' : 'none';
    }

    function closeAllPopovers(except) {
        document.querySelectorAll('.popover.open').forEach(function (p) { if (p !== except) p.classList.remove('open'); });
    }

    // ---- Events ----
    document.addEventListener('click', function (e) {
        // Popover öffnen/schließen
        var btn = e.target.closest('.cbtn[data-pop]');
        if (btn) {
            e.preventDefault();
            var form = btn.closest('.composer');
            var panel = form.querySelector('.popover[data-pop-panel="' + btn.getAttribute('data-pop') + '"]');
            var wasOpen = panel.classList.contains('open');
            closeAllPopovers();
            if (!wasOpen) {
                panel.classList.add('open');
                // Inline-Kalender beim Öffnen auf das gewählte Datum / aktuellen Monat zurücksetzen.
                panel.querySelectorAll('.dp.dp-inline').forEach(function (w) { if (w._dpReset) w._dpReset(); });
            }
            return;
        }

        // Chip entfernen
        var chip = e.target.closest('.ck[data-chip-key]');
        if (chip) {
            e.preventDefault();
            clearProp(chip.closest('.composer'), chip.getAttribute('data-chip-key'));
            return;
        }

        // Priorität: Klick auf die bereits aktive Priorität setzt sie zurück (P4 = keine)
        var prioLabel = e.target.closest('.popover[data-pop-panel="prio"] label.chip');
        if (prioLabel) {
            e.preventDefault();
            var pform = prioLabel.closest('.composer');
            var radio = prioLabel.querySelector('input[data-f="priority"]');
            if (radio) {
                if (radio.checked && radio.value !== '4') {
                    var p4 = pform.querySelector('[data-f="priority"][value="4"]');
                    if (p4) p4.checked = true;
                } else {
                    radio.checked = true;
                }
            }
            refreshChips(pform);
            return;
        }

        // Schnellwahl Fälligkeit (erneutes Drücken derselben Option entfernt sie wieder)
        var q = e.target.closest('[data-quick-due]');
        if (q) {
            e.preventDefault();
            var form = q.closest('.composer');
            var mode = q.getAttribute('data-quick-due');
            if (mode === 'clear') { setVal(form, 'dueDate', ''); setVal(form, 'dueTime', ''); }
            else {
                var t = todayStr();
                var target = mode === 'today' ? t : mode === 'tomorrow' ? addDaysStr(t, 1) : mode === 'nextweek' ? addDaysStr(t, 7) : '';
                if (val(form, 'dueDate') === target) { setVal(form, 'dueDate', ''); setVal(form, 'dueTime', ''); }
                else setVal(form, 'dueDate', target);
            }
            refreshChips(form);
            return;
        }

        // Popover schließen
        if (e.target.closest('[data-pop-close]')) {
            e.preventDefault();
            closeAllPopovers();
            return;
        }

        // Klick außerhalb schließt Popover
        if (!e.target.closest('.popover') && !e.target.closest('.cbtn')) closeAllPopovers();
    });

    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeAllPopovers(); });

    // Eingaben in Popovern aktualisieren die Chips
    document.addEventListener('change', function (e) {
        var form = e.target.closest('.composer');
        if (!form) return;
        if (e.target.hasAttribute('data-rem-type')) toggleReminder(form);
        refreshChips(form);
    });

    // Abbrechen -> Formular zurücksetzen und Chips aktualisieren
    document.addEventListener('reset', function (e) {
        var form = e.target.closest('.composer');
        if (!form) return;
        setTimeout(function () { toggleReminder(form); refreshChips(form); closeAllPopovers(); }, 0);
    });

    // Initiale Chips (z.B. Fälligkeit „heute“ vorbelegt)
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.composer[data-composer]').forEach(function (form) {
            toggleReminder(form);
            refreshChips(form);
        });
    });
})();
