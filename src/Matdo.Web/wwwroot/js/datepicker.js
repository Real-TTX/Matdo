// Matdo – eigener Kalender-Datepicker (ersetzt native input[type=date]/[type=time]).
// Zwei Modi:
//   - inline  (data-dp-inline)  : Kalender direkt sichtbar (Composer-Popover)
//   - popup   (Standard)        : Trigger-Button öffnet den Kalender als Overlay (Edit-Formular)
// Fortschrittliche Anreicherung: das native <input> bleibt als Wertträger erhalten (Name,
// Wert, change-Event), wird aber visuell versteckt. Ein optionales Partner-Zeitfeld
// (…Date -> …Time) wird im Kalender-Fuß als Auswahl gesteuert.
(function () {
    'use strict';

    var LANG = (document.documentElement.getAttribute('lang') || 'de').slice(0, 2).toLowerCase();
    var I18N = {
        de: {
            months: ['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober', 'November', 'Dezember'],
            mon: ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez'],
            days: ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'],
            today: 'Heute', tomorrow: 'Morgen', dayAfter: 'Übermorgen', nextWeek: 'Nächste Woche', weekend: 'Wochenende', noDate: 'Kein Datum', time: 'Uhrzeit', pick: 'Datum wählen'
        },
        en: {
            months: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
            mon: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
            days: ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'],
            today: 'Today', tomorrow: 'Tomorrow', dayAfter: 'Day after', nextWeek: 'Next week', weekend: 'Weekend', noDate: 'No date', time: 'Time', pick: 'Pick a date'
        }
    };
    var L = I18N[LANG] || I18N.de;

    function pad(n) { return (n < 10 ? '0' : '') + n; }
    function iso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
    function parseISO(s) { var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s || ''); return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null; }
    function today() { var d = new Date(); return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
    function addDays(d, n) { var x = new Date(d); x.setDate(x.getDate() + n); return x; }
    function same(a, b) { return a && b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate(); }
    function esc(s) { return (s || '').replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    function svg(inner) { return "<svg width='15' height='15' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" + inner + "</svg>"; }
    var IC = {
        cal: svg("<rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/>"),
        clock: svg("<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>"),
        x: svg("<path d='M18 6 6 18M6 6l12 12'/>"),
        // Schnellauswahl-Icons
        sun: svg("<circle cx='12' cy='12' r='4'/><path d='M12 3v1.5M12 19.5V21M4.6 4.6l1 1M18.4 18.4l1 1M3 12h1.5M19.5 12H21M4.6 19.4l1-1M18.4 5.6l1-1'/>"),        // Heute
        sunrise: svg("<path d='M12 3v6'/><path d='m8 7 4-4 4 4'/><path d='M3 20h18'/><path d='M5.6 16a6.5 6.5 0 0 1 12.8 0'/>"),                                       // Morgen
        chevrons: svg("<path d='m6 17 5-5-5-5'/><path d='m13 17 5-5-5-5'/>"),                                                                                        // Übermorgen
        sofa: svg("<path d='M20 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v3'/><path d='M2 12a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z'/><path d='M4 18v2M20 18v2M12 5v7'/>"), // Wochenende
        arrow: svg("<path d='M5 12h14'/><path d='m13 5 7 7-7 7'/>")                                                                                                  // Nächste Woche
    };

    // Partner-Zeitfeld zu einem Datumsfeld (…Date -> …Time) im selben Formular.
    function partnerTime(input) {
        if ('_dpTime' in input) return input._dpTime;
        var t = null, nm = input.getAttribute('name');
        if (nm && /Date$/.test(nm)) {
            var target = nm.replace(/Date$/, 'Time');
            var scope = input.form || document;
            var list = scope.querySelectorAll('input[name="' + target.replace(/"/g, '\\"') + '"]');
            if (list.length) t = list[0];
        }
        input._dpTime = t;
        return t;
    }

    function fmtDisplay(input) {
        var d = parseISO(input.value);
        if (!d) return '';
        var t = today(), label;
        if (same(d, t)) label = L.today;
        else if (same(d, addDays(t, 1))) label = L.tomorrow;
        else if (same(d, addDays(t, -1))) label = LANG === 'en' ? 'Yesterday' : 'Gestern';
        else label = LANG === 'en' ? (L.mon[d.getMonth()] + ' ' + d.getDate()) : (d.getDate() + '. ' + L.mon[d.getMonth()]);
        if (d.getFullYear() !== t.getFullYear()) label += ' ' + d.getFullYear();
        var ti = partnerTime(input);
        if (ti && ti.value) label += ', ' + ti.value;
        return label;
    }

    function timeOptions(cur) {
        var opts = '', found = false;
        for (var h = 0; h < 24; h++) for (var m = 0; m < 60; m += 30) {
            var v = pad(h) + ':' + pad(m);
            if (v === cur) found = true;
            opts += "<option value='" + v + "'" + (v === cur ? ' selected' : '') + '>' + v + '</option>';
        }
        if (cur && !found) opts = "<option value='" + esc(cur) + "' selected>" + esc(cur) + '</option>' + opts;
        return opts;
    }

    // Zeichnet den Kalender in den Container (c._dpInput = zugehöriges Datumsfeld).
    function paint(c) {
        var input = c._dpInput, t = today(), sel = parseISO(input.value);
        if (c._y == null) { var base = sel || t; c._y = base.getFullYear(); c._m = base.getMonth(); }

        // Nächstes Vorkommen eines Wochentags (0=So..6=Sa); heute -> +7.
        function nextOf(dow) { var diff = (dow - t.getDay() + 7) % 7; if (diff === 0) diff = 7; return addDays(t, diff); }
        var quick = [
            { l: L.today, d: t, ic: IC.sun },
            { l: L.tomorrow, d: addDays(t, 1), ic: IC.sunrise },
            { l: L.dayAfter, d: addDays(t, 2), ic: IC.chevrons },
            { l: L.weekend, d: nextOf(6), ic: IC.sofa },        // nächster Samstag
            { l: L.nextWeek, d: nextOf(1), ic: IC.arrow }        // nächster Montag
        ];
        var h = "<div class='dp-quick'>" + quick.map(function (o) {
            var wd = L.days[(o.d.getDay() + 6) % 7];             // Wochentags-Kürzel (Mo…So)
            return "<button type='button' class='dp-q' data-date='" + iso(o.d) + "'>"
                + o.ic + "<span class='dp-q-lbl'>" + o.l + "</span><span class='dp-q-day'>" + wd + "</span></button>";
        }).join('') + "</div>";
        h += "<div class='dp-head'><button type='button' class='dp-nav' data-nav='-1' aria-label='‹'>‹</button>"
            + "<span class='dp-title'>" + L.months[c._m] + " " + c._y + "</span>"
            + "<button type='button' class='dp-nav' data-nav='1' aria-label='›'>›</button></div>";
        h += "<div class='dp-dow'>" + L.days.map(function (d) { return "<span>" + d + "</span>"; }).join('') + "</div>";

        var first = new Date(c._y, c._m, 1);
        var startDow = (first.getDay() + 6) % 7;          // Montag = 0
        var count = new Date(c._y, c._m + 1, 0).getDate();
        var cells = '';
        for (var i = 0; i < startDow; i++) cells += "<span class='dp-cell dp-empty'></span>";
        for (var day = 1; day <= count; day++) {
            var d = new Date(c._y, c._m, day), cls = 'dp-cell dp-day';
            if (same(d, t)) cls += ' dp-today';
            if (sel && same(d, sel)) cls += ' dp-sel';
            cells += "<button type='button' class='" + cls + "' data-day='" + day + "'>" + day + "</button>";
        }
        h += "<div class='dp-days'>" + cells + "</div>";

        h += "<div class='dp-foot'>";
        h += "<button type='button' class='dp-nodate'>" + IC.x + " " + L.noDate + "</button>";
        // Uhrzeit rechts unten (Todoist-artig).
        var ti = partnerTime(input);
        if (ti) h += "<label class='dp-time'>" + IC.clock + "<select class='dp-time-sel'><option value=''>" + L.time + "</option>" + timeOptions(ti.value) + "</select></label>";
        h += "</div>";

        c.innerHTML = h;
    }

    function setNative(el, v) { if (el.value !== v) { el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); } }
    function setDate(input, d) { setNative(input, iso(d)); }
    function clearVal(input) { setNative(input, ''); var ti = partnerTime(input); if (ti) setNative(ti, ''); }

    // Klicks/Änderungen im Kalender-Container behandeln. isPopup -> nach Auswahl schließen.
    function wire(c, isPopup) {
        c.addEventListener('click', function (e) {
            // stopPropagation: der Klick darf NICHT den Außenklick-Handler des Composers erreichen –
            // paint() ersetzt innerHTML, wodurch das geklickte Element „detached" wird und der
            // Composer sonst das Popover schließen würde.
            var nav = e.target.closest('[data-nav]');
            if (nav) { e.preventDefault(); e.stopPropagation(); c._m += (+nav.getAttribute('data-nav')); if (c._m < 0) { c._m = 11; c._y--; } else if (c._m > 11) { c._m = 0; c._y++; } paint(c); return; }
            var quick = e.target.closest('[data-date]');
            if (quick) { e.preventDefault(); e.stopPropagation(); setDate(c._dpInput, parseISO(quick.getAttribute('data-date'))); if (isPopup) closePop(); else paint(c); return; }
            var day = e.target.closest('[data-day]');
            if (day) { e.preventDefault(); e.stopPropagation(); setDate(c._dpInput, new Date(c._y, c._m, +day.getAttribute('data-day'))); if (isPopup) closePop(); else paint(c); return; }
            if (e.target.closest('.dp-nodate')) { e.preventDefault(); e.stopPropagation(); clearVal(c._dpInput); if (isPopup) closePop(); else paint(c); return; }
        });
        c.addEventListener('change', function (e) {
            var ts = e.target.closest('.dp-time-sel');
            if (ts) { var ti = partnerTime(c._dpInput); if (ti) setNative(ti, ts.value); if (!isPopup) { /* Auswahl behalten */ } }
        });
    }

    // ---------- Popup-Modus ----------
    var pop = null, popAnchor = null, onDoc, onKey, onScroll;

    function closePop() {
        if (onDoc) document.removeEventListener('click', onDoc);
        if (onKey) document.removeEventListener('keydown', onKey, true);
        if (onScroll) document.removeEventListener('scroll', onScroll, true);
        onDoc = onKey = onScroll = null;
        if (pop) { pop.remove(); pop = null; }
        popAnchor = null;
    }

    function openPop(input, anchor) {
        closePop();
        pop = document.createElement('div');
        pop.className = 'dp-pop';
        pop._dpInput = input; pop._y = null; pop._m = null;
        document.body.appendChild(pop);
        wire(pop, true);
        paint(pop);
        position(pop, anchor);
        popAnchor = anchor;
        onDoc = function (e) { if (pop && !e.target.closest('.dp-pop') && e.target !== anchor && !anchor.contains(e.target)) closePop(); };
        onKey = function (e) { if (e.key === 'Escape') { e.stopImmediatePropagation(); closePop(); } };
        onScroll = function (e) { if (pop && (!e.target.closest || !e.target.closest('.dp-pop'))) closePop(); };
        setTimeout(function () { document.addEventListener('click', onDoc); }, 0);
        document.addEventListener('keydown', onKey, true);
        document.addEventListener('scroll', onScroll, true);
    }

    function position(el, anchor) {
        var r = anchor.getBoundingClientRect();
        el.style.visibility = 'hidden';
        var w = el.offsetWidth, hgt = el.offsetHeight;
        var vw = document.documentElement.clientWidth, vh = document.documentElement.clientHeight;
        var left = r.left + window.scrollX, top = r.bottom + window.scrollY + 4;
        if (left + w > window.scrollX + vw - 8) left = window.scrollX + vw - w - 8;
        if (left < window.scrollX + 8) left = window.scrollX + 8;
        if (r.bottom + hgt + 8 > vh && r.top - hgt - 4 > 0) top = r.top + window.scrollY - hgt - 4;
        el.style.left = left + 'px'; el.style.top = top + 'px'; el.style.visibility = '';
    }

    // ---------- Anreicherung ----------
    function hideNative(el) { el.classList.add('dp-native'); }
    function hidePartner(ti) {
        hideNative(ti); ti.dataset.dpTimeDone = '1';
        var ff = ti.closest('.form-field');
        if (ff && ff.querySelectorAll('input,select,textarea').length === 1) ff.style.display = 'none';
    }

    function enhance(input) {
        if (input.dataset.dpDone) return;
        input.dataset.dpDone = '1';

        var ti = partnerTime(input);
        if (ti) hidePartner(ti);

        var inline = input.hasAttribute('data-dp-inline');
        var wrap = document.createElement(inline ? 'div' : 'span');
        wrap.className = inline ? 'dp dp-inline' : 'dp';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
        hideNative(input);

        if (inline) {
            wrap._dpInput = input; wrap._y = null; wrap._m = null;
            wire(wrap, false);
            paint(wrap);
            // Ansicht auf das gewählte Datum / den aktuellen Monat zurücksetzen (z.B. beim Wieder-Öffnen).
            wrap._dpReset = function () { wrap._y = null; wrap._m = null; paint(wrap); };
            input.addEventListener('change', function () { wrap._dpReset(); });
            if (ti) ti.addEventListener('change', function () { paint(wrap); });
        } else {
            var btn = document.createElement('button');
            btn.type = 'button'; btn.className = 'dp-trigger';
            wrap.appendChild(btn);
            input._dpBtn = btn;
            renderTrigger(input);
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                if (e.target.closest('.dp-clear')) { clearVal(input); return; }
                if (pop && popAnchor === btn) { closePop(); return; }
                openPop(input, btn);
            });
            input.addEventListener('change', function () { renderTrigger(input); });
            if (ti) ti.addEventListener('change', function () { renderTrigger(input); });
        }
    }

    function renderTrigger(input) {
        var btn = input._dpBtn; if (!btn) return;
        var disp = fmtDisplay(input);
        btn.innerHTML = IC.cal + "<span class='dp-tval" + (disp ? '' : ' dp-ph') + "'>" + esc(disp || L.pick) + "</span>"
            + (disp ? "<span class='dp-clear' role='button' aria-label='" + esc(L.noDate) + "'>" + IC.x + "</span>" : "");
    }

    function scan(root) {
        var list = (root.querySelectorAll ? root.querySelectorAll('input[type="date"]:not([data-dp-done])') : []);
        for (var i = 0; i < list.length; i++) enhance(list[i]);
    }

    function boot() { scan(document); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();

    // Nach „Abbrechen" (form.reset) den Kalender/Trigger mit den zurückgesetzten Werten neu zeichnen.
    document.addEventListener('reset', function (e) {
        var form = e.target;
        if (!form || !form.querySelectorAll) return;
        setTimeout(function () {
            form.querySelectorAll('.dp').forEach(function (w) {
                var inp = w.querySelector('input[data-dp-done]');
                if (!inp) return;
                if (w.classList.contains('dp-inline')) { w._y = null; w._m = null; paint(w); }
                else renderTrigger(inp);
            });
        }, 0);
    });

    // Dynamisch nachgeladene Inhalte (z.B. Bearbeiten-Modal) ebenfalls anreichern.
    new MutationObserver(function (muts) {
        for (var i = 0; i < muts.length; i++) {
            var added = muts[i].addedNodes;
            for (var j = 0; j < added.length; j++) {
                var n = added[j];
                if (n.nodeType !== 1) continue;
                if (n.matches && n.matches('input[type="date"]')) enhance(n);
                scan(n);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true });
})();
