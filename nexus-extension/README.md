# Timecard → Nexus (browser extension)

Two things, both **in the browser you are already using**:

1. **The 🚀** fills the Nexus hours form for that row, in a tab right next to
   the timecard. No second window, no Node.js, no helper in the background.
2. **Task lookup** — type a 6-digit task number in the timecard and its
   **project** and **contractor** are pulled from Nexus and filled in. Job type
   stays yours to pick.

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
5. Click **Details** on "Timecard → Nexus" and turn on
   **Allow access to file URLs**.

It shows up under the same navy **N** as the button in the timecard, so it is
easy to pick out in a list of extensions.

Step 5 matters: the timecard is a file on your disk, and without that tick the
extension is not allowed to talk to it, so the task lookup stays silent. The 🚀
works either way. Settings tells you which state you are in — it says
*"extension found — lookups will work"* once the tick is on and the page has
been reloaded.

Then just press a 🚀, or type a task number.

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

The folder holds nine files: `manifest.json`, `background.js`, `content.js`,
`page.js`, `bridge.js`, this README, and `icon16/32/48/128.png` — the navy N.
They all have to sit in the one folder, or the browser refuses to load it.

Every step waits for the thing it needs to appear rather than counting
seconds, so a slow, US-hosted Nexus just means a longer wait — never a
misplaced click.

The job is stripped out of the address bar as soon as it's read, so
refreshing the page doesn't re-run it.

## What it can touch

- It runs **only** on `nexus.tcs.local` and on local files. Every other site is
  out of scope — enforced by the browser from the manifest, not by the code.
- Its only permission is to reach `nexus.tcs.local`. No history, no tabs, no
  storage, no other site.
- The lookup uses the same address Nexus's own Edit button uses
  (`php/ajax.php?function=get_project_info`), with your own cookies. It sees
  exactly what you can see when you search that task, and nothing else.
- On a local file it only ever answers the timecard's own messages, so on any
  other local page it does nothing.
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

## The task lookup

Type a 6-digit task number and the row's **project** and **contractor** fill
themselves in a moment later. Anything you have already typed is left alone —
it only fills boxes that are empty, and it never touches the job type.

The answers are remembered per task number, so the same task fills instantly
next time even without Nexus.

Turn it off in the timecard's Settings if you would rather type them yourself.

**"extension not detected on this page"** in Settings means either the
extension is not installed, or **Allow access to file URLs** is off (see step 5
above). Reload the timecard after turning it on.

**"sign in to Nexus first"** means your Nexus session has expired. Open Nexus,
sign in, then type the number again.
