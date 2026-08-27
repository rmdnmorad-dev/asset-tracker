# Timecard → Nexus helper

A small program that copies one line of your Timecard into the Nexus hours
form for you, so you don't have to retype it.

It fills the form in and then **stops**. You read it and press Submit
yourself. It never submits anything on its own.

---

## What it actually does

You press the 🚀 next to a task number in the Timecard. The helper then does,
in your browser, exactly what you would have done by hand:

1. Opens Nexus in a new tab (or reuses the Nexus tab you already have open).
2. Searches for the 6-digit task number.
3. Waits for the results, then clicks **Edit** on that task.
4. Waits for the Edit window, then opens the **Hours** tab.
5. Picks the **milestone** you chose for that task.
6. Fills in **Hours**, **Date** and **Description** from that Timecard row.
7. Outlines the form green and stops.

Step 7 is the whole point: nothing is saved to Nexus until you press Submit.

Every wait above is a real wait — it watches for the thing to appear rather
than counting seconds — so a slow day on the US-hosted server just means it
waits longer, not that it clicks the wrong thing or gives up.

---

## What it is not

- It is **not** a Nexus login, account, or integration. It has no Nexus
  credentials of its own and cannot get any. It types into a page that
  *you* are already signed into, in your own browser session.
- It **never presses Submit**, so it cannot file hours you have not read.
- It **sends nothing anywhere.** No internet calls, no telemetry, no cloud.
  The only network traffic is your browser talking to Nexus exactly as it
  does when you click things yourself.
- It **installs nothing.** No npm, no downloads, no libraries. It uses only
  what already ships inside Node.js.

## What it can touch

Worth being precise, since it does drive a browser:

- It only ever types into a **Nexus** tab. Any other tab — your Timecard,
  your mail — is never navigated, read, or altered.
- It runs the browser on a **separate profile** (a `chrome-profile` folder
  next to the helper). Your normal browser profile, passwords, history and
  open tabs are untouched. This separation is also required: since Chrome
  136, browsers refuse to enable automation on your default profile.
- It listens on `127.0.0.1:8765` — your own machine only. It is not
  reachable from the network, and the only thing it accepts is a job from
  the Timecard page.

---

## How it works

The Timecard is a web page, and a web page is not allowed to reach into
another site's tab — that is a browser security rule, and a good one. So the
Timecard cannot drive Nexus by itself.

Instead:

```
Timecard page  ──job──▶  helper (Node.js, your PC)  ──DevTools Protocol──▶  browser tab
   press 🚀              127.0.0.1:8765                                     Nexus
```

The helper starts your browser with its debugging port enabled and talks to
it over the DevTools Protocol — the same channel the browser's own developer
tools use. That is what lets it wait for real page events instead of
guessing.

It uses **whatever browser Windows has set as your default**. If that browser
can't be automated (Firefox has no DevTools Protocol) or refuses to open a
debugging port, it falls back to whichever of Chrome, Edge, Opera, Brave or
Vivaldi is installed, and tells you which one it picked.

---

## Setup

1. Install Node.js from <https://nodejs.org> (the LTS button). One time only.
2. Put `nexus-helper.js`, `START-HELPER.bat` and your `Timecard*.html` in the
   same folder.
3. Double-click `START-HELPER.bat`. Leave the window open and minimise it.
4. Your browser opens with the Timecard in the first tab. Sign in to Nexus
   once in that window — it is remembered from then on.
5. Press a 🚀.

The helper window is the log. It prints what it is doing and what it filled,
which is where to look if a task behaves oddly.

### Settings

Set these in `START-HELPER.bat` if the defaults don't suit you:

| Variable | What it does |
| --- | --- |
| `BROWSER_PATH` | Force one browser instead of your default |
| `TIMECARD` | Point at one exact Timecard file |
| `NEXUS_URL` | Use a different Nexus address |
| `NEXUS_PROFILE` | Put the browser profile folder somewhere else |

---

## If something goes wrong

**The window closes instantly.** Open Command Prompt and run it from there so
the window can't vanish:

```
cd /d "C:\path\to\the\folder"
START-HELPER.bat
```

**"I cannot find nexus-helper.js".** The `.bat` and the `.js` are not in the
same folder. The message lists what is actually in there.

**"your default browser is Firefox, which cannot be automated".** Expected —
Firefox has no DevTools Protocol. It will use another browser instead, or set
`BROWSER_PATH` to choose one.

**The rocket does nothing.** The helper window is closed, or it printed an
error. Check that window first.
