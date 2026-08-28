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

async function lookup(task, nexusUrl) {
  if (!/^\d{3,}$/.test(String(task || ''))) return { ok: false, error: 'not a task number' };
  let res;
  try {
    res = await fetch(ajaxUrl(nexusUrl, task), {
      credentials: 'include',
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    });
  } catch (e) {
    return { ok: false, error: 'Nexus did not answer — are you on the company network?' };
  }
  if (!res.ok) return { ok: false, error: 'Nexus answered ' + res.status };

  const text = (await res.text()).trim();
  if (!text) return { ok: false, error: 'task ' + task + ' not found' };
  // A login page instead of JSON means the session has expired.
  if (/^\s*</.test(text)) return { ok: false, error: 'sign in to Nexus first, then try again' };

  let data;
  try { data = JSON.parse(text); }
  catch (e) { return { ok: false, error: 'Nexus sent something unreadable' }; }

  const info = pick(data);
  return info ? { ok: true, data: info }
              : { ok: false, error: 'task ' + task + ' not found' };
}

chrome.runtime.onMessage.addListener((msg, sender, reply) => {
  if (!msg || msg.type !== 'tcLookup') return;
  lookup(msg.task, msg.nexusUrl).then(reply);
  return true;                                          // reply arrives asynchronously
});
