/*  Timecard → Nexus  ·  background
 *
 *  Looks a task number up in Nexus and hands back its project and contractor.
 *
 *  It uses the same endpoint Nexus's own Edit button uses:
 *      php/ajax.php?function=get_project_info&rowID=<task>
 *  which is where protected.js reads projectName and Contractor from. The
 *  request carries your Nexus cookies, so it sees exactly what you can see -
 *  and nothing you can't.
 */
'use strict';

/* Turn the Timecard's Nexus address into the ajax endpoint next to it:
   http://nexus.tcs.local/protected.php  ->  http://nexus.tcs.local/php/ajax.php */
function ajaxUrl(nexusUrl, task) {
  let base;
  try { base = new URL(nexusUrl); }
  catch (e) { base = new URL('http://nexus.tcs.local/protected.php'); }
  const dir = base.pathname.replace(/[^/]*$/, '');        // strip protected.php
  return base.origin + dir + 'php/ajax.php' +
         '?function=get_project_info&rowID=' + encodeURIComponent(task);
}

/* The task's own description - the "Description" column in Nexus's task list,
   e.g. "Exterior Refrigeration Pipe Supports". That is what the timecard's JOB
   TYPE holds. It is NOT the description on the Hours tab, which is per entry.
 *
 * Only fields that ARE a description are read. TaskType, Deliverable and Trade
 * are categories, not descriptions - TaskType is what filled JOB TYPE with
 * "CUSTOM ENGINEERING" - and a wrong value on a timesheet is worse than an
 * empty box, so anything not clearly a description is left alone and JOB TYPE
 * stays yours to type. `diag.fields` lists the whole answer, so a Nexus that
 * names the field something else can be read off rather than guessed at. */
const JOB_KEYS = ['Description', 'description', 'TaskDescription', 'Task_Description',
                  'taskDescription', 'task_description', 'TaskDesc', 'task_desc'];
function jobTypeFrom(data) {
  const s = (v) => (typeof v === 'string' ? v.trim() : '');
  for (const k of JOB_KEYS) if (s(data[k])) return { value: s(data[k]), key: k };
  for (const k of Object.keys(data)) {
    // a description of the TASK: not the project's, not one hours entry's
    if (!/desc/i.test(k) || /project|hour|emp|milestone|ms_/i.test(k)) continue;
    if (s(data[k])) return { value: s(data[k]), key: k };
  }
  return { value: '', key: '' };
}

function pick(data) {
  const s = (v) => (v == null ? '' : String(v)).trim();
  const project = s(data.projectName);
  const contractor = s(data.Contractor);
  const job = jobTypeFrom(data);
  if (!project && !contractor && !job.value) return null;   // nothing useful came back
  return {
    project: project,
    contractor: contractor,
    jobType: job.value,
    jobTypeKey: job.key,
    projectId: s(data.projectID),
    projectNo: s(data.Project_No),
    manager: s(data.ProjectManager),
    status: s(data.Status)
  };
}

/* Every answer carries a `diag` so the Timecard's "Test the lookup" button can
   say exactly how far the request got, instead of just failing quietly. */
/* Any tab already sitting on the same host as the address we want. */
async function findNexusTabs(url) {
  let host;
  try { host = new URL(url).hostname; } catch (e) { return []; }
  try {
    const tabs = await chrome.tabs.query({ url: ['http://' + host + '/*', 'https://' + host + '/*'] });
    return tabs.filter(t => t.id !== undefined);
  } catch (e) { return []; }
}

function askTab(tabId, url) {
  return new Promise(resolve => {
    let done = false;
    const finish = (r) => { if (!done) { done = true; resolve(r); } };
    setTimeout(() => finish({ ok: false, error: 'the tab did not answer in 15s' }), 15000);
    try {
      chrome.tabs.sendMessage(tabId, { type: 'tcFetchHere', url: url }, (r) => {
        if (chrome.runtime.lastError)
          return finish({ ok: false, error: chrome.runtime.lastError.message });
        finish(r || { ok: false, error: 'no answer from the tab' });
      });
    } catch (e) { finish({ ok: false, error: String((e && e.message) || e) }); }
  });
}

/* Put the wanted path onto the origin the tab is actually on. */
function onTabOrigin(tabUrl, wanted) {
  try {
    const t = new URL(tabUrl), u = new URL(wanted);
    if (t.host !== u.host && t.hostname !== u.hostname) return wanted;
    u.protocol = t.protocol;
    u.host = t.host;
    return u.href;
  } catch (e) { return wanted; }
}

function flipScheme(u) {
  return u.startsWith('https://') ? 'http://'  + u.slice(8)
       : u.startsWith('http://')  ? 'https://' + u.slice(7) : null;
}

/* One request for one row id. Returns the body, or null if the route failed.
   `route` is how this browser reaches Nexus, worked out once and reused. */
async function ask(rowId, nexusUrl, diag, route, loud) {
  const url = ajaxUrl(nexusUrl, rowId);
  const note = (s) => { if (loud) diag.tried.push(s); };

  /* An open Nexus tab is the best place to ask from: it already reaches the
     site, with its cookies, its proxy route and any certificate you accepted.
     A background fetch has none of that, so it is only the fallback. */
  if (route.tabId !== undefined) {
    /* Ask using the tab's OWN origin, not the address the Timecard was
       configured with. A tab sitting on https cannot fetch an http address -
       the browser blocks it as mixed content - and the reverse wastes a
       redirect. The tab is signed in at whatever origin it is on, so that is
       the one to use. */
    const target = onTabOrigin(route.tabUrl, url);
    if (loud && target !== url) diag.rewrote = url + '  ->  ' + target;
    const viaTab = await askTab(route.tabId, target);
    note('in your Nexus tab (' + target.replace(/\?.*$/, '') + ') -> ' +
         (viaTab.ok ? viaTab.status : viaTab.error));
    if (viaTab.ok) { if (loud) { diag.how = 'from your open Nexus tab'; diag.url = target; } return viaTab; }
    return null;
  }

  for (const attempt of [route.scheme === 'flip' ? flipScheme(url) : url]) {
    if (!attempt) continue;
    try {
      const r = await fetch(attempt, {
        credentials: 'include',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      note('background ' + attempt + ' -> ' + r.status);
      if (loud) { diag.how = 'from the extension itself'; diag.url = attempt; }
      return { status: r.status, finalUrl: r.url,
               type: r.headers.get('content-type') || '', text: await r.text() };
    } catch (e) {
      note('background ' + attempt + ' -> ' + String((e && e.message) || e));
    }
  }
  return null;
}

/* Work out how this browser can reach Nexus, and prove it on the row we
   actually want. Returns { route, got } or null. */
async function openRoute(task, nexusUrl, diag, tabs) {
  const url = ajaxUrl(nexusUrl, task);
  tabs = tabs || await findNexusTabs(url);
  diag.nexusTabsOpen = tabs.length;
  for (const tab of tabs) {
    const route = { tabId: tab.id, tabUrl: tab.url };
    const got = await ask(task, nexusUrl, diag, route, true);
    if (got) return { route: route, got: got };
  }
  for (const scheme of ['as-is', 'flip']) {
    const route = { scheme: scheme };
    const got = await ask(task, nexusUrl, diag, route, true);
    if (got) return { route: route, got: got };
  }
  return null;
}

/* Turn a body into the task's fields, or null if it is not a task. `soft`
   returns null instead of naming a failure, for callers where "not there" is an
   ordinary answer rather than something to report. */
function readBody(got, task, diag, soft) {
  if (!got) return soft ? null : { error: 'no answer' };
  if (!soft) { diag.status = got.status; diag.type = got.type; diag.finalUrl = got.finalUrl; }
  if (got.status < 200 || got.status >= 300)
    return soft ? null : (diag.step = 'bad status', { error: 'Nexus answered ' + got.status });

  const text = (got.text || '').trim();
  if (!soft) { diag.length = text.length; diag.sample = text.slice(0, 300); }
  if (!text)
    return soft ? null : (diag.step = 'empty body',
      { error: 'task ' + task + ' not found (Nexus sent nothing back)' });
  // A login page instead of JSON means the session has expired.
  if (/^\s*</.test(text))
    return soft ? null : (diag.step = 'got HTML, not JSON',
      { error: 'sign in to Nexus first, then try again' });

  let data;
  try { data = JSON.parse(text); }
  catch (e) {
    return soft ? null : (diag.step = 'body is not JSON', { error: 'Nexus sent something unreadable' });
  }
  if (!soft) {
    diag.keys = Object.keys(data).slice(0, 40);
    /* The whole answer, field by field, so the box that should hold the task's
       description can be pointed at instead of guessed at. */
    diag.fields = Object.keys(data).slice(0, 60).map((k) => {
      const v = data[k];
      const t = v == null ? '' : String(v);
      return k + ' = ' + (t.length > 120 ? t.slice(0, 120) + '…' : t);
    });
  }
  const info = pick(data);
  if (!info)
    return soft ? null : (diag.step = 'JSON had no projectName / Contractor / description',
      { error: 'task ' + task + ' not found' });
  return { info: info };
}

/* Nexus lists a task's sub-tasks as separate rows - 300042, 300042-1,
   300042-2 - and each carries its own description. The newest one is the one
   the timecard wants. There is no call that lists them, so they are asked for
   directly. A whole batch goes at once: asking one at a time meant a round
   trip each, and four of those ran the timecard's patience out. Nearly every
   task is answered by the first batch. */
/* ---- what the task list itself said ------------------------------------
   A row is asked for exactly as it is written: 300042 answers for 300042, and
   300042-1 for 300042-1. Some Nexus builds hand the parent task back whatever
   suffix get_project_info is given, though, and then the endpoint alone cannot
   tell the rows apart. The task list can - it prints a Description against every
   row - so whenever the N button opens a task, page.js reads that column and
   every row it saw is kept here, one description per row id. */
const learned = new Map();                  // '300042-2' -> { id, desc }
let learnedAt = 0;                          // bumped whenever something new is read
function saveLearned() {
  try { chrome.storage.local.set({ tcRows2: Object.fromEntries(learned) }); } catch (e) {}
}
try {
  chrome.storage.local.get('tcRows2', (o) => {
    void chrome.runtime.lastError;
    const m = (o && o.tcRows2) || {};
    for (const k of Object.keys(m)) if (m[k] && typeof m[k] === 'object') learned.set(k, m[k]);
  });
} catch (e) {}
function rememberRows(seen) {
  if (!seen || !Array.isArray(seen.rows) || !seen.rows.length) return;
  let any = false;
  for (const r of seen.rows) {
    const row = String((r && r.row) || '').trim();
    if (!row) continue;
    const was = learned.get(row) || {};
    const next = { id: String((r && r.id) || was.id || row).trim(),
                   desc: String((r && r.desc) || was.desc || '').trim() };
    if (was.id === next.id && was.desc === next.desc) continue;
    learned.set(row, next);                 // 300042, 300042-1, 300042-2 … each its own
    any = true;
  }
  if (any) { learnedAt = Date.now(); saveLearned(); }
}

/* Ask an open Nexus tab what it can see of this task right now. Free - it reads
   the page, touches nothing - and it means a task already listed over there is
   understood without anyone pressing N first. */
function scanTab(tabId, base) {
  return new Promise((done) => {
    let settled = false;
    const finish = (r) => { if (!settled) { settled = true; done(r); } };
    setTimeout(() => finish(null), 4000);
    try {
      chrome.tabs.sendMessage(tabId, { type: 'tcScanRows', base: base }, (r) => {
        void chrome.runtime.lastError;
        finish(r && r.ok ? r.rows : null);
      });
    } catch (e) { finish(null); }
  });
}

/* The same task is asked for again the moment a whole column is pasted, so a
   short memory keeps that from becoming a burst of identical requests. */
const CACHE_MS = 60000;
const cache = new Map();
function cached(key) {
  const hit = cache.get(key);
  if (hit && Date.now() - hit.t < CACHE_MS) return hit.res;
  if (hit) cache.delete(key);
  return null;
}

async function lookup(task, nexusUrl) {
  const diag = { url: ajaxUrl(nexusUrl, task), step: 'starting', tried: [] };
  task = String(task || '').trim();
  if (!/^\d{3,}(-\d+)?$/.test(task))
    return { ok: false, error: 'not a task number', diag: diag };

  diag.step = 'fetching';
  const t0 = Date.now();

  /* Look at the task list first, if a Nexus tab has it on screen. It gives the
     id Nexus itself uses for each row - which for a revision is not always the
     number you see - so the question can be asked about the right row rather
     than about a number the endpoint may quietly round back to the parent. */
  const tabs = await findNexusTabs(ajaxUrl(nexusUrl, task));
  for (const tab of tabs) {
    const rows = await scanTab(tab.id, task);
    if (rows && rows.length) {
      rememberRows({ task: task, rows: rows });
      diag.sawInTab = rows.map(r => r.row + (r.id !== r.row ? ' (id ' + r.id + ')' : '')).join(', ');
      break;
    }
  }
  const known = learned.get(task) || {};
  const rowId = known.id || task;
  if (rowId !== task) diag.askedAs = 'asked Nexus for row id ' + rowId + ', which is what it calls ' + task;

  const opened = await openRoute(rowId, nexusUrl, diag, tabs);
  if (!opened) {
    diag.step = diag.nexusTabsOpen ? 'the Nexus tab could not fetch it either'
                                   : 'no Nexus tab open, and the extension cannot reach it directly';
    return { ok: false,
             error: diag.nexusTabsOpen
               ? 'Nexus is open but would not answer ' + diag.url + '. Open that exact address in a tab to see what it says.'
               : 'open Nexus in a tab first, then try again — the extension on its own cannot reach ' + diag.url,
             diag: diag };
  }

  const base = readBody(opened.got, task, diag, false);
  if (base.error) return { ok: false, error: base.error, diag: diag };

  /* The row asked for is the row answered for - no hunting for a newer one.
     If the list showed a description for THIS row, it wins: it is per-row, and
     get_project_info is not always, however it is addressed. */
  let info = base.info;
  const desc = (learned.get(task) || {}).desc;
  if (desc && desc !== info.jobType) {
    info = Object.assign({}, info, { jobType: desc, jobTypeKey: 'the task list' });
    diag.fromList = 'job type read off the task list, row ' + task;
  }
  diag.ms = Date.now() - t0;
  diag.row = task;
  diag.jobTypeFrom = info.jobTypeKey || '(no description field in the answer)';
  diag.step = 'ok';
  return { ok: true, data: Object.assign({ row: task }, info), diag: diag };
}

/* ---- opening Nexus ----------------------------------------------------
   The rocket used to open a new tab every time. If Nexus is already open,
   that tab is the one to use: it is signed in, and a pile of duplicates is
   nobody's idea of help. */
function activate(tab) {
  return new Promise((done) => {
    try {
      chrome.tabs.update(tab.id, { active: true }, () => {
        void chrome.runtime.lastError;
        try { chrome.windows.update(tab.windowId, { focused: true }, () => { void chrome.runtime.lastError; done(); }); }
        catch (e) { done(); }
      });
    } catch (e) { done(); }
  });
}
function handOver(tabId, job) {
  return new Promise((done) => {
    try {
      chrome.tabs.sendMessage(tabId, { type: 'tcJob', job: job }, (r) => {
        done(!chrome.runtime.lastError && !!(r && r.ok));
      });
    } catch (e) { done(false); }
  });
}
async function openJob(job, nexusUrl) {
  const base = String(nexusUrl || '').split('#')[0] || 'http://nexus.tcs.local/protected.php';
  const url = base + (job ? '#tcjob=' + encodeURIComponent(JSON.stringify(job)) : '');
  const tabs = await findNexusTabs(base);

  for (const tab of tabs) {
    /* Best case: the tab is on the task list already, so it takes the job
       where it stands - no reload, no losing your place. */
    if (job && await handOver(tab.id, job)) {
      await activate(tab);
      return { ok: true, how: 'handed to your open Nexus tab' };
    }
    // It is on some other Nexus page, so send that same tab to the list.
    try {
      await chrome.tabs.update(tab.id, { url: onTabOrigin(tab.url, url) });
      await activate(tab);
      return { ok: true, how: 'sent your open Nexus tab to the task' };
    } catch (e) { /* try the next tab */ }
  }

  try {
    await chrome.tabs.create({ url: url, active: true });
    return { ok: true, how: 'opened a new Nexus tab' };
  } catch (e) {
    return { ok: false, error: String((e && e.message) || e) };
  }
}

chrome.runtime.onMessage.addListener((msg, sender, reply) => {
  if (!msg) return;

  if (msg.type === 'tcLookup') {
    const key = String(msg.task) + '@' + String(msg.nexusUrl || '');
    const hit = cached(key);
    if (hit) { reply(Object.assign({ fromCache: true }, hit)); return true; }
    lookup(msg.task, msg.nexusUrl).then((res) => {
      if (res && res.ok) cache.set(key, { t: Date.now(), res: res });
      reply(res);
    });
    return true;                                        // reply arrives asynchronously
  }

  // page.js, by way of the Nexus tab's content script
  if (msg.type === 'tcRowsSeen') {
    rememberRows(msg.seen);
    cache.clear();                       // the old answer is the one we outgrew
    return;
  }

  if (msg.type === 'tcOpen') {
    openJob(msg.job, msg.nexusUrl).then(reply);
    return true;
  }

  // "is Nexus open in this browser?" - the timecard turns its N red when not
  if (msg.type === 'tcStatus') {
    const base = String(msg.nexusUrl || '').split('#')[0] || 'http://nexus.tcs.local/protected.php';
    findNexusTabs(base).then((tabs) => reply({ ok: true, tabs: tabs.length, learnedAt: learnedAt }));
    return true;
  }
});
