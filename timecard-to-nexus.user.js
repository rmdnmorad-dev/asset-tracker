// ==UserScript==
// @name         Timecard → Nexus (Add Hours autofill)
// @namespace    jordan-isat-timecard
// @version      1.5
// @description  Opens the Nexus task window straight from the Timecard rocket (🚀) and fills the Hours tab: milestone, Hours, Date, Description. Never presses Submit.
// @match        *://nexus.tcs.local/*
// @include      http*://nexus.tcs.local/*
// @run-at       document-start
// @noframes
// @grant        none
// ==/UserScript==
(function () {
  'use strict';

  var KEY = 'tcpush_pending';
  var LOG = [];
  function log(m) { LOG.push(new Date().toISOString().slice(11, 19) + '  ' + m); }

  /* ---------------- payload ---------------- */
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

  /* ---------------- banner ---------------- */
  var bar = null, msg = null;
  function ensureBar() {
    if (bar || !document.body) return bar;
    bar = document.createElement('div');
    bar.style.cssText = 'position:fixed;left:50%;top:14px;transform:translateX(-50%);z-index:2147483647;' +
      'background:#0b3d91;color:#fff;font:13px/1.45 Segoe UI,Arial;padding:10px 16px;border-radius:8px;' +
      'box-shadow:0 8px 26px rgba(0,0,0,.35);max-width:72vw;text-align:center';
    msg = document.createElement('div');
    bar.appendChild(msg);
    document.body.appendChild(bar);
    return bar;
  }
  function say(t, kind) {
    ensureBar(); if (!msg) return;
    msg.innerHTML = (kind === 'err' ? '⚠ ' : '🚀 ') + t;
    bar.style.background = kind === 'err' ? '#b91c1c' : kind === 'ok' ? '#166534' : '#0b3d91';
  }
  function hideBarIn(ms) { setTimeout(function () { if (bar) { bar.remove(); bar = null; msg = null; } }, ms); }
  function done(t) { say(t, 'ok'); hideBarIn(12000); }
  // On failure: show what the page actually looked like, and let you copy it in one click.
  function fail(t) {
    say(t + '<br><span style="opacity:.85">Finish by hand — nothing was submitted.</span>', 'err');
    var d = diagnostics();
    try { console.log('[Timecard->Nexus]\n' + d + '\n' + LOG.join('\n')); } catch (e) {}
    // print it on screen too, so a screenshot is enough to diagnose
    var pre = document.createElement('pre');
    pre.textContent = d;
    pre.style.cssText = 'text-align:left;margin:8px 0 0;padding:8px;background:rgba(0,0,0,.35);' +
      'border-radius:6px;font:11px/1.35 Consolas,monospace;white-space:pre-wrap;max-height:38vh;overflow:auto';
    if (bar) bar.appendChild(pre);
    var btn = document.createElement('button');
    btn.textContent = '⧉ Copy details for Claude';
    btn.style.cssText = 'margin-top:8px;padding:5px 10px;border:0;border-radius:6px;cursor:pointer;font:12px Segoe UI,Arial';
    btn.onclick = function () {
      var txt = 'Timecard→Nexus v1.5 FAILED\n' + t.replace(/<[^>]*>/g, '') + '\n\n' + d + '\n\nLOG:\n' + LOG.join('\n');
      try { navigator.clipboard.writeText(txt); btn.textContent = '✓ copied — paste it to Claude'; }
      catch (e) { window.prompt('Copy this:', txt); }
    };
    if (bar) bar.appendChild(btn);
  }
  function diagnostics() {
    function n(sel) { try { return document.querySelectorAll(sel).length; } catch (e) { return 'err'; } }
    var mo = document.querySelector('#EditProject');
    return [
      'url: ' + location.href,
      'jQuery: ' + (window.jQuery ? window.jQuery.fn.jquery : 'MISSING') + '   jQuery.active: ' + (window.jQuery ? window.jQuery.active : '-'),
      'task asked for: ' + (P && P.task),
      'input.task_search: ' + n('input.task_search'),
      'button.btn-edit total: ' + n('button.btn-edit') + '   with this data-row: ' + n('button.btn-edit[data-row="' + (P && P.task) + '"]'),
      '#EditProject exists: ' + !!mo + '   innerHTML length: ' + (mo ? mo.innerHTML.length : 0) +
        '   visible: ' + (mo ? (mo.offsetParent !== null) : false),
      '.nav-tabs in modal: ' + n('#EditProject .nav-tabs'),
      'a[data-target="#second"]: ' + n('a[data-target="#second"]'),
      '#form-input_hours: ' + n('#form-input_hours') + '   visible: ' + !!liveForm(),
      'visible .modal: ' + n('.modal.in, .modal.show, .modal[style*="display: block"]')
    ].join('\n');
  }
  function whenBody(fn) {
    if (document.body) return fn();
    var t = setInterval(function () { if (document.body) { clearInterval(t); fn(); } }, 30);
    document.addEventListener('DOMContentLoaded', function () { clearInterval(t); fn(); });
  }

  /* ---------------- helpers ---------------- */
  function waitFor(fn, label, timeout, onTick) {
    timeout = timeout || 60000;
    return new Promise(function (resolve, reject) {
      var t0 = Date.now();
      (function tick() {
        var v = null;
        try { v = fn(); } catch (e) { v = null; }
        if (v) return resolve(v);
        var waited = Date.now() - t0;
        if (waited > timeout) return reject(new Error(label));
        if (onTick) { try { onTick(waited); } catch (e) {} }
        setTimeout(tick, 250);
      })();
    });
  }
  function visible(el) { return !!el && el.offsetParent !== null && el.getBoundingClientRect().height > 0; }
  function liveForm() {
    var fs = document.querySelectorAll('#form-input_hours, #form-inputHours_forQuote');
    for (var i = 0; i < fs.length; i++) if (visible(fs[i])) return fs[i];
    return null;
  }
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

  /* ----------------------------------------------------------------
     Open the task window WITHOUT touching the search box or the table.
     Nexus binds its Edit handler on <body> and reads only data-row:
         $('body').on('click', '.btn-edit, .open_edit_task_modal', …)
         let rowID = $(this).data('row');
     so handing it a button carrying the task number runs Nexus's own
     code path — no search, no waiting for the list, no stale rows.
     ---------------------------------------------------------------- */
  function triggerEdit() {
    var t = String(P.task);
    var real = document.querySelector('button.btn-edit[data-row="' + t + '"]');
    if (visible(real)) { log('clicking the real Edit button for ' + t); clickIt(real); return 'real button'; }
    var b = document.createElement('button');
    b.className = 'btn btn-primary btn-sm btn-edit';
    b.setAttribute('data-row', t);
    b.setAttribute('data-new_project', '1');
    b.style.cssText = 'position:fixed;left:-9999px;top:0';
    document.body.appendChild(b);
    log('dispatching synthesised .btn-edit[data-row=' + t + ']');
    // ONE click only: the native event bubbles to the body-delegated jQuery handler.
    // Adding jQuery(b).trigger('click') would run it a second time and start a
    // second modal load on top of the first.
    clickIt(b);
    setTimeout(function () { if (b.parentNode) b.parentNode.removeChild(b); }, 2000);
    return 'synthesised button';
  }
  function modalReady() {
    var m = document.querySelector('#EditProject');
    if (m && m.querySelector('.nav-tabs') && visible(m)) return m;
    var mods = document.querySelectorAll('.modal.in, .modal.show, .modal[style*="display: block"]');
    for (var i = 0; i < mods.length; i++) if (visible(mods[i]) && mods[i].querySelector('.nav-tabs')) return mods[i];
    // content arrived but Bootstrap never showed it — show it ourselves
    if (m && m.querySelector('.nav-tabs') && !visible(m) && window.jQuery) {
      try { window.jQuery(m).modal('show'); } catch (e) {}
    }
    return null;
  }

  /* ---------------- fill ---------------- */
  function fillForm(f) {
    if (!f || !P || P.__bad || consumed) return false;
    var touched = false;

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
          var msId = hit.getAttribute('data-ms-id');
          var msHidden = f.querySelector('input[name="ts_ms_id"]');
          if (msId && msHidden) msHidden.value = msId;          // Nexus never copies this across
          touched = true;
          log('milestone=' + hit.value + ' ts_ms_id=' + msId);
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

  // Fill the Hours form whenever it turns up, however you got there.
  function watchForForm() {
    var tryFill = function () {
      if (consumed || !P || P.__bad) return;
      var f = liveForm();
      if (f && !f.__tcFilled) { f.__tcFilled = true; setTimeout(function () { fillForm(f); }, 300); }
    };
    try { new MutationObserver(tryFill).observe(document.documentElement, { childList: true, subtree: true }); } catch (e) {}
    setInterval(tryFill, 1000);
  }

  /* ---------------- the flow ---------------- */
  function run() {
    var how = '';
    return Promise.resolve()
      .then(function () {
        // The site is in the US: scripts and data arrive late. Dispatching the
        // click before Nexus has bound its delegated body handler would do
        // nothing at all, so wait for the handler to exist first.
        say('Step 1/3 — waiting for Nexus to finish loading…');
        return waitFor(function () {
          if (!window.jQuery) return null;
          if (!document.querySelector('input.task_search')) return null;
          try {
            var ev = window.jQuery._data(document.body, 'events');
            if (ev && ev.click && ev.click.length) return true;
          } catch (e) { return true; }        // old jQuery: cannot check, assume ready
          return null;
        }, 'Nexus never finished loading its scripts', 60000, function (w) {
          if (w > 3000) say('Step 1/3 — waiting for Nexus to finish loading…  <span style="opacity:.75">' +
                            Math.round(w / 1000) + 's</span>');
        });
      })
      .then(function () {
        say('Step 1/3 — opening task <b>' + P.task + '</b>…');
        how = triggerEdit();
        log('opened via ' + how);
        var retried = false;
        // Nexus is slow, so wait up to 90s. Retry ONCE at 20s, and only if the
        // modal container is still completely empty — i.e. nothing is loading.
        // Re-clicking during a slow-but-healthy load would start a second load
        // on top of the first and leave the window half-built.
        return waitFor(modalReady, 'the task window never opened (Nexus did not load it)', 90000, function (waited) {
          if (!retried && waited > 20000) {
            var m = document.querySelector('#EditProject');
            var empty = !m || m.innerHTML.trim().length === 0;
            // jQuery.active > 0 means a request is genuinely in flight — an empty
            // container then just means the response has not landed yet.
            var inFlight = !!(window.jQuery && window.jQuery.active > 0);
            retried = true;
            if (empty && !inFlight) { log('nothing loading after 20s — retrying the click'); triggerEdit(); }
            else log('load still in progress (active=' + (window.jQuery ? window.jQuery.active : '?') +
                     ', ' + (m ? m.innerHTML.length : 0) + ' chars) — waiting, not re-clicking');
          }
          if (waited > 3000) say('Step 1/3 — opening task <b>' + P.task + '</b>…  <span style="opacity:.75">waiting ' +
                                 Math.round(waited / 1000) + 's</span>');
        });
      })
      .then(function (modal) {
        log('modal ready');
        say('Step 2/3 — opening the <b>Hours</b> tab…');
        return waitFor(function () {
          var a = modal.querySelector('a[data-target="#second"]');
          if (visible(a)) return a;
          var links = modal.querySelectorAll('.nav-tabs a, a[data-toggle="tab"]');
          for (var i = 0; i < links.length; i++)
            if (norm(links[i].textContent) === 'HOURS' && visible(links[i])) return links[i];
          return null;
        }, 'the Hours tab never appeared in the task window', 60000)
        .then(function (tab) {
          clickIt(tab);
          if (window.jQuery) { try { window.jQuery(tab).tab('show'); } catch (e) {} }
          log('clicked Hours tab');
        });
      })
      .then(function () {
        say('Step 3/3 — filling the form…');
        // the Hours tab already contains #form-input_hours
        return waitFor(liveForm, 'the Hours form never became visible', 60000);
      })
      .then(function (form) { if (!consumed) fillForm(form); })
      .catch(function (err) { fail('Stopped at: ' + (err && err.message ? err.message : err)); });
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
    if (document.readyState === 'complete') setTimeout(run, 1200);
    else window.addEventListener('load', function () { setTimeout(run, 1200); });
  });
})();
