/*  Timecard → Nexus  ·  content script
 *
 *  Runs in the Nexus tab that the Timecard's 🚀 opened. Its only jobs are to
 *  read the job off the URL and hand it to page.js.
 *
 *  Why two files: a content script lives in an isolated world and cannot see
 *  the page's own jQuery, which the fill routine needs. page.js is injected
 *  into the page itself, where jQuery is real.
 */
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

  function inject() {
    var s = document.createElement('script');
    s.src = chrome.runtime.getURL('page.js');
    s.dataset.job = JSON.stringify(job);
    s.onload = function () { s.remove(); };
    (document.head || document.documentElement).appendChild(s);
  }

  if (document.documentElement) inject();
  else document.addEventListener('readystatechange', inject, { once: true });
})();
