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
function flipScheme(u) {
  return u.startsWith('https://') ? 'http://'  + u.slice(8)
       : u.startsWith('http://')  ? 'https://' + u.slice(7) : null;
}

async function lookup(task, nexusUrl) {
  const url = ajaxUrl(nexusUrl, task);
  const diag = { url: url, step: 'starting', tried: [] };
  if (!/^\d{3,}$/.test(String(task || '')))
    return { ok: false, error: 'not a task number', diag: diag };

  /* Try the address as given, then the other scheme. An internal site is often
     only on one of the two, and a background fetch cannot click through a
     certificate warning the way a tab can - both show up as "Failed to fetch". */
  let res = null, used = null;
  diag.step = 'fetching';
  const t0 = Date.now();
  for (const attempt of [url, flipScheme(url)]) {
    if (!attempt) continue;
    try {
      res = await fetch(attempt, {
        credentials: 'include',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      used = attempt;
      diag.tried.push(attempt + ' -> ' + res.status);
      break;
    } catch (e) {
      diag.tried.push(attempt + ' -> ' + String((e && e.message) || e));
      res = null;
    }
  }
  if (!res) {
    diag.step = 'could not connect on either http or https';
    return { ok: false,
             error: 'could not reach Nexus at ' + url +
                    ' (nor over the other scheme). Open that address in a tab: if it ' +
                    'warns about the certificate, that is what blocks this.',
             diag: diag };
  }
  diag.url = used;
  diag.ms = Date.now() - t0;
  diag.status = res.status;
  diag.type = res.headers.get('content-type') || '';
  diag.finalUrl = res.url;
  if (!res.ok) {
    diag.step = 'bad status';
    return { ok: false, error: 'Nexus answered ' + res.status, diag: diag };
  }

  const text = (await res.text()).trim();
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
