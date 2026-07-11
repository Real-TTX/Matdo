// Matdo – Smart-Eingabe: #Projekt, +Etikett, @Person Autocomplete in Textfeldern mit [data-smart]
(function () {
    'use strict';

    var TRIGGERS = { '#': 'project', '+': 'label', '@': 'person' };
    var box = null, activeInput = null, items = [], sel = -1, tokenStart = -1, tokenEnd = -1, curType = null, seq = 0;

    function escapeHtml(s) {
        return (s || '').replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
    }

    function closeBox() {
        seq++; // laufende Suggest-Antworten entwerten (kein Geister-Dropdown)
        if (box) { box.remove(); box = null; }
        activeInput = null; items = []; sel = -1; tokenStart = -1; tokenEnd = -1; curType = null;
    }

    function findToken(input) {
        var pos = input.selectionStart;
        var text = input.value.slice(0, pos);
        var m = text.match(/(^|\s)([#+@])([^\s#+@]*)$/);
        if (!m) return null;
        var query = m[3];
        return { trigger: m[2], type: TRIGGERS[m[2]], query: query, start: pos - query.length - 1, end: pos };
    }

    async function update(input) {
        var tok = findToken(input);
        if (!tok) { closeBox(); return; }
        curType = tok.type; tokenStart = tok.start; tokenEnd = tok.end; activeInput = input;
        var mySeq = ++seq;
        try {
            var resp = await fetch('/api/suggest?type=' + tok.type + '&q=' + encodeURIComponent(tok.query));
            if (mySeq !== seq) return;              // veraltete Antwort verwerfen
            if (!resp.ok) { closeBox(); return; }
            items = await resp.json();
        } catch (e) { closeBox(); return; }
        if (!items.length) { closeBox(); return; }
        render(input, tok);
    }

    function render(input, tok) {
        if (!box) { box = document.createElement('div'); box.className = 'ac-box'; document.body.appendChild(box); }
        sel = 0;
        box.innerHTML = items.map(function (it, i) {
            var sw;
            if (tok.trigger === '#') sw = "<span class='ac-hash' style='color:" + (it.color || 'currentColor') + "'>#</span>";   // Projekt: farbiges #
            else if (it.color) sw = "<span class='ac-dot' style='background:" + it.color + "'></span>";                            // Etikett: farbiger Punkt
            else sw = "<span class='ac-at'>" + tok.trigger + "</span>";                                                            // Person: @
            return "<div class='ac-item" + (i === 0 ? ' active' : '') + "' data-i='" + i + "'>" + sw + "<span>" + escapeHtml(it.name) + "</span></div>";
        }).join('');
        var r = input.getBoundingClientRect();
        box.style.left = (r.left + window.scrollX) + 'px';
        box.style.top = (r.bottom + window.scrollY + 3) + 'px';
        box.style.minWidth = Math.max(200, r.width * 0.6) + 'px';
    }

    // Übernimmt den Treffer als echte Auswahl im Formular (Projekt-Picker / Label-Checkbox /
    // Zuweisungs-Select) – wie eine manuelle Auswahl. Gibt true zurück, wenn angewendet.
    function applyMatch(form, type, it) {
        if (!form) return false;
        if (type === 'project') {
            var item = form.querySelector('.ppick-item[data-pid="' + it.id + '"]');
            if (item) { item.click(); return true; }   // picker.js setzt Wert + Button-Label
            return false;
        }
        if (type === 'label') {
            var cb = form.querySelector('input[name$="LabelIds"][value="' + it.id + '"]');
            if (cb) {
                if (!cb.checked) { cb.checked = true; cb.dispatchEvent(new Event('change', { bubbles: true })); }
                return true;
            }
            return false;
        }
        if (type === 'person') {
            var sel = form.querySelector('select[name$="AssigneeId"]');
            if (sel && sel.querySelector('option[value="' + it.id + '"]')) {
                sel.value = String(it.id);
                sel.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            }
            return false;
        }
        return false;
    }

    // Entfernt das getippte Token (#…/+…/@…) aus dem Textfeld. Start und Ende werden zum
    // Suggest-Zeitpunkt festgehalten – so kann eine Cursor-Bewegung (Pfeiltasten) den Titel
    // nicht verstümmeln.
    function removeToken(input, start, end) {
        var before = input.value.slice(0, start);
        var after = input.value.slice(end);
        if (/\s$/.test(before) && /^\s/.test(after)) after = after.replace(/^\s+/, '');
        input.value = before + after;
        var pos = before.length;
        try { input.setSelectionRange(pos, pos); } catch (e) { }
    }

    function choose(i) {
        if (i < 0 || i >= items.length || !activeInput) return;
        var it = items[i], input = activeInput, type = curType, start = tokenStart, end = tokenEnd;
        var trigger = Object.keys(TRIGGERS).filter(function (k) { return TRIGGERS[k] === type; })[0];
        var form = input.closest('form');

        if (applyMatch(form, type, it)) {
            // Als Chip/Badge übernommen -> Text-Token verschwindet
            removeToken(input, start, end);
        } else {
            // Fallback: Token als Text einfügen (wird beim Speichern serverseitig geparst)
            var before = input.value.slice(0, start);
            var after = input.value.slice(end);
            var insert = trigger + it.name + ' ';
            input.value = before + insert + after;
            var pos = (before + insert).length;
            try { input.setSelectionRange(pos, pos); } catch (e) { }
        }
        closeBox();
        input.focus();
    }

    function highlight() {
        if (!box) return;
        box.querySelectorAll('.ac-item').forEach(function (el, i) { el.classList.toggle('active', i === sel); });
    }

    document.addEventListener('input', function (e) {
        var input = e.target.closest('[data-smart]');
        if (input) update(input);
    });

    document.addEventListener('keydown', function (e) {
        if (!box) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); sel = Math.min(sel + 1, items.length - 1); highlight(); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(sel - 1, 0); highlight(); }
        else if (e.key === 'Enter' || e.key === 'Tab') { if (sel >= 0) { e.preventDefault(); choose(sel); } }
        else if (e.key === 'Escape') { e.stopImmediatePropagation(); closeBox(); }
    });

    document.addEventListener('click', function (e) {
        var it = e.target.closest('.ac-item');
        if (it && box) { choose(parseInt(it.getAttribute('data-i'), 10)); return; }
        if (box && !e.target.closest('.ac-box')) closeBox();
    });

    document.addEventListener('scroll', function () { if (box) closeBox(); }, true);
})();
