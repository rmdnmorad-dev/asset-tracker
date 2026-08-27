/*  Timecard → Nexus desktop helper
 *
 *  Runs on YOUR PC. It keeps a real Chrome window open and, whenever you press a
 *  rocket (🚀) in the Timecard, it drives that window: opens the task, opens the
 *  Hours tab and fills milestone / hours / date / description.
 *
 *  It never presses Submit — you check the form and submit it yourself.
 *
 *  Why this works where the in-browser script struggled: Playwright waits for an
 *  element to actually exist, be visible, be enabled and stop moving before it
 *  clicks. A slow, far-away server just means it waits longer.
 *
 *  Start it with:  START-HELPER.bat   (or:  node nexus-helper.js)
 */

'use strict';

const http = require('http');
const path = require('path');
const fs = require('fs');

// ---------------------------------------------------------------- settings
const PORT = 8765;
const NEXUS_URL = process.env.NEXUS_URL || 'http://nexus.tcs.local/protected.php';
const PROFILE_DIR = path.join(__dirname, 'chrome-profile');   // keeps you logged in
const SLOW = {                                                // generous: the server is in the US
  page: 180000,      // first load / logging in
  element: 120000    // any single element
};

// ---------------------------------------------------------------- pretty log
const t = () => new Date().toLocaleTimeString();
const log  = (m) => console.log('  ' + t() + '  ' + m);
const ok   = (m) => console.log('  ' + t() + '  \x1b[32m' + m + '\x1b[0m');
const warn = (m) => console.log('  ' + t() + '  \x1b[33m' + m + '\x1b[0m');
const errl = (m) => console.log('  ' + t() + '  \x1b[31m' + m + '\x1b[0m');

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch (e) {
  console.error('\n  Playwright is not installed yet.\n' +
                '  Open a command prompt in this folder and run:   npm install\n');
  process.exit(1);
}

// ---------------------------------------------------------------- browser
let ctx = null, page = null;

async function browser() {
  if (ctx && page && !page.isClosed()) return page;
  log('opening Chrome…');
  try {
    ctx = await chromium.launchPersistentContext(PROFILE_DIR, {
      headless: false,
      channel: 'chrome',            // uses the Chrome already installed on this PC
      viewport: null,
      args: ['--start-maximized']
    });
  } catch (e) {
    warn('could not use installed Chrome (' + e.message.split('\n')[0] + ') — trying bundled browser');
    ctx = await chromium.launchPersistentContext(PROFILE_DIR, {
      headless: false, viewport: null, args: ['--start-maximized']
    });
  }
  ctx.setDefaultTimeout(SLOW.element);
  page = ctx.pages()[0] || await ctx.newPage();
  ctx.on('close', () => { ctx = null; page = null; });
  return page;
}

// Make sure we are on protected.php and signed in. If a login is needed this
// simply waits — you log in once and the profile remembers it afterwards.
async function ensureNexus(p) {
  const onNexus = p.url().indexOf('protected.php') !== -1;
  if (!onNexus) {
    log('loading Nexus…');
    await p.goto(NEXUS_URL, { waitUntil: 'domcontentloaded', timeout: SLOW.page });
  }
  const search = p.locator('input.task_search');
  if (!(await search.count()) || !(await search.first().isVisible().catch(() => false))) {
    warn('waiting for Nexus to be ready (log in if it is asking you to)…');
    await search.first().waitFor({ state: 'visible', timeout: SLOW.page });
  }
  return p;
}

// ---------------------------------------------------------------- one job
async function runJob(job) {
  const task = String(job.task || '').trim();
  console.log('');
  log('── job: task ' + task + ' · ' + (job.milestone || '?') + ' · ' +
      (job.hours || '0') + ' h · ' + (job.date || '') + ' ──');

  const p = await browser();
  await ensureNexus(p);

  // If a task window is already open from a previous job, close it first.
  const openModal = p.locator('#EditProject .nav-tabs');
  if (await openModal.count() && await openModal.first().isVisible().catch(() => false)) {
    log('closing the previous task window');
    await p.keyboard.press('Escape').catch(() => {});
    await p.waitForTimeout(800);
  }

  // 1 ── find the task. Type it in the search box; Playwright then waits for the
  //      row's own Edit button to appear, however long the server takes.
  log('searching for ' + task + '…');
  const search = p.locator('input.task_search').first();
  await search.click();
  await search.fill('');
  await search.type(task, { delay: 60 });
  await search.press('Enter').catch(() => {});

  const editBtn = p.locator('button.btn-edit[data-row="' + task + '"]').first();
  log('waiting for the task row (server is remote, this can take a while)…');
  try {
    await editBtn.waitFor({ state: 'visible', timeout: SLOW.element });
  } catch (e) {
    // Fall back to Nexus's own handler, which needs only the row id.
    warn('row did not appear — asking Nexus to open the task directly');
    await p.evaluate((id) => {
      const b = document.createElement('button');
      b.className = 'btn btn-primary btn-sm btn-edit';
      b.setAttribute('data-row', id);
      b.style.cssText = 'position:fixed;left:-9999px;top:0';
      document.body.appendChild(b);
      b.click();
      setTimeout(() => b.remove(), 2000);
    }, task);
  }
  if (await editBtn.isVisible().catch(() => false)) {
    log('opening the task…');
    await editBtn.click();
  }

  // 2 ── the task window (loaded by AJAX)
  log('waiting for the task window…');
  const hoursTab = p.locator('#EditProject a[data-target="#second"]').first();
  await hoursTab.waitFor({ state: 'visible', timeout: SLOW.element });

  // 3 ── Hours tab (the form lives inside it)
  log('opening the Hours tab…');
  await hoursTab.click();

  const form = p.locator('#form-input_hours').first();
  await form.waitFor({ state: 'visible', timeout: SLOW.element });

  // 4 ── fill: milestone first, then hours, date, description
  if (job.milestone) {
    const sel = form.locator('select[name="ms_select"]').first();
    await sel.waitFor({ state: 'visible', timeout: SLOW.element });
    try {
      await sel.selectOption({ label: job.milestone });
    } catch (e) {
      await sel.selectOption(job.milestone).catch(async () => {
        warn('milestone "' + job.milestone + '" not in the list — leaving it as it is');
      });
    }
    // Nexus does not copy the option's data-ms-id into the hidden field itself
    await p.evaluate(() => {
      const f = document.querySelector('#form-input_hours');
      if (!f) return;
      const s = f.querySelector('select[name="ms_select"]');
      const h = f.querySelector('input[name="ts_ms_id"]');
      if (s && h && s.selectedIndex >= 0) {
        const id = s.options[s.selectedIndex].getAttribute('data-ms-id');
        if (id) h.value = id;
      }
    });
    log('milestone set to ' + job.milestone);
  }

  if (job.hours) {
    const h = form.locator('input[name="ts_hours"]').first();
    await h.fill(String(job.hours));
    log('hours = ' + job.hours);
  }

  if (job.date) {
    // the date box carries a jQuery datepicker, so set it directly and close the picker
    await p.evaluate((d) => {
      const el = document.querySelector('#form-input_hours input[name="ts_date"]');
      if (!el) return;
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
      setter.call(el, d);
      ['input', 'change', 'blur'].forEach(t => el.dispatchEvent(new Event(t, { bubbles: true })));
      if (window.jQuery) { try { window.jQuery(el).datepicker('hide'); } catch (e) {} }
      const dp = document.getElementById('ui-datepicker-div');
      if (dp) dp.style.display = 'none';
    }, String(job.date));
    log('date = ' + job.date);
  }

  if (job.desc) {
    const d = form.locator('textarea[name="ts_emp_description"]').first();
    await d.fill(String(job.desc));
    log('description filled');
  }

  await form.scrollIntoViewIfNeeded().catch(() => {});
  await p.evaluate(() => {
    const f = document.querySelector('#form-input_hours');
    if (f) { f.style.outline = '3px solid #16a34a'; f.style.outlineOffset = '3px'; }
  });
  await p.bringToFront().catch(() => {});
  ok('READY — check the form in Chrome and press Submit yourself.');
}

// ---------------------------------------------------------------- queue
let queue = [], busy = false;
async function pump() {
  if (busy || !queue.length) return;
  busy = true;
  const job = queue.shift();
  try { await runJob(job); }
  catch (e) {
    errl('could not finish: ' + (e && e.message ? e.message.split('\n')[0] : e));
    errl('the Chrome window is still open — finish that one by hand.');
  }
  busy = false;
  if (queue.length) pump();
}

// ---------------------------------------------------------------- listener
const GIF = Buffer.from('R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7', 'base64');

http.createServer((req, res) => {
  const u = new URL(req.url, 'http://127.0.0.1');
  if (u.pathname === '/ping' || u.pathname === '/job') {
    const raw = u.searchParams.get('d');
    if (raw) {
      let job = null;
      try { job = JSON.parse(raw); } catch (e) { errl('unreadable job from the Timecard'); }
      if (job && job.task) { queue.push(job); log('got a job from the Timecard — queued'); setImmediate(pump); }
    }
    res.writeHead(200, { 'Content-Type': 'image/gif', 'Access-Control-Allow-Origin': '*', 'Cache-Control': 'no-store' });
    return res.end(GIF);
  }
  res.writeHead(404, { 'Access-Control-Allow-Origin': '*' });
  res.end();
}).listen(PORT, '127.0.0.1', () => {
  console.log('\n  ┌────────────────────────────────────────────────────────┐');
  console.log('  │  Timecard → Nexus helper is running                    │');
  console.log('  │                                                        │');
  console.log('  │  Leave this window open.                               │');
  console.log('  │  Press a 🚀 in the Timecard and watch Chrome.          │');
  console.log('  │  It never presses Submit — you do that.                │');
  console.log('  └────────────────────────────────────────────────────────┘');
  log('listening on http://127.0.0.1:' + PORT);
  log('Nexus: ' + NEXUS_URL);
  if (!fs.existsSync(PROFILE_DIR)) log('first run: Chrome will open — sign in to Nexus once and it will be remembered.');
});

process.on('unhandledRejection', (e) => errl('background error: ' + (e && e.message ? e.message : e)));
