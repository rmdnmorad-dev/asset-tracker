/*  Timecard → Nexus  ·  content script
 *
 *  Runs in the Nexus tab that the Timecard's 🚀 opened. Its only jobs are to
 *  read the job off the URL and hand it to page.js.
 *
 *  Why two files: a content script lives in an isolated world and cannot see
 *  the page's own jQuery, which the fill routine needs. page.js is injected
 *  into the page itself, where jQuery is real.
 */
function tcInject(job) {
  var s = document.createElement('script');
  s.src = chrome.runtime.getURL('page.js');
  s.dataset.job = JSON.stringify(job);
  s.onload = function () { s.remove(); };
  (document.head || document.documentElement).appendChild(s);
}

(function () {
  'use strict';

  // Read it now, before anything else can rewrite the URL.
  var m = /[#&]tcjob=([^&]+)/.exec(location.hash || '');
  if (!m) return;

  var job = null;
  try { job = JSON.parse(decodeURIComponent(m[1])); } catch (e) { return; }
  if (!job || !job.task) return;

  // Take the job out of the address bar so a refresh doesn't re-run it.
  try {
    history.replaceState(null, '', location.pathname + location.search);
  } catch (e) {}

  if (document.documentElement) tcInject(job);
  else document.addEventListener('readystatechange', function () { tcInject(job); }, { once: true });
})();

/* A job handed to a tab that is already sitting on the task list. Nothing
   reloads, so whatever you had open stays open. If this is some other Nexus
   page the answer is no, and the extension sends the tab to the list instead. */
/* ---- reading the task list ---------------------------------------------
   Nexus lists a task and its revisions as separate rows. Two things on each row
   matter, and both are plain DOM, so they are read here rather than in the page
   script: the Edit button's data-row - the id Nexus itself hands to
   get_project_info, which for a revision is NOT always the number you see - and
   the Description cell, which is that row's own.
   Nothing is clicked, typed or navigated; whatever is on screen is simply read. */
function tcRx(s) { return String(s).replace(/[.*+?^${}()|[\]\\-]/g, '\\$&'); }

/* Which column holds the description. The heading is looked for across the
   whole table, not just a <thead> - plenty of grids keep their header row
   elsewhere, and demanding a <thead> meant finding nothing at all. */
function tcDescColumn(tr) {
  var tbl = tr && tr.closest('table');
  if (!tbl) return -1;
  var rows = tbl.querySelectorAll('tr');
  for (var r = 0; r < rows.length && r < 6; r++) {
    var cells = rows[r].children;
    for (var i = 0; i < cells.length; i++) {
      var txt = String(cells[i].textContent || '').toUpperCase().replace(/[^A-Z]/g, '');
      if (txt === 'DESCRIPTION') return i;
    }
  }
  // the header may live in a table of its own, beside or above this one
  var heads = document.querySelectorAll('th');
  for (var h = 0; h < heads.length; h++) {
    var t = String(heads[h].textContent || '').toUpperCase().replace(/[^A-Z]/g, '');
    if (t !== 'DESCRIPTION') continue;
    var row = heads[h].parentElement;
    if (row) for (var k = 0; k < row.children.length; k++)
      if (row.children[k] === heads[h]) return k;
  }
  return -1;
}

/* Every row on screen belonging to this task number, revisions included. */
function tcScan(base) {
  base = String(base || '').replace(/-\d+$/, '');
  if (!/^\d{3,}$/.test(base)) return [];
  var out = [], seen = {}, idx = -1;
  var all = document.querySelectorAll('button.btn-edit[data-row]');
  var re = new RegExp('(?:^|[^0-9-])' + tcRx(base) + '-(\\d+)(?![0-9])', 'g');
  var exact = new RegExp('^' + tcRx(base) + '(?:-(\\d+))?$');
  for (var i = 0; i < all.length; i++) {
    var b = all[i], dr = String(b.getAttribute('data-row') || '').trim();
    if (!dr) continue;                                    // hidden template button
    var tr = b.closest('tr');
    if (!tr || b.offsetParent === null) continue;
    var m = exact.exec(dr), sub = m ? (m[1] == null ? -1 : +m[1]) : null;
    var text = tr.textContent || '', hit;
    re.lastIndex = 0;
    while ((hit = re.exec(text)) !== null) {
      var n = +hit[1];
      if (sub === null || n > sub) sub = n;
    }
    if (sub === null) continue;                           // a different task
    var id = sub < 0 ? base : base + '-' + sub;
    if (seen[id]) continue;
    seen[id] = 1;
    if (idx < 0) idx = tcDescColumn(tr);
    out.push({
      row: id,
      id: dr,                                             // what Nexus asks with
      desc: idx >= 0 && idx < tr.children.length
        ? String(tr.children[idx].textContent || '').replace(/\s+/g, ' ').trim() : ''
    });
  }
  return out;
}

function tcReport(base) {
  var rows = tcScan(base);
  if (rows.length) {
    try { chrome.runtime.sendMessage({ type: 'tcRowsSeen', seen: { task: base, rows: rows } }); }
    catch (err) {}
  }
  return rows;
}

/* page.js runs in the page's own world and cannot reach the extension, so it
   asks for the scan through here once its search has landed. */
window.addEventListener('message', function (e) {
  if (e.source !== window || !e.data) return;
  if (e.data.__tcScan) { tcReport(e.data.__tcScan); return; }
  // the milestones this task really offers, read off its own Hours tab
  if (e.data.__tcMilestones) {
    try { chrome.runtime.sendMessage({ type: 'tcMilestonesSeen', seen: e.data.__tcMilestones }); }
    catch (err) {}
  }
});

/* The lookup asks this before it fetches: if the rows happen to be on screen
   already, they say which id Nexus uses for each and what each one is for. */
chrome.runtime.onMessage.addListener(function (msg, sender, reply) {
  if (!msg || msg.type !== 'tcScanRows') return;
  try { reply({ ok: true, rows: tcScan(msg.base) }); } catch (e) { reply({ ok: false, rows: [] }); }
});

chrome.runtime.onMessage.addListener(function (msg, sender, reply) {
  if (!msg || msg.type !== 'tcJob' || !msg.job || !msg.job.task) return;
  if (!document.querySelector('input.task_search')) { reply({ ok: false }); return; }
  try { tcInject(msg.job); reply({ ok: true }); }
  catch (e) { reply({ ok: false }); }
});

/* ---- lookups run in here, not in the background ----------------------
   A background fetch has its own network context: it cannot use a
   certificate exception you clicked through, and internal hosts and proxies
   often refuse it outright. This tab already reaches Nexus, so the request is
   made here instead, exactly like the site's own AJAX. */
/* A marker the page can see, so "is the extension running on this Nexus tab?"
   is answerable instead of guessable. */
function tcMark() {
  try { document.documentElement.setAttribute('data-tc-nexus', '1'); } catch (e) {}
}
tcMark();                                   // document_start: <html> may not exist yet
document.addEventListener('DOMContentLoaded', tcMark);

chrome.runtime.onMessage.addListener(function (msg, sender, reply) {
  if (!msg || msg.type !== 'tcFetchHere' || !msg.url) return;
  fetch(msg.url, { credentials: 'same-origin',
                   headers: { 'X-Requested-With': 'XMLHttpRequest' } })
    .then(function (r) {
      return r.text().then(function (t) {
        reply({ ok: true, status: r.status, finalUrl: r.url,
                type: r.headers.get('content-type') || '', text: t });
      });
    })
    .catch(function (e) {
      reply({ ok: false, error: String((e && e.message) || e) });
    });
  return true;                       // the answer comes back asynchronously
});
