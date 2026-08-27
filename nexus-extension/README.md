# Timecard → Nexus (browser extension)

Fills the Nexus hours form from a Timecard 🚀 press — **in the browser you are
already using**, in a tab right next to the timecard. No second window, no
Node.js, no helper running in the background.

It never presses Submit. You do that.

---

## Install (once, about 30 seconds)

Works in Opera, Chrome, Edge, Brave and Vivaldi — they all take the same
extension.

1. Put the `nexus-extension` folder somewhere permanent. **Don't delete it
   afterwards** — the browser loads the extension from this folder every time
   it starts. Somewhere like `Documents\nexus-extension` is fine; the Downloads
   folder is not.
2. Open your browser's extensions page:
   - Opera — `opera://extensions`
   - Chrome — `chrome://extensions`
   - Edge — `edge://extensions`
   - Brave — `brave://extensions`
   - Vivaldi — `vivaldi://extensions`
3. Turn on **Developer mode** (top-right in most, bottom-left in Edge).
4. Click **Load unpacked** and pick the `nexus-extension` folder.
5. That's it. "Timecard → Nexus" appears in the list.

Then just press a 🚀 in the timecard.

## What you'll see

Nexus opens in a new tab beside the timecard, and a small panel in the
bottom-right corner tells you what is happening:

```
🚀 Timecard → Nexus
waiting for Nexus to finish loading…
opening task 303905…
waiting for the task window…
opening the Hours tab…
Filled milestone, hours, date, description.
Check it and press Submit yourself.
```

The panel turns green when it's done and fades out. If anything goes wrong it
turns red and says what failed — it never fails silently.

The Hours form is outlined in green so you can see exactly what was touched.

---

## How it works

The 🚀 opens `nexus.tcs.local/protected.php#tcjob=…` with that row's task,
milestone, hours, date and notes in the URL. The extension is the only thing
that reads that, and it only runs on Nexus pages.

Two files, for one reason: a content script runs in an isolated world and
cannot see the page's own jQuery, which the form needs. So `content.js` reads
the job and injects `page.js` into the page itself, where jQuery is real.

Every step waits for the thing it needs to appear rather than counting
seconds, so a slow, US-hosted Nexus just means a longer wait — never a
misplaced click.

The job is stripped out of the address bar as soon as it's read, so
refreshing the page doesn't re-run it.

## What it can touch

- It runs **only** on `nexus.tcs.local`. Every other site is out of scope —
  that's enforced by the browser from the manifest, not by the code.
- It asks for **no permissions**: no history, no tabs, no storage, no network
  access.
- It **never presses Submit**, so it cannot file hours you have not read.
- It sends nothing anywhere. Everything happens inside the page you are
  looking at.

---

## If something goes wrong

**The tab opens but nothing fills.** The extension isn't installed or isn't
enabled — check your extensions page. If you moved or deleted the folder after
loading it, the browser silently drops it; load it again from a permanent
location.

**"Your browser blocked the new tab."** Allow pop-ups for the timecard page,
then press the rocket again.

**The panel goes red.** It says which step failed. "Nexus never finished
loading its scripts" usually means you're signed out — sign in and press the
rocket again.

**Your Nexus is on a different address.** Change it in the timecard's Settings,
and edit the two `matches` lines in `manifest.json` to the same host, then
reload the extension.
