// ==UserScript==
// @name         Timecard → Nexus (Add Hours autofill)
// @namespace    jordan-isat-timecard
// @version      1.3
// @description  Fills the Nexus Hours tab from the Timecard rocket (🚀): milestone, then Hours, Date and Description. Never presses Submit.
// @match        *://nexus.tcs.local/*
// @include      http*://nexus.tcs.local/*
// @run-at       document-start
// @noframes
// @grant        none
// ==/UserScript==
(function () {
  'use strict';

  var KEY = 'tcpush_pending';

  /* ---------- payload, captured as early as possible ---------- */
  function grabPayload() {
    var raw = null;
    var m = /[#&?]tcpush=([^&]+)/.exec(location.hash || '');
    if (!m) m = /[?&]tcpush=([^&]+)/.exec(location.search || '');
    if (m) {
      try { raw = decodeURIComponent(m[1]); }
      catch (e) { try { raw = decodeURIComponent(m[1].replace(/%(?![0-9a-f]{2})/gi, '%25')); } catch (e2) { raw = m[1]; } }
      try { sessionStorage.setItem(KEY, raw); } catch (e) {}
      try { history.replaceState(null, '', location.pathname + location.search); } catch (e) {}
    }
    if (!raw) { try { raw = sessionStorage.getItem(KEY); } catch (e) {} }
    if (!raw) return null;
    try { return JSON.parse(raw); } catch (e) { return { __bad: raw }; }
  }
  var P = grabPayload();
  var consumed = false;
  function clearPending() { consumed = true; try { sessionStorage.removeItem(KEY); } catch (e) {} }

  /* ---------- banner (always shown, so you can see it is alive) ---------- */
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
  function hideBarIn(ms) { setTimeout(function () { if (bar) { bar.remove(); bar = null; } }, ms); }
  function done(t) { say(t, 'ok'); hideBarIn(12000); }
  function fail(t) { say(t + '<br><span style="opacity:.85">Finish by hand — nothing was submitted.</span>', 'err'); }
  function whenBody(fn) {
    if (document.body) return fn();
    var t = setInterval(function () { if (document.body) { clearInterval(t); fn(); } }, 30);
    document.addEventListener('DOMContentLoaded', function () { clearInterval(t); fn(); });
  }

  /* ---------- helpers ---------- */
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
  // ONE realistic click. Nexus's Edit handler fires an AJAX load, so clicking twice
  // (native + jQuery.trigger) starts two loads and can leave the modal half-built.
  function clickIt(el) {
    try { el.scrollIntoView({ block: 'center' }); } catch (e) {}
    ['pointerdown', 'mousedown', 'mouseup', 'click'].forEach(function (type) {
      var ev;
      try { ev = new MouseEvent(type, { bubbles: true, cancelable: true, view: window }); }
      catch (e) { ev = document.createEvent('MouseEvents'); ev.initEvent(type, true, true); }
      el.dispatchEvent(ev);
    });
  }
  function setVal(el, val) {
    var proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, val);
    ['input', 'change', 'blur'].forEach(function (t) { el.dispatchEvent(new Event(t, { bubbles: true })); });
    if (window.jQuery) window.jQuery(el).val(val).trigger('input').trigger('change');
  }
  function norm(s) { return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toUpperCase(); }

  var FORM_SEL = '#form-input_hours';
  function liveForm() {
    var fs = document.querySelectorAll(FORM_SEL + ', #form-inputHours_forQuote');
    for (var i = 0; i < fs.length; i++) if (visible(fs[i])) return fs[i];
    return null;
  }

  /* ---------- fill the Hours form ---------- */
  function fillForm(f) {
    if (!f || !P || P.__bad || consumed) return false;
    var touched = false;

    // 1. Milestone FIRST — value attribute is the milestone name, and the chosen
    //    option carries data-ms-id which the hidden ts_ms_id field needs.
    if (P.milestone) {
      var sel = f.querySelector('select[name="ms_select"]') || f.querySelector('select');
      if (sel) {
        var hit = null, i;
        for (i = 0; i < sel.options.length; i++)
          if (norm(sel.options[i].value) === norm(P.milestone) || norm(sel.options[i].textContent) === norm(P.milestone)) { hit = sel.options[i]; break; }
        if (!hit) for (i = 0; i < sel.options.length; i++)
          if (norm(sel.options[i].textContent).indexOf(norm(P.milestone)) >= 0) { hit = sel.options[i]; break; }
        if (hit) {
          sel.value = hit.value;
          sel.dispatchEvent(new Event('change', { bubbles: true }));
          if (window.jQuery) window.jQuery(sel).val(hit.value).trigger('change');
          // nothing in Nexus copies data-ms-id across, so do it ourselves
          var msId = hit.getAttribute('data-ms-id');
          var msHidden = f.querySelector('input[name="ts_ms_id"]');
          if (msId && msHidden) msHidden.value = msId;
          touched = true;
        }
      }
    }

    var f2 = liveForm() || f;
    var hoursIn = f2.querySelector('input[name="ts_hours"]');
    var dateIn  = f2.querySelector('input[name="ts_date"]');
    var descIn  = f2.querySelector('textarea[name="ts_emp_description"], #ts_emp_description');

    if (hoursIn && P.hours && !hoursIn.value) { setVal(hoursIn, P.hours); touched = true; }
    if (dateIn && P.date && !dateIn.value) {
      setVal(dateIn, P.date);
      try { dateIn.blur(); } catch (e) {}
      // close the jQuery UI datepicker it pops open
      try { if (window.jQuery && window.jQuery(dateIn).datepicker) window.jQuery(dateIn).datepicker('hide'); } catch (e) {}
      var dp = document.getElementById('ui-datepicker-div');
      if (dp) dp.style.display = 'none';
      touched = true;
    }
    if (descIn && P.desc && !descIn.value) { setVal(descIn, P.desc); touched = true; }

    if (touched) {
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
    return touched;
  }

  // Safety net: fill the form whenever it appears, however you got there.
  function watchForForm() {
    var tryFill = function () {
      if (consumed || !P || P.__bad) return;
      var f = liveForm();
      if (f && !f.__tcFilled) { f.__tcFilled = true; setTimeout(function () { fillForm(f); }, 300); }
    };
    try { new MutationObserver(tryFill).observe(document.documentElement, { childList: true, subtree: true }); } catch (e) {}
    setInterval(tryFill, 1000);
  }

  /* ---------- find the row's Edit button ---------- */
  function findEditButton() {
    var t = String(P.task);
    var b = document.querySelector('button.btn-edit[data-row="' + t + '"]');
    if (visible(b)) return b;
    // any element carrying the row id
    var any = document.querySelectorAll('[data-row="' + t + '"]');
    for (var i = 0; i < any.length; i++) {
      if (any[i].tagName === 'BUTTON' && visible(any[i])) return any[i];
      var inner = any[i].querySelector && any[i].querySelector('button.btn-edit, button');
      if (inner && visible(inner)) return inner;
    }
    // the row that shows this task number -> its Edit button
    var rows = document.querySelectorAll('#table1_body tr, #table2_body tr, tbody tr');
    for (var r = 0; r < rows.length; r++) {
      if (norm(rows[r].textContent).indexOf(t) < 0) continue;
      var eb = rows[r].querySelector('button.btn-edit');
      if (visible(eb)) return eb;
      var td3 = rows[r].cells && rows[r].cells[2] ? rows[r].cells[2].querySelector('button') : null;
      if (visible(td3)) return td3;
    }
    // last resort: exactly one Edit button left after filtering
    var all = [], nodes = document.querySelectorAll('button.btn-edit');
    for (var k = 0; k < nodes.length; k++) if (visible(nodes[k])) all.push(nodes[k]);
    return all.length === 1 ? all[0] : null;
  }

  /* ---------- the flow ---------- */
  function run() {
    return Promise.resolve()
      .then(function () {
        say('Step 1/4 — searching task <b>' + P.task + '</b>…');
        return waitFor(function () { return seen('input.task_search'); }, 'the Task search box never appeared');
      })
      .then(function (search) {
        if (norm(search.value) !== norm(P.task)) {
          setVal(search, P.task);
          ['keydown', 'keypress', 'keyup', 'search'].forEach(function (t) {
            search.dispatchEvent(new KeyboardEvent(t, { bubbles: true, key: 'Enter', keyCode: 13, which: 13 }));
          });
          if (window.jQuery) window.jQuery(search).trigger('keyup');
        }
        return settle(1000);
      })
      .then(function () {
        say('Step 2/4 — opening the task…  <span style="opacity:.8">(Nexus can be slow)</span>');
        return waitFor(findEditButton, 'the task row / its Edit button was not found — check the task number');
      })
      .then(function (btn) { clickIt(btn); return settle(1500); })
      .then(function () {
        say('Step 3/4 — opening the <b>Hours</b> tab…');
        // the Edit window is AJAX-loaded into #EditProject
        return waitFor(function () {
          var m = document.querySelector('#EditProject');
          if (visible(m) && m.querySelector('.nav-tabs')) return m;
          var mods = document.querySelectorAll('.modal.in, .modal.show, .modal[style*="display: block"]');
          for (var i = 0; i < mods.length; i++) if (visible(mods[i]) && mods[i].querySelector('.nav-tabs')) return mods[i];
          return null;
        }, 'the task window did not open');
      })
      .then(function (modal) {
        // Hours is the 2nd tab: <a data-target="#second" data-toggle="tab">Hours</a>
        return waitFor(function () {
          var a = modal.querySelector('a[data-target="#second"]');
          if (visible(a)) return a;
          var links = modal.querySelectorAll('.nav-tabs a, a[data-toggle="tab"]');
          for (var i = 0; i < links.length; i++)
            if (norm(links[i].textContent) === 'HOURS' && visible(links[i])) return links[i];
          return null;
        }, 'the Hours tab was not found').then(function (tab) {
          clickIt(tab);
          if (window.jQuery) { try { window.jQuery(tab).tab('show'); } catch (e) {} }
          return settle(900);
        });
      })
      .then(function () {
        say('Step 4/4 — filling the form…');
        // the Hours tab already CONTAINS #form-input_hours — no "Add Hours" click needed
        return waitFor(liveForm, 'the Hours form did not appear');
      })
      .then(function (form) { if (!consumed) fillForm(form); })
      .catch(function (err) {
        fail('Stopped at: ' + (err && err.message ? err.message : err) +
             '<br><span style="opacity:.85">Open the Hours tab yourself — I will still fill it in.</span>');
      });
  }

  whenBody(function () {
    watchForForm();
    if (!P) {
      say('Timecard → Nexus is active. Use the 🚀 in the Timecard to fill an Add-Hours form.');
      hideBarIn(4000);
      return;
    }
    if (P.__bad) { fail('The Timecard sent something I could not read: ' + String(P.__bad).slice(0, 120)); return; }
    say('Timecard → Nexus starting…');
    if (document.readyState === 'complete') setTimeout(run, 800);
    else window.addEventListener('load', function () { setTimeout(run, 800); });
  });
})();
