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

function pick(data) {
  const s = (v) => (v == null ? '' : String(v)).trim();
  const project = s(data.projectName);
  const contractor = s(data.Contractor);
  if (!project && !contractor) return null;             // nothing useful came back
  return {
    project: project,
    contractor: contractor,
    projectId: s(data.projectID),
    projectNo: s(data.Project_No),
    taskType: s(data.TaskType),
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

function flipScheme(u) {
  return u.startsWith('https://') ? 'http://'  + u.slice(8)
       : u.startsWith('http://')  ? 'https://' + u.slice(7) : null;
}

async function lookup(task, nexusUrl) {
  const url = ajaxUrl(nexusUrl, task);
  const diag = { url: url, step: 'starting', tried: [] };
  if (!/^\d{3,}$/.test(String(task || '')))
    return { ok: false, error: 'not a task number', diag: diag };

  /* An open Nexus tab is the best place to ask from: it already reaches the
     site, with its cookies, its proxy route and any certificate you accepted.
     A background fetch has none of that, so it is only the fallback. */
  let got = null;
  diag.step = 'fetching';
  const t0 = Date.now();

  const nexusTabs = await findNexusTabs(url);
  diag.nexusTabsOpen = nexusTabs.length;
  for (const tab of nexusTabs) {
    const viaTab = await askTab(tab.id, url);
    diag.tried.push('in your Nexus tab -> ' + (viaTab.ok ? viaTab.status : viaTab.error));
    if (viaTab.ok) { got = viaTab; diag.how = 'from your open Nexus tab'; break; }
  }

  if (!got) {
    for (const attempt of [url, flipScheme(url)]) {
      if (!attempt) continue;
      try {
        const r = await fetch(attempt, {
          credentials: 'include',
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        diag.tried.push('background ' + attempt + ' -> ' + r.status);
        got = { status: r.status, finalUrl: r.url,
                type: r.headers.get('content-type') || '', text: await r.text() };
        diag.how = 'from the extension itself';
        diag.url = attempt;
        break;
      } catch (e) {
        diag.tried.push('background ' + attempt + ' -> ' + String((e && e.message) || e));
      }
    }
  }

  if (!got) {
    diag.step = nexusTabs.length ? 'the Nexus tab could not fetch it either'
                                 : 'no Nexus tab open, and the extension cannot reach it directly';
    return { ok: false,
             error: nexusTabs.length
               ? 'Nexus is open but would not answer ' + url + '. Open that exact address in a tab to see what it says.'
               : 'open Nexus in a tab first, then try again — the extension on its own cannot reach ' + url,
             diag: diag };
  }

  diag.ms = Date.now() - t0;
  diag.status = got.status;
  diag.type = got.type;
  diag.finalUrl = got.finalUrl;
  if (got.status < 200 || got.status >= 300) {
    diag.step = 'bad status';
    return { ok: false, error: 'Nexus answered ' + got.status, diag: diag };
  }

  const text = (got.text || '').trim();
  diag.length = text.length;
  diag.sample = text.slice(0, 300);
  if (!text) {
    diag.step = 'empty body';
    return { ok: false, error: 'task ' + task + ' not found (Nexus sent nothing back)', diag: diag };
  }
  // A login page instead of JSON means the session has expired.
  if (/^\s*</.test(text)) {
    diag.step = 'got HTML, not JSON';
    return { ok: false, error: 'sign in to Nexus first, then try again', diag: diag };
  }

  let data;
  try { data = JSON.parse(text); }
  catch (e) {
    diag.step = 'body is not JSON';
    return { ok: false, error: 'Nexus sent something unreadable', diag: diag };
  }
  diag.keys = Object.keys(data).slice(0, 40);

  const info = pick(data);
  if (!info) {
    diag.step = 'JSON had no projectName / Contractor';
    return { ok: false, error: 'task ' + task + ' not found', diag: diag };
  }
  diag.step = 'ok';
  return { ok: true, data: info, diag: diag };
}

chrome.runtime.onMessage.addListener((msg, sender, reply) => {
  if (!msg || msg.type !== 'tcLookup') return;
  lookup(msg.task, msg.nexusUrl).then(reply);
  return true;                                          // reply arrives asynchronously
});
