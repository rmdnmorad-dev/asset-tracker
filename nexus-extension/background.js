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
async function lookup(task, nexusUrl) {
  const url = ajaxUrl(nexusUrl, task);
  const diag = { url: url, step: 'starting' };
  if (!/^\d{3,}$/.test(String(task || '')))
    return { ok: false, error: 'not a task number', diag: diag };

  let res;
  diag.step = 'fetching';
  const t0 = Date.now();
  try {
    res = await fetch(url, {
      credentials: 'include',
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    });
  } catch (e) {
    diag.step = 'fetch threw';
    diag.threw = String((e && e.message) || e);
    return { ok: false,
             error: 'could not reach ' + url + ' — ' + diag.threw, diag: diag };
  }
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
