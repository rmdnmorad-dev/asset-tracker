# Timecard → Nexus (browser extension)

Two things, both **in the browser you are already using**:

1. **The 🚀** fills the Nexus hours form for that row, in a tab right next to
   the timecard. No second window, no Node.js, no helper in the background.
2. **Task lookup** — type a task number in the timecard — six digits, or six
   digits and a revision like `300042-1` — and its
   **project**, **contractor** and **job type** are pulled from Nexus and
   filled in. Job type is the task's own description — the *Description*
   column in Nexus's task list, not the note on the Hours tab. Only a field
   that really is a description is used: a category such as *TaskType* or
   *Deliverable* is never taken for one, and if the answer holds no
   description then JOB TYPE is left empty for you rather than filled with the
   wrong thing.

   **Ctrl/⌘ + click the N** on any row to see exactly what Nexus answers for
   that task, field by field, with a Copy button. That is the place to look if
   job type stays empty — the field that holds the description will be in that
   list.

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

The 🚀 hands that row's task, milestone, hours, date and notes to the Nexus tab
you already have open, and brings it to the front — no reload, and no pile of
duplicate tabs however many times you press it. Only when there is no Nexus tab
at all does it open one, at
`nexus.tcs.local/protected.php#tcjob=…`. The extension is the only thing that
reads that, and it only runs on Nexus pages.

Two files, for one reason: a content script runs in an isolated world and
cannot see the page's own jQuery, which the form needs. So `content.js` reads
the job and injects `page.js` into the page itself, where jQuery is real.

The folder holds nine files: `manifest.json`, `background.js`, `content.js`,
`page.js`, `bridge.js`, this README, and `icon16/32/48/128.png` — the navy N.
They all have to sit in the one folder, or the browser refuses to load it.

**Revisions.** Nexus lists a task and its revisions as separate rows — 300042,
300042-1, 300042-2 — each with its own description. They are separate tasks, so
the row you write is the row you get: `300042` looks up and opens 300042, and
`300042-1` looks up and opens 300042-1. Nothing hunts for a newer one.

Asking `get_project_info` for `300042-2` does not work: it answers with the
parent task, because the displayed number is not the id Nexus files that row
under. The list knows the real one — it is on the row's own Edit button — so
whenever a Nexus tab has the task on screen, the extension reads each row's id
and description straight off the list and asks with **that**. Nothing is clicked
or searched to do it; the page is only read.

So a task already listed in your Nexus tab is right the first time, and one that
is not becomes right the moment it appears — pressing 🚀 lists it, and rows
already filled in on the timecard correct themselves within a few seconds.
Whichever way, each revision ends up with its own project, contractor and
description, and what is learned is remembered.

Every step waits for the thing it needs to appear rather than counting
seconds, so a slow, US-hosted Nexus just means a longer wait — never a
misplaced click.

The job is stripped out of the address bar as soon as it's read, so
refreshing the page doesn't re-run it.

## What it can touch

- It runs **only** on `nexus.tcs.local` and on local files. Every other site is
  out of scope — enforced by the browser from the manifest, not by the code.
- Its permissions are: reach `nexus.tcs.local`; see your tabs, so it can find
  the Nexus one you already have open instead of piling up new ones; and its
  own local storage, which holds nothing but task numbers and the descriptions
  read off Nexus's own task list. No history, no bookmarks, no other site.
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

Type a task number — `300042`, or `300042-1` for a revision — and the row's
**project**, **contractor** and **job type** fill themselves in a moment later,
from that exact row.

It does not matter how the number arrives: typed and entered, pasted into one
cell, or a whole column of numbers pasted at once — the sheet is swept and
every task on it that is still missing information is looked up, one after
another. Put a different number in a row and the previous task's answers are
cleared out first, so a row never keeps information belonging to a task that is
no longer in it. Anything you typed yourself is left exactly as you wrote it.

**No Nexus tab open?** Nothing can be looked up — this page is a local file and
the tab is what carries your session. So the N buttons turn red and a line
appears above the sheet saying so; click either one and Nexus opens. When a tab
is there, the N goes back to navy and the tasks that could not be looked up are
picked up automatically.

The answers are remembered per task number, so the same task fills instantly
next time even without Nexus.

**"extension not detected on this page"** in Settings means either the
extension is not installed, or **Allow access to file URLs** is off (see step 5
above). Reload the timecard after turning it on.

**"sign in to Nexus first"** means your Nexus session has expired. Open Nexus,
sign in, then type the number again.
