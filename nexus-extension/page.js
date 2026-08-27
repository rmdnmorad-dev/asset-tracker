/*  Timecard → Nexus  ·  page script
 *
 *  Runs inside the Nexus page itself, so window.jQuery is the real one.
 *  Searches the task, opens Edit, opens the Hours tab, then fills milestone,
 *  hours, date and description.
 *
 *  It never presses Submit. That is yours.
 */
(function () {
  'use strict';

  var J = null;
  try { J = JSON.parse(document.currentScript.dataset.job); } catch (e) { return; }
  if (!J || !J.task) return;

  // ---------------------------------------------------------------- status
  var box, txt;
  function banner() {
    if (box) return;
    box = document.createElement('div');
    box.style.cssText = [
      'position:fixed', 'z-index:2147483647', 'right:14px', 'bottom:14px',
      'max-width:330px', 'background:#0f1626', 'color:#e8eefc',
      'border:1px solid #2a3550', 'border-radius:10px', 'padding:11px 13px',
      'box-shadow:0 8px 26px rgba(0,0,0,.4)', 'font:13px/1.45 Arial,sans-serif'
    ].join(';');
    box.innerHTML = '<b style="display:block;margin-bottom:3px">🚀 Timecard → Nexus</b>' +
                    '<span id="tcn-msg">starting…</span>';
    (document.body || document.documentElement).appendChild(box);
    txt = box.querySelector('#tcn-msg');
  }
  function say(m, colour) {
    banner();
    if (txt) txt.innerHTML = m;
    if (colour) box.style.borderColor = colour;
  }
  function done(m) { say(m, '#16a34a'); setTimeout(fade, 9000); }
  function fail(m) { say('<span style="color:#ffb4a8">' + m + '</span>', '#ef4444'); }
  function fade() {
    if (!box) return;
    box.style.transition = 'opacity .6s';
    box.style.opacity = '0';
    setTimeout(function () { if (box) box.remove(); box = null; }, 700);
  }

  // ---------------------------------------------------------------- helpers
  var vis = function (el) {
    return !!el && el.offsetParent !== null && el.getBoundingClientRect().height > 0;
  };
  var norm = function (s) {
    return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toUpperCase();
  };
  /* Everything is waited for, never timed. A slow, far-away Nexus just means
     the wait is longer - it never means we click the wrong thing. */
  function waitFor(fn, label, ms) {
    return new Promise(function (res, rej) {
      var t0 = Date.now();
      (function tick() {
        var v = null;
        try { v = fn(); } catch (e) {}
        if (v) return res(v);
        if (Date.now() - t0 > (ms || 120000)) return rej(new Error(label));
        setTimeout(tick, 250);
      })();
    });
  }
  function click(el) {
    try { el.scrollIntoView({ block: 'center' }); } catch (e) {}
    ['pointerdown', 'mousedown', 'mouseup', 'click'].forEach(function (ty) {
      el.dispatchEvent(new MouseEvent(ty, { bubbles: true, cancelable: true, view: window }));
    });
  }
  function setVal(el, v) {
    var proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, v);
    ['input', 'change', 'blur'].forEach(function (t) {
      el.dispatchEvent(new Event(t, { bubbles: true }));
    });
    if (window.jQuery) window.jQuery(el).val(v).trigger('input').trigger('change');
  }

  // ---------------------------------------------------------------- the run
  (async function () {
    say('waiting for Nexus to finish loading…');
    await waitFor(function () {
      if (!window.jQuery) return null;
      if (!document.querySelector('input.task_search')) return null;
      try {
        var ev = window.jQuery._data(document.body, 'events');
        if (ev && ev.click && ev.click.length) return true;
      } catch (e) { return true; }
      return null;
    }, 'Nexus never finished loading its scripts', 180000);

    // 1 - open the task. Nexus's own handler needs only data-row.
    say('opening task <b>' + J.task + '</b>…');
    var real = document.querySelector('button.btn-edit[data-row="' + J.task + '"]');
    if (vis(real)) {
      click(real);
    } else {
      var b = document.createElement('button');
      b.className = 'btn btn-primary btn-sm btn-edit';
      b.setAttribute('data-row', J.task);
      b.style.cssText = 'position:fixed;left:-9999px;top:0';
      document.body.appendChild(b);
      b.click();
      setTimeout(function () { b.remove(); }, 2000);
    }

    // 2 - the task window arrives by AJAX
    say('waiting for the task window…');
    var tab = await waitFor(function () {
      var a = document.querySelector('#EditProject a[data-target="#second"]');
      if (vis(a)) return a;
      var links = document.querySelectorAll('#EditProject .nav-tabs a, .modal.in .nav-tabs a');
      for (var i = 0; i < links.length; i++)
        if (norm(links[i].textContent) === 'HOURS' && vis(links[i])) return links[i];
      return null;
    }, 'the task window never opened', 180000);

    // 3 - Hours tab (the form is inside it)
    say('opening the Hours tab…');
    click(tab);
    if (window.jQuery) { try { window.jQuery(tab).tab('show'); } catch (e) {} }
    var form = await waitFor(function () {
      var f = document.querySelector('#form-input_hours');
      return vis(f) ? f : null;
    }, 'the Hours form never appeared', 120000);

    // 4 - milestone first, then hours, date, description
    var filled = [], missing = [];
    if (J.milestone) {
      var sel = form.querySelector('select[name="ms_select"]') || form.querySelector('select');
      if (sel) {
        var hit = null, o, i;
        for (i = 0; i < sel.options.length; i++) {
          o = sel.options[i];
          if (norm(o.value) === norm(J.milestone) || norm(o.textContent) === norm(J.milestone)) { hit = o; break; }
        }
        if (!hit) for (i = 0; i < sel.options.length; i++) {
          o = sel.options[i];
          if (norm(o.textContent).indexOf(norm(J.milestone)) >= 0) { hit = o; break; }
        }
        if (hit) {
          sel.value = hit.value;
          sel.dispatchEvent(new Event('change', { bubbles: true }));
          if (window.jQuery) window.jQuery(sel).val(hit.value).trigger('change');
          var id = hit.getAttribute('data-ms-id');
          var hid = form.querySelector('input[name="ts_ms_id"]');
          if (id && hid) hid.value = id;            // Nexus does not copy this itself
          filled.push('milestone');
        } else missing.push('milestone "' + J.milestone + '" is not in the list');
      } else missing.push('milestone dropdown');
    }

    var h = form.querySelector('input[name="ts_hours"]');
    var d = form.querySelector('input[name="ts_date"]');
    var n = form.querySelector('textarea[name="ts_emp_description"], #ts_emp_description');

    if (h && J.hours) { setVal(h, String(J.hours)); filled.push('hours'); }
    else if (!h) missing.push('Hours box');

    if (d && J.date) {
      setVal(d, String(J.date));
      try {
        d.blur();
        if (window.jQuery && window.jQuery(d).datepicker) window.jQuery(d).datepicker('hide');
      } catch (e) {}
      var dp = document.getElementById('ui-datepicker-div');
      if (dp) dp.style.display = 'none';
      filled.push('date');
    } else if (!d) missing.push('Date box');

    if (n && J.desc) { setVal(n, String(J.desc)); filled.push('description'); }
    else if (!n) missing.push('Description box');

    form.style.outline = '3px solid #16a34a';
    form.style.outlineOffset = '3px';
    try { form.scrollIntoView({ block: 'center' }); } catch (e) {}

    done('Filled ' + filled.join(', ') + '.<br>Check it and press <b>Submit</b> yourself.' +
         (missing.length ? '<br><span style="color:#ffd58a">Could not fill: ' + missing.join(', ') + '</span>' : ''));
  })().catch(function (e) {
    fail((e && e.message ? e.message : e) + '<br>Nothing was submitted.');
  });
})();
