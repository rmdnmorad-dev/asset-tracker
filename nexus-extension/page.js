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
    /* The tab is reused now, so a second press would otherwise leave the first
       press's panel sitting underneath this one. */
    var old = document.getElementById('tcn-box');
    if (old) old.remove();
    box = document.createElement('div');
    box.id = 'tcn-box';
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
  /* Nexus lists a task and its revisions as separate rows: 300042, 300042-1,
     300042-2. They are separate tasks, so the one asked for is the one opened -
     300042 means 300042, and never 300042-2. */
  var rx = function (s) { return String(s).replace(/[.*+?^${}()|[\]\\-]/g, '\\$&'); };
  function baseOf(task) { return String(task).replace(/-\d+$/, ''); }
  function subOf(row, base) {
    var m = new RegExp('^' + rx(base) + '(?:-(\\d+))?$').exec(row);
    return m ? (m[1] == null ? -1 : +m[1]) : null;    // -1 = the task itself
  }
  /* Which row this is. data-row is what Nexus opens, but it does not always
     carry the revision; the row's own text does - the Project ID cell reads
     "LM68339XTRASAV.300042-2" - so both are read and the more specific wins.
     Returns null when the row is not this task at all, which is what keeps
     3000420 and 1300042 out of it. */
  function rowIdOf(btn, base) {
    var tr = btn.closest('tr');
    var sub = subOf(String(btn.getAttribute('data-row') || '').trim(), base);
    var m = new RegExp('(?:^|[^0-9-])' + rx(base) + '-(\\d+)(?![0-9])', 'g');
    var text = tr ? (tr.textContent || '') : '', hit;
    while ((hit = m.exec(text)) !== null) {
      var n = +hit[1];
      if (sub === null || n > sub) sub = n;
    }
    if (sub === null) return null;                    // a different task entirely
    return sub < 0 ? base : base + '-' + sub;
  }
  /* Every row the search brought back for this task number, revisions included.
     The descriptions of all of them are worth reporting even though only one is
     opened. */
  function rowsFor(base) {
    var all = document.querySelectorAll('button.btn-edit[data-row]'), out = [], seen = {};
    for (var i = 0; i < all.length; i++) {
      var b = all[i];
      if (!String(b.getAttribute('data-row') || '').trim()) continue;   // hidden template button
      if (!b.closest('tr') || !vis(b)) continue;
      var id = rowIdOf(b, base);
      if (!id || seen[id]) continue;
      seen[id] = 1;
      out.push({ btn: b, row: id });
    }
    return out;
  }
  function editButtons(task) {
    return rowsFor(baseOf(task)).filter(function (r) { return r.row === task; });
  }
  function editButton(task) {
    var l = editButtons(task);
    return l.length ? l[0].btn : null;
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
    /* Always search, even when the number seems to be on screen already: what
       is showing may be a different revision of it, and the right row has to be
       picked out of the full list rather than whatever happens to be there. */
    say('searching for task <b>' + J.task + '</b>…');
    var btn = await lookUp(J.task, false);
    if (!btn) {
      say('not in the list — clearing the other filters and searching again…');
      btn = await lookUp(J.task, true);
    }
    if (!btn) btn = editButton(J.task);          // fall back to whatever is on screen
    if (!btn) throw new Error(
      'Task ' + J.task + ' did not come back from the search.<br>' +
      'Check the number, and that you can see the task in Nexus yourself.');

    // let any straggling row land before reading the list
    await new Promise(function (r) { setTimeout(r, 500); });
    var exact = editButtons(J.task);
    var target = exact.length ? exact[0] : { btn: btn, row: J.task };
    var opened = target.row;

    /* Ask the content script to read this task's rows off the list while they
       are on screen - every revision, not just the one being opened. It takes
       each row's own id and description, which is what makes the timecard's
       JOB TYPE right for each of them afterwards. */
    try { window.postMessage({ __tcScan: baseOf(J.task) }, '*'); } catch (e) {}

    say('opening task <b>' + opened + '</b>…');
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
