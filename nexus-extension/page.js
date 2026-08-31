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

  // ------------------------------------------------------- finding the task
  /* The Edit button must be the real one on the real row, so we search for the
     task the same way a person does and wait for the row to come back. */
  /* Nexus lists a task and its sub-tasks as separate rows: 321526, 321526-1,
     321526-2 … The hours belong on the newest one, so of everything the search
     brought back for this number we take the highest suffix. A number given
     WITH a suffix means that exact row and nothing else. */
  var rx = function (s) { return String(s).replace(/[.*+?^${}()|[\]\\-]/g, '\\$&'); };
  function subOf(row, task) {
    var m = new RegExp('^' + rx(task) + '(?:-(\\d+))?$').exec(row);
    return m ? (m[1] == null ? -1 : +m[1]) : null;    // -1 = the task itself
  }
  function editButtons(task) {
    var all = document.querySelectorAll('button.btn-edit[data-row]'), out = [];
    for (var i = 0; i < all.length; i++) {
      var b = all[i], row = String(b.getAttribute('data-row') || '').trim();
      if (!row) continue;                 // Nexus's hidden template button
      var s = subOf(row, task);
      if (s === null || !b.closest('tr') || !vis(b)) continue;
      out.push({ btn: b, row: row, sub: s });
    }
    out.sort(function (a, b) { return a.sub - b.sub; });
    return out;
  }
  function editButton(task) {
    var l = editButtons(task);
    return l.length ? l[l.length - 1].btn : null;     // the newest sub-task
  }

  function fire(el, types) {
    types.forEach(function (t) { el.dispatchEvent(new Event(t, { bubbles: true })); });
    if (window.jQuery) try { window.jQuery(el).trigger('input').trigger('change'); } catch (e) {}
  }

  function startSearch(task, clearOtherFilters) {
    var s = document.querySelector('input.task_search');
    if (!s) return false;
    if (clearOtherFilters) {
      var pn = document.querySelector('input.project_name_search');
      if (pn && pn.value) { pn.value = ''; fire(pn, ['input', 'change']); }
    }
    s.value = task;
    fire(s, ['input', 'change']);
    // Nexus exposes run_search() globally; calling it skips its own 800ms
    // debounce and avoids firing the search twice.
    if (typeof window.run_search === 'function') {
      try { window.run_search(); return true; } catch (e) {}
    }
    // otherwise let the page's own keyup debounce / enter-to-search do it
    ['keydown', 'keyup'].forEach(function (t) {
      s.dispatchEvent(new KeyboardEvent(t, { bubbles: true, key: 'Enter', keyCode: 13, which: 13 }));
    });
    return true;
  }

  async function lookUp(task, clearOtherFilters) {
    if (!startSearch(task, clearOtherFilters)) return null;
    try {
      // A search that is going to come back does so well inside this, even on
      // a slow day. Waiting longer just delays telling you the number is wrong.
      return await waitFor(function () { return editButton(task); },
                           'not found', 30000);
    } catch (e) { return null; }
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

    // 1 - find the task's own Edit button, searching for it if need be.
    //
    // It has to be the REAL button on the real row. Nexus's Edit handler reads
    // both the button's data-row and its row's data-milestone:
    //     let rowID = $(this).data('row');
    //     let opened_milestone = $(this).closest('tr').attr('data-milestone');
    // so a button we make ourselves has no row to belong to and opens a
    // half-built task window. Only a task already listed on screen ever
    // worked; any other number quietly broke.
    var btn = editButton(J.task);
    if (!btn) {
      say('searching for task <b>' + J.task + '</b>…');
      btn = await lookUp(J.task, false);
    }
    if (!btn) {
      say('not in the list — clearing the other filters and searching again…');
      btn = await lookUp(J.task, true);
    }
    if (!btn) throw new Error(
      'Task ' + J.task + ' did not come back from the search.<br>' +
      'Check the number, and that you can see the task in Nexus yourself.');

    /* The search paints its rows together, but give any straggler a moment to
       land before choosing — picking the newest sub-task is only right if all
       of them are on screen to choose from. */
    await new Promise(function (r) { setTimeout(r, 500); });
    var rows = editButtons(J.task);
    var target = rows.length ? rows[rows.length - 1] : { btn: btn, row: J.task, sub: -1 };
    var opened = target.row;

    say(rows.length > 1
      ? 'task <b>' + J.task + '</b> has ' + rows.length + ' sub-tasks — opening the newest, <b>' +
        opened + '</b>…'
      : 'opening task <b>' + opened + '</b>…');
    click(target.btn);

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

    done('Filled ' + filled.join(', ') + ' on <b>' + opened + '</b>.' +
         '<br>Check it and press <b>Submit</b> yourself.' +
         (missing.length ? '<br><span style="color:#ffd58a">Could not fill: ' + missing.join(', ') + '</span>' : ''));
  })().catch(function (e) {
    fail((e && e.message ? e.message : e) + '<br>Nothing was submitted.');
  });
})();
