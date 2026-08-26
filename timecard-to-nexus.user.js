// ==UserScript==
// @name         Timecard → Nexus (Add Hours autofill)
// @namespace    jordan-isat-timecard
// @version      1.0
// @description  Fills the Nexus "Add Hours" form from the Timecard rocket (🚀). Selects the milestone, then Hours, Date and Description. Never presses Submit.
// @match        *://nexus.tcs.local/protected.php*
// @run-at       document-idle
// @grant        none
// ==/UserScript==
(function () {
  'use strict';

  /* ---------- read what the Timecard sent in the URL hash ---------- */
  function readPayload() {
    const m = /#tcpush=([^&]+)/.exec(location.hash || '');
    if (!m) return null;
    try { return JSON.parse(decodeURIComponent(m[1])); } catch (e) { return null; }
  }
  const P = readPayload();
  if (!P || !P.task) return;
  // clear the hash so a manual refresh doesn't run it all again
  history.replaceState(null, '', location.pathname + location.search);

  /* ---------- progress banner ---------- */
  const bar = document.createElement('div');
  bar.style.cssText = 'position:fixed;left:50%;top:14px;transform:translateX(-50%);z-index:99999;' +
    'background:#0b3d91;color:#fff;font:13px/1.45 Segoe UI,Arial;padding:10px 16px;border-radius:8px;' +
    'box-shadow:0 8px 26px rgba(0,0,0,.35);max-width:70vw';
  document.body.appendChild(bar);
  let step = 0;
  const say = (t, ok) => {
    bar.innerHTML = (ok === false ? '⚠ ' : '🚀 ') + t;
    if (ok === false) bar.style.background = '#b91c1c';
    if (ok === true) bar.style.background = '#166534';
  };
  const done = (t) => { say(t, true); setTimeout(() => bar.remove(), 9000); };
  const fail = (t) => { say(t + '<br><span style="opacity:.85">Do the rest by hand — nothing was submitted.</span>', false); };

  /* ---------- waiting helpers (Nexus loads slowly / via AJAX) ---------- */
  // Poll for a condition. Long default timeout because Nexus can take a while.
  function waitFor(fn, label, timeout = 45000, interval = 250) {
    return new Promise((resolve, reject) => {
      const t0 = Date.now();
      (function tick() {
        let v = null;
        try { v = fn(); } catch (e) { v = null; }
        if (v) return resolve(v);
        if (Date.now() - t0 > timeout) return reject(new Error(label));
        setTimeout(tick, interval);
      })();
    });
  }
  // Wait until an element is present AND visible (Nexus builds modals hidden first)
  const visible = (el) => el && el.offsetParent !== null && el.getBoundingClientRect().height > 0;
  const seen = (sel, root) => { const e = (root || document).querySelector(sel); return visible(e) ? e : null; };
  // Wait for the page to go quiet — no pending jQuery AJAX — before the next click
  function settle(ms = 400) {
    return new Promise(res => {
      const t0 = Date.now();
      (function tick() {
        const busy = (window.jQuery && jQuery.active > 0);
        if (!busy && Date.now() - t0 > ms) return res();
        if (Date.now() - t0 > 30000) return res();      // never hang forever
        setTimeout(tick, 150);
      })();
    });
  }
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));

  // Set a field the way the page's own scripts expect (jQuery + native listeners both fire)
  function setVal(el, val) {
    const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, val);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new Event('blur', { bubbles: true }));
    if (window.jQuery) jQuery(el).val(val).trigger('input').trigger('change');
  }
  function clickIt(el) {
    el.scrollIntoView({ block: 'center' });
    el.click();
    if (window.jQuery) jQuery(el).trigger('click');
  }
  const norm = (s) => String(s || '').replace(/\s+/g, ' ').trim().toUpperCase();

  /* ---------- the flow ---------- */
  (async function run() {
    try {
      /* 1 ─ type the task number into the Task search box */
      say('Step 1/6 — searching task <b>' + P.task + '</b>…');
      const search = await waitFor(() => seen('input.task_search'), 'the Task search box never appeared');
      setVal(search, P.task);
      // DataTables filters on key events
      ['keydown', 'keypress', 'keyup', 'search'].forEach(t =>
        search.dispatchEvent(new KeyboardEvent(t, { bubbles: true, key: 'Enter', keyCode: 13, which: 13 })));
      if (window.jQuery) jQuery(search).trigger('keyup');
      await settle(700);

      /* 2 ─ the row's own Edit button carries the task number in data-row */
      say('Step 2/6 — opening the task…  <span style="opacity:.8">(Nexus can be slow)</span>');
      const editBtn = await waitFor(
        () => seen('button.btn-edit[data-row="' + P.task + '"]') ||
              // fall back: the only row on screen after filtering
              (document.querySelectorAll('button.btn-edit').length === 1 ? seen('button.btn-edit') : null),
        'the task did not show up in the list — check the task number');
      clickIt(editBtn);
      await settle(900);

      /* 3 ─ the Task Information modal, then its Hours tab */
      say('Step 3/6 — waiting for the task window…');
      const modal = await waitFor(
        () => { const m = document.querySelector('#EditProject'); return visible(m) && m.querySelector('a,button') ? m : null; },
        'the task window did not open');
      say('Step 4/6 — opening the <b>Hours</b> tab…');
      const hoursTab = await waitFor(() => {
        const links = modal.querySelectorAll('a, button, li');
        for (const a of links) if (norm(a.textContent) === 'HOURS' && visible(a)) return a;
        return null;
      }, 'the Hours tab was not found');
      clickIt(hoursTab);
      await settle(900);

      /* 4 ─ the milestone row → its "Add Hours" button */
      say('Step 5/6 — opening <b>Add Hours</b> on ' + (P.milestone || 'the milestone') + '…');
      // Only ever click something genuinely clickable — a <td> can also read "Add Hours",
      // and clicking the cell instead of the link inside it does nothing.
      const CLICKABLE = 'a, button, input[type=button], input[type=submit], [role=button], #ms_hours_modal';
      const addInRow = (tr) => {
        for (const b of tr.querySelectorAll(CLICKABLE))
          if (norm(b.textContent) === 'ADD HOURS' && visible(b)) return b;
        // the label might sit on the cell, with the real control inside it
        for (const td of tr.querySelectorAll('td'))
          if (norm(td.textContent) === 'ADD HOURS') {
            const inner = td.querySelector(CLICKABLE);
            if (inner && visible(inner)) return inner;
            if (visible(td)) return td;
          }
        return null;
      };
      const addBtn = await waitFor(() => {
        const rows = modal.querySelectorAll('tr');
        let firstAdd = null;
        for (const tr of rows) {
          const add = addInRow(tr);
          if (!add) continue;
          if (!firstAdd) firstAdd = add;
          if (!P.milestone) continue;
          const first = tr.cells && tr.cells[0] ? norm(tr.cells[0].textContent) : '';
          if (first === norm(P.milestone) || norm(tr.textContent).indexOf(norm(P.milestone)) === 0) return add;
        }
        return firstAdd;      // otherwise any Add Hours — we set the dropdown ourselves below
      }, 'no “Add Hours” button was found on the Hours tab');
      clickIt(addBtn);
      await settle(900);

      /* 5 ─ the form: milestone FIRST, then hours, date, description */
      say('Step 6/6 — filling the form…');
      const FORM_SEL = '#form-input_hours, #form-inputHours_forQuote';
      const form = await waitFor(
        () => { for (const f of document.querySelectorAll(FORM_SEL)) if (visible(f)) return f; return null; },
        'the Add Hours form did not open');

      // milestone first (it can re-render the rest of the form)
      if (P.milestone) {
        const sel = form.querySelector('select[name*="milestone" i], select');
        if (sel) {
          let hit = null;
          for (const o of sel.options) if (norm(o.textContent) === norm(P.milestone)) { hit = o; break; }
          if (!hit) for (const o of sel.options) if (norm(o.textContent).indexOf(norm(P.milestone)) >= 0) { hit = o; break; }
          if (hit) {
            sel.value = hit.value;
            sel.dispatchEvent(new Event('change', { bubbles: true }));
            if (window.jQuery) jQuery(sel).val(hit.value).trigger('change');
            await settle(500);
          }
        }
      }

      let f2 = form;                                                        // may have re-rendered
      for (const f of document.querySelectorAll(FORM_SEL)) if (visible(f)) { f2 = f; break; }
      const hoursIn = f2.querySelector('input[name="ts_hours"], input[name^="ts_hours"]');
      const dateIn  = f2.querySelector('input[name="ts_date"], input[name^="ts_date"]');
      const descIn  = f2.querySelector('textarea[name="ts_emp_description"], textarea[name^="ts_emp_description"]');

      if (hoursIn && P.hours) setVal(hoursIn, P.hours);
      if (dateIn && P.date) {
        setVal(dateIn, P.date);
        // close any date-picker the field pops open
        dateIn.blur(); document.body.click();
      }
      if (descIn && P.desc) setVal(descIn, P.desc);

      // highlight the form, do NOT submit
      f2.scrollIntoView({ block: 'center' });
      f2.style.outline = '3px solid #16a34a';
      f2.style.outlineOffset = '3px';
      const missing = [];
      if (!hoursIn) missing.push('Hours');
      if (!dateIn) missing.push('Date');
      if (!descIn) missing.push('Description');
      if (missing.length) fail('Filled what I could — could not find: ' + missing.join(', ') + '.');
      else done('Ready — <b>' + P.milestone + '</b> · ' + (P.hours || '0') + ' h · ' + P.date +
                '.<br><span style="opacity:.9">Check it and press <b>Submit</b> yourself.</span>');
    } catch (err) {
      fail('Stopped at: ' + (err && err.message ? err.message : err));
    }
  })();
})();
