/*  Timecard → Nexus  ·  bridge
 *
 *  Sits on the Timecard page. The Timecard is a local file and cannot reach
 *  Nexus itself, so it asks through here: the page posts a message, this
 *  passes it to the extension, and the answer comes back the same way.
 *
 *  It only ever answers messages of our own shape, so on any other page it
 *  does nothing at all.
 */
(function () {
  'use strict';

  function answer(id, payload) {
    window.postMessage(Object.assign({ __tc: 'lookupResult', id: id }, payload), '*');
  }

  window.addEventListener('message', function (e) {
    if (e.source !== window) return;                   // only this page, not frames
    const d = e.data;
    if (!d || d.__tc !== 'lookup' || !d.task) return;

    try {
      chrome.runtime.sendMessage(
        { type: 'tcLookup', task: d.task, nexusUrl: d.nexusUrl },
        function (res) {
          if (chrome.runtime.lastError) {
            answer(d.id, { ok: false, error: 'the extension was reloaded — refresh this page' });
            return;
          }
          answer(d.id, res || { ok: false, error: 'no answer from the extension' });
        });
    } catch (err) {
      answer(d.id, { ok: false, error: 'the extension was reloaded — refresh this page' });
    }
  });

  // Let the Timecard know a lookup is available, so it can say so in Settings.
  window.postMessage({ __tc: 'ready' }, '*');
})();
