// ==UserScript==
// @name         Timecard → Nexus (Add Hours autofill)
// @namespace    jordan-isat-timecard
// @version      1.2
// @description  Fills the Nexus "Add Hours" form from the Timecard rocket (🚀): milestone, then Hours, Date and Description. Never presses Submit.
// @match        *://nexus.tcs.local/*
// @include      http*://nexus.tcs.local/*
// @run-at       document-start
// @noframes
// @grant        none
// ==/UserScript==
(function () {
  'use strict';

  var KEY = 'tcpush_pending';

  /* ---------------------------------------------------------------------
     1. Grab the payload as EARLY as possible (document-start).
        Nexus uses hash-based tabs of its own, so the hash may not survive
        page start-up; we stash it in sessionStorage immediately and work
        from there for the rest of the visit.
     ------------------------------------------------------------------ */
  function grabPayload() {
    var raw = null;
    var m = /[#&?]tcpush=([^&]+)/.exec(location.hash || '');
    if (!m) m = /[?&]tcpush=([^&]+)/.exec(location.search || '');
    if (m) {
      try { raw = decodeURIComponent(m[1]); }
      catch (e) { try { raw = decodeURIComponent(m[1].replace(/%(?![0-9a-f]{2})/gi, '%25')); } catch (e2) { raw = m[1]; } }
      try {
        sessionStorage.setItem(KEY, raw);
      } catch (e) { /* private mode — we still have it in memory below */ }
      // take the payload out of the URL so a refresh doesn't replay it
      try { history.replaceState(null, '', location.pathname + location.search); } catch (e) {}
    }
    if (!raw) { try { raw = sessionStorage.getItem(KEY); } catch (e) {} }
    if (!raw) return null;
    try { return JSON.parse(raw); } catch (e) { return { __bad: raw }; }
  }
  var P = grabPayload();
  var consumed = false;
  function clearPending() {
    consumed = true;
    try { sessionStorage.removeItem(KEY); } catch (e) {}
  }

  /* ---------------------------------------------------------------------
     2. Banner — ALWAYS shown, so you can see at a glance that the script
        is installed and running, even when there is nothing to fill.
     ------------------------------------------------------------------ */
  var bar = null;
  function ensureBar() {
    if (bar || !document.body) return bar;
    bar = document.createElement('div');
    bar.style.cssText = 'position:fixed;left:50%;top:14px;transform:translateX(-50%);z-index:2147483647;' +
      'background:#0b3d91;color:#fff;font:13px/1.45 Segoe UI,Arial;padding:10px 16px;border-radius:8px;' +
      'box-shadow:0 8px 26px rgba(0,0,0,.35);max-width:72vw;text-align:center';
    document.body.appendChild(bar);
    return bar;
  }
  function say(t, kind) {
    var b = ensureBar(); if (!b) return;
    b.innerHTML = (kind === 'err' ? '⚠ ' : '🚀 ') + t;
    b.style.background = kind === 'err' ? '#b91c1c' : kind === 'ok' ? '#166534' : '#0b3d91';
  }
  function hideBarIn(ms) { setTimeout(function () { if (bar) bar.remove(), bar = null; }, ms); }
  function done(t) { say(t, 'ok'); hideBarIn(12000); }
  function fail(t) { say(t + '<br><span style="opacity:.85">Finish by hand — nothing was submitted.</span>', 'err'); }

  function whenBody(fn) {
    if (document.body) return fn();
    var t = setInterval(function () { if (document.body) { clearInterval(t); fn(); } }, 30);
    document.addEventListener('DOMContentLoaded', function () { clearInterval(t); fn(); });
  }

  /* ---------------------------------------------------------------------
     3. Helpers
     ------------------------------------------------------------------ */
  function waitFor(fn, label, timeout, interval) {
    timeout = timeout || 45000; interval = interval || 250;
    return new Promise(function (resolve, reject) {
      var t0 = Date.now();
      (function tick() {
        var v = null;
        try { v = fn(); } catch (e) { v = null; }
        if (v) return resolve(v);
        if (Date.now() - t0 > timeout) return reject(new Error(label));
        setTimeout(tick, interval);
      })();
    });
  }
  function visible(el) { return !!el && el.offsetParent !== null && el.getBoundingClientRect().height > 0; }
  function seen(sel, root) { var e = (root || document).querySelector(sel); return visible(e) ? e : null; }
  function settle(ms) {
    ms = ms || 400;
    return new Promise(function (res) {
      var t0 = Date.now();
      (function tick() {
        var busy = (window.jQuery && window.jQuery.active > 0);
        if (!busy && Date.now() - t0 > ms) return res();
        if (Date.now() - t0 > 30000) return res();
        setTimeout(tick, 150);
      })();
    });
  }
  function setVal(el, val) {
    var proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, val);
    ['input', 'change', 'blur'].forEach(function (t) { el.dispatchEvent(new Event(t, { bubbles: true })); });
    if (window.jQuery) window.jQuery(el).val(val).trigger('input').trigger('change');
  }
  function clickIt(el) {
    try { el.scrollIntoView({ block: 'center' }); } catch (e) {}
    el.click();
    if (window.jQuery) window.jQuery(el).trigger('click');
  }
  function norm(s) { return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toUpperCase(); }

  var FORM_SEL = '#form-input_hours, #form-inputHours_forQuote';
  function liveForm() {
    var fs = document.querySelectorAll(FORM_SEL);
    for (var i = 0; i < fs.length; i++) if (visible(fs[i])) return fs[i];
    return null;
  }

  /* ---------------------------------------------------------------------
     4. Fill the Add Hours form (milestone first, then hours/date/notes).
        Used by the automated run AND by the watcher below, so the values
        land even if you navigated to the form yourself.
     ------------------------------------------------------------------ */
  function fillForm(f) {
    if (!f || !P || P.__bad) return false;
    var filledAny = false;

    if (P.milestone) {
      var sels = f.querySelectorAll('select');
      for (var s = 0; s < sels.length; s++) {
        var sel = sels[s], hit = null;
        for (var i = 0; i < sel.options.length; i++) {
          if (norm(sel.options[i].textContent) === norm(P.milestone)) { hit = sel.options[i]; break; }
        }
        if (!hit) for (var j = 0; j < sel.options.length; j++) {
          if (norm(sel.options[j].textContent).indexOf(norm(P.milestone)) >= 0) { hit = sel.options[j]; break; }
        }
        if (hit) {
          sel.value = hit.value;
          sel.dispatchEvent(new Event('change', { bubbles: true }));
          if (window.jQuery) window.jQuery(sel).val(hit.value).trigger('change');
          filledAny = true;
          break;
        }
      }
    }

    var f2 = liveForm() || f;                       // choosing a milestone can re-render the form
    var hoursIn = f2.querySelector('input[name="ts_hours"], input[name^="ts_hours"]');
    var dateIn  = f2.querySelector('input[name="ts_date"], input[name^="ts_date"]');
    var descIn  = f2.querySelector('textarea[name="ts_emp_description"], textarea[name^="ts_emp_description"]');

    // never overwrite something you typed yourself
    if (hoursIn && P.hours && !hoursIn.value) { setVal(hoursIn, P.hours); filledAny = true; }
    if (dateIn && P.date && !dateIn.value) {
      setVal(dateIn, P.date);
      try { dateIn.blur(); } catch (e) {}
      var hdr = document.querySelector('h4.modal-title, .modal-header');
      if (hdr) hdr.click();                         // dismiss the date-picker popup
      filledAny = true;
    }
    if (descIn && P.desc && !descIn.value) { setVal(descIn, P.desc); filledAny = true; }

    if (filledAny) {
      f2.style.outline = '3px solid #16a34a';
      f2.style.outlineOffset = '3px';
      try { f2.scrollIntoView({ block: 'center' }); } catch (e) {}
      var missing = [];
      if (!hoursIn) missing.push('Hours');
      if (!dateIn) missing.push('Date');
      if (!descIn) missing.push('Description');
      if (missing.length) fail('Filled what I could — could not find: ' + missing.join(', ') + '.');
      else done('Ready — <b>' + (P.milestone || '') + '</b> · ' + (P.hours || '0') + ' h · ' + (P.date || '') +
                '.<br><span style="opacity:.9">Check it and press <b>Submit</b> yourself.</span>');
      clearPending();
    }
    return filledAny;
  }

  /* ---------------------------------------------------------------------
     5. Safety net: whenever the Add Hours form shows up — however you got
        there — fill it. So even if the click-through below fails, doing it
        by hand still gets the values.
     ------------------------------------------------------------------ */
  function watchForForm() {
    var tryFill = function () {
      if (consumed || !P || P.__bad) return;
      var f = liveForm();
      if (f && !f.__tcFilled) { f.__tcFilled = true; setTimeout(function () { fillForm(f); }, 250); }
    };
    try {
      new MutationObserver(tryFill).observe(document.documentElement, { childList: true, subtree: true });
    } catch (e) {}
    setInterval(tryFill, 1000);
  }

  /* ---------------------------------------------------------------------
     6. The click-through
     ------------------------------------------------------------------ */
  function run() {
    return Promise.resolve().then(function () {

      say('Step 1/6 — searching task <b>' + P.task + '</b>…');
      return waitFor(function () { return seen('input.task_search'); }, 'the Task search box never appeared')

      .then(function (search) {
        if (norm(search.value) !== norm(P.task)) {
          setVal(search, P.task);
          ['keydown', 'keypress', 'keyup', 'search'].forEach(function (t) {
            search.dispatchEvent(new KeyboardEvent(t, { bubbles: true, key: 'Enter', keyCode: 13, which: 13 }));
          });
          if (window.jQuery) window.jQuery(search).trigger('keyup');
        }
        return settle(900);
      })

      .then(function () {
        say('Step 2/6 — opening the task…  <span style="opacity:.8">(Nexus can be slow)</span>');
        return waitFor(function () {
          var b = seen('button.btn-edit[data-row="' + P.task + '"]');
          if (b) return b;
          var all = document.querySelectorAll('button.btn-edit');
          return all.length === 1 && visible(all[0]) ? all[0] : null;
        }, 'the task did not show up in the list — check the task number');
      })

      .then(function (editBtn) { clickIt(editBtn); return settle(1200); })

      .then(function () {
        say('Step 3/6 — waiting for the task window…');
        return waitFor(function () {
          var m = document.querySelector('#EditProject');
          if (visible(m) && m.querySelector('a,button')) return m;
          var any = document.querySelectorAll('.modal.in, .modal.show, .modal[style*="display: block"]');
          for (var i = 0; i < any.length; i++) if (visible(any[i]) && any[i].querySelector('a,button')) return any[i];
          return null;
        }, 'the task window did not open');
      })

      .then(function (modal) {
        say('Step 4/6 — opening the <b>Hours</b> tab…');
        return waitFor(function () {
          var links = modal.querySelectorAll('a, button, li');
          for (var i = 0; i < links.length; i++)
            if (norm(links[i].textContent) === 'HOURS' && visible(links[i])) return links[i];
          return null;
        }, 'the Hours tab was not found').then(function (tab) {
          clickIt(tab);
          return settle(1200).then(function () { return modal; });
        });
      })

      .then(function (modal) {
        say('Step 5/6 — opening <b>Add Hours</b> on ' + (P.milestone || 'the milestone') + '…');
        var CLICKABLE = 'a, button, input[type=button], input[type=submit], [role=button], #ms_hours_modal';
        function addInRow(tr) {
          var c = tr.querySelectorAll(CLICKABLE), i;
          for (i = 0; i < c.length; i++) if (norm(c[i].textContent) === 'ADD HOURS' && visible(c[i])) return c[i];
          var tds = tr.querySelectorAll('td');
          for (i = 0; i < tds.length; i++) if (norm(tds[i].textContent) === 'ADD HOURS') {
            var inner = tds[i].querySelector(CLICKABLE);
            if (inner && visible(inner)) return inner;
            if (visible(tds[i])) return tds[i];
          }
          return null;
        }
        return waitFor(function () {
          var rows = modal.querySelectorAll('tr'), firstAdd = null;
          for (var i = 0; i < rows.length; i++) {
            var add = addInRow(rows[i]);
            if (!add) continue;
            if (!firstAdd) firstAdd = add;
            if (!P.milestone) continue;
            var first = rows[i].cells && rows[i].cells[0] ? norm(rows[i].cells[0].textContent) : '';
            if (first === norm(P.milestone) || norm(rows[i].textContent).indexOf(norm(P.milestone)) === 0) return add;
          }
          return firstAdd;
        }, 'no “Add Hours” button was found on the Hours tab');
      })

      .then(function (addBtn) { clickIt(addBtn); return settle(1200); })

      .then(function () {
        say('Step 6/6 — filling the form…');
        return waitFor(function () { return liveForm(); }, 'the Add Hours form did not open');
      })

      .then(function (form) {
        if (!consumed) fillForm(form);
      });
    })
    .catch(function (err) {
      fail('Stopped at: ' + (err && err.message ? err.message : err) +
           '<br><span style="opacity:.85">Open Add&nbsp;Hours yourself — I will still fill it in.</span>');
    });
  }

  /* ---------------------------------------------------------------------
     7. Go
     ------------------------------------------------------------------ */
  whenBody(function () {
    watchForForm();
    if (!P) {                                  // nothing to do — just prove we are alive
      say('Timecard → Nexus is active. Use the 🚀 in the Timecard to fill an Add-Hours form.');
      hideBarIn(4000);
      return;
    }
    if (P.__bad) {
      fail('The Timecard sent something I could not read: ' + String(P.__bad).slice(0, 120));
      return;
    }
    say('Timecard → Nexus starting…');
    // let Nexus finish its own start-up first; it is slow
    if (document.readyState === 'complete') setTimeout(run, 600);
    else window.addEventListener('load', function () { setTimeout(run, 600); });
  });
})();
