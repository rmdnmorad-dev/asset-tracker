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
