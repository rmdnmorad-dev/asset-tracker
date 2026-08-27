/*  Timecard → Nexus helper  (v2 — no dependencies, no npm)
 *
 *  Runs on YOUR PC with nothing installed but Node.js and a browser.
 *  It uses whatever browser Windows has set as your default. If that browser
 *  cannot be driven (Firefox has no DevTools Protocol) or refuses to open a
 *  debugging port, it falls back to whichever of Chrome, Edge, Opera, Brave
 *  or Vivaldi is on the machine.
 *  It talks to the browser over the DevTools Protocol, using only Node
 *  built-ins (http, net, crypto).
 *
 *  Press a 🚀 in the Timecard and it opens the task, opens the Hours tab and
 *  fills milestone / hours / date / description.  It never presses Submit.
 *
 *  Start it with:  START-HELPER.bat   (or:  node nexus-helper.js)
 */

'use strict';

const http = require('http');
const net = require('net');
const crypto = require('crypto');
const path = require('path');
const fs = require('fs');
const os = require('os');
const { spawn, spawnSync } = require('child_process');

// ---------------------------------------------------------------- settings
const PORT = 8765;
const CDP_PORT = 9222;
const NEXUS_URL = process.env.NEXUS_URL || 'http://nexus.tcs.local/protected.php';
const PROFILE_DIR = process.env.NEXUS_PROFILE || path.join(__dirname, 'chrome-profile');

const t = () => new Date().toLocaleTimeString();
const log  = (m) => console.log('  ' + t() + '  ' + m);
const ok   = (m) => console.log('  ' + t() + '  \x1b[32m' + m + '\x1b[0m');
const warn = (m) => console.log('  ' + t() + '  \x1b[33m' + m + '\x1b[0m');
const errl = (m) => console.log('  ' + t() + '  \x1b[31m' + m + '\x1b[0m');
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

// ------------------------------------------------- your default browser
/* We drive whatever browser Windows says is your default. Everything else in
   the list below is only a fallback, for when the default cannot be driven. */

// Browsers that are not Chromium underneath. They have no DevTools Protocol,
// so there is nothing to drive - don't waste time launching them.
const NOT_CHROMIUM = /(firefox|waterfox|librewolf|palemoon|seamonkey|iexplore|safari|tor)/i;

let defaultBrowser = null;    // { exe, name, drivable } once looked up

/* The rocket reaches us as an HTTP request from the Timecard page, and that
   request says which browser you pressed it in. That beats guessing. */
let pressedIn = null;         // e.g. 'opera' - a filename fragment to match

function brandFromUA(ua) {
  if (!ua) return null;
  if (/\bOPR\//.test(ua))     return 'opera';     // Opera and Opera GX
  if (/\bEdg\//.test(ua))     return 'msedge';
  if (/Vivaldi/i.test(ua))    return 'vivaldi';
  if (/Firefox\//.test(ua))   return 'firefox';   // not drivable, but worth naming
  if (/Chrome\//.test(ua))    return 'chrome';    // Brave looks like Chrome too
  if (/Safari\//.test(ua))    return 'safari';
  return null;
}

function regQuery(key, args) {
  try {
    const r = spawnSync('reg', ['query', key].concat(args), { encoding: 'utf8', windowsHide: true });
    return (r.status === 0 && r.stdout) ? r.stdout : null;
  } catch (e) { return null; }
}

function exeFromCommand(cmd) {
  if (!cmd) return null;
  const quoted = cmd.match(/"([^"]+\.exe)"/i);   // "C:\...\opera.exe" -- "%1"
  if (quoted) return quoted[1];
  const bare = cmd.match(/^\s*(\S+\.exe)/i);     //  C:\...\chrome.exe -- "%1"
  return bare ? bare[1] : null;
}

/* Windows records the default browser as a ProgId (OperaStable, ChromeHTML,
   MSEdgeHTM, FirefoxURL …); the ProgId then points at the real .exe. */
function findDefaultBrowser() {
  if (process.platform === 'win32') {
    for (const scheme of ['https', 'http']) {
      const out = regQuery('HKCU\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\' +
                           scheme + '\\UserChoice', ['/v', 'ProgId']);
      const m = out && out.match(/ProgId\s+REG_SZ\s+(\S+)/i);
      if (!m) continue;
      const progId = m[1];
      for (const root of ['HKCU\\Software\\Classes\\', 'HKCR\\']) {
        const c = regQuery(root + progId + '\\shell\\open\\command', ['/ve']);
        const line = c && c.match(/REG_SZ\s+(.+)/i);
        const exe = exeFromCommand(line && line[1].trim());
        if (exe && fs.existsSync(exe)) return { exe, name: progId, drivable: !NOT_CHROMIUM.test(exe) };
      }
    }
    return null;
  }
  // Linux: ask xdg, then read the .desktop file it names.
  try {
    const r = spawnSync('xdg-settings', ['get', 'default-web-browser'], { encoding: 'utf8' });
    const desktop = (r.stdout || '').trim();
    if (desktop) {
      for (const d of ['/usr/share/applications/', (process.env.HOME || '') + '/.local/share/applications/']) {
        const f = path.join(d, desktop);
        if (!fs.existsSync(f)) continue;
        const ex = fs.readFileSync(f, 'utf8').match(/^Exec=(\S+)/m);
        if (!ex) continue;
        let exe = ex[1];
        if (!exe.includes('/')) { const w = spawnSync('which', [exe], { encoding: 'utf8' });
                                  exe = (w.stdout || '').trim(); }
        if (exe && fs.existsSync(exe)) return { exe, name: desktop, drivable: !NOT_CHROMIUM.test(exe) };
      }
    }
  } catch (e) {}
  return null;
}

function browserCandidates() {
  const out = [];
  const add = (p) => { try { if (p && fs.existsSync(p) && out.indexOf(p) < 0) out.push(p); } catch (e) {} };
  // whatever you point us at wins
  add(process.env.BROWSER_PATH); add(process.env.CHROME_PATH);

  // then your actual default browser, whatever it happens to be
  if (!defaultBrowser) defaultBrowser = findDefaultBrowser() || { none: true };
  if (defaultBrowser.exe && defaultBrowser.drivable) add(defaultBrowser.exe);

  if (process.platform === 'win32') {
    const LA = process.env['LOCALAPPDATA'], PF = process.env['PROGRAMFILES'], PX = process.env['PROGRAMFILES(X86)'];
    // Opera keeps opera.exe either directly in the install folder or in a
    // version-numbered subfolder, so look in both.
    const operaHomes = [];
    [LA, PF, PX].forEach(root => { if (!root) return;
      operaHomes.push(path.join(root, 'Programs\\Opera'), path.join(root, 'Programs\\Opera GX'),
                      path.join(root, 'Opera'), path.join(root, 'Opera GX'));
    });
    const verNum = (s) => s.split('.').reduce((a, n) => a * 1000 + (+n || 0), 0);
    operaHomes.forEach(home => {
      add(path.join(home, 'opera.exe'));
      try {
        fs.readdirSync(home)
          .filter(sub => /^[0-9]+(\.[0-9]+)*$/.test(sub))
          .sort((a, b) => verNum(b) - verNum(a))          // newest Opera first
          .forEach(sub => add(path.join(home, sub, 'opera.exe')));
      } catch (e) {}
    });
    [LA, PF, PX].forEach(root => { if (!root) return;
      add(path.join(root, 'Google\\Chrome\\Application\\chrome.exe'));
      add(path.join(root, 'Microsoft\\Edge\\Application\\msedge.exe'));
      add(path.join(root, 'BraveSoftware\\Brave-Browser\\Application\\brave.exe'));
      add(path.join(root, 'Vivaldi\\Application\\vivaldi.exe'));
    });
  } else {
    ['/usr/bin/opera', '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
     '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
     '/Applications/Opera.app/Contents/MacOS/Opera',
     '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'].forEach(add);
  }

  /* The browser you actually pressed the rocket in goes to the front - it is
     the one you are working in, whatever the registry says is "default".
     An explicit BROWSER_PATH still outranks it. */
  if (pressedIn && !process.env.BROWSER_PATH && !process.env.CHROME_PATH) {
    const mine = out.filter(p => path.basename(p).toLowerCase().includes(pressedIn));
    if (mine.length) return mine.concat(out.filter(p => mine.indexOf(p) < 0));
  }
  return out;
}
function findChrome() { return browserCandidates()[0] || null; }


// ---------------------------------------------------------------- tiny CDP client
function httpJson(url, method) {
  return new Promise((resolve, reject) => {
    const u = new URL(url);
    const req = http.request({ hostname: u.hostname, port: u.port, path: u.pathname + u.search,
                               method: method || 'GET' }, res => {
      let b = '';
      res.on('data', d => b += d);
      res.on('end', () => { try { resolve(JSON.parse(b)); } catch (e) { reject(new Error(b.slice(0, 200))); } });
    });
    req.on('error', reject);
    req.end();
  });
}
function httpGetJson(url) { return httpJson(url, 'GET'); }

/* Chrome 111+ insists on PUT for /json/new; older builds only accept GET. */
function cdpNewTab(url) {
  const ep = 'http://127.0.0.1:' + CDP_PORT + '/json/new?' + encodeURIComponent(url);
  return httpJson(ep, 'PUT').catch(() => httpJson(ep, 'GET'));
}

// Minimal WebSocket client (RFC 6455, client → server frames are masked).
class WS {
  constructor(url) {
    const u = new URL(url);
    this.sock = null; this.buf = Buffer.alloc(0); this.open = false;
    this.frag = []; this.onmessage = null; this.u = u;
  }
  connect() {
    return new Promise((resolve, reject) => {
      const key = crypto.randomBytes(16).toString('base64');
      const s = net.connect(+this.u.port, this.u.hostname, () => {
        s.write('GET ' + this.u.pathname + this.u.search + ' HTTP/1.1\r\n' +
                'Host: ' + this.u.host + '\r\n' +
                'Upgrade: websocket\r\nConnection: Upgrade\r\n' +
                'Sec-WebSocket-Key: ' + key + '\r\nSec-WebSocket-Version: 13\r\n\r\n');
      });
      this.sock = s;
      let handshake = false, acc = Buffer.alloc(0);
      s.on('data', d => {
        if (!handshake) {
          acc = Buffer.concat([acc, d]);
          const i = acc.indexOf('\r\n\r\n');
          if (i < 0) return;
          const head = acc.slice(0, i).toString();
          if (!/101/.test(head.split('\r\n')[0])) return reject(new Error('websocket upgrade failed: ' + head.split('\r\n')[0]));
          handshake = true; this.open = true;
          this.buf = acc.slice(i + 4);
          this._drain();
          return resolve(this);
        }
        this.buf = Buffer.concat([this.buf, d]);
        this._drain();
      });
      s.on('error', reject);
      s.on('close', () => { this.open = false; });
    });
  }
  _drain() {
    for (;;) {
      const b = this.buf;
      if (b.length < 2) return;
      const fin = (b[0] & 0x80) !== 0, opcode = b[0] & 0x0f;
      let len = b[1] & 0x7f, off = 2;
      if (len === 126) { if (b.length < 4) return; len = b.readUInt16BE(2); off = 4; }
      else if (len === 127) { if (b.length < 10) return; len = Number(b.readBigUInt64BE(2)); off = 10; }
      if (b.length < off + len) return;
      const payload = b.slice(off, off + len);
      this.buf = b.slice(off + len);
      if (opcode === 0x8) { this.close(); return; }
      if (opcode === 0x9) { this._send(payload, 0xA); continue; }   // ping → pong
      if (opcode === 0xA) continue;
      this.frag.push(payload);
      if (fin) {
        const msg = Buffer.concat(this.frag).toString('utf8');
        this.frag = [];
        if (this.onmessage) { try { this.onmessage(msg); } catch (e) {} }
      }
    }
  }
  _send(payload, opcode) {
    if (!this.sock || !this.open) return;
    const mask = crypto.randomBytes(4);
    const len = payload.length;
    let head;
    if (len < 126) head = Buffer.from([0x80 | opcode, 0x80 | len]);
    else if (len < 65536) { head = Buffer.alloc(4); head[0] = 0x80 | opcode; head[1] = 0x80 | 126; head.writeUInt16BE(len, 2); }
    else { head = Buffer.alloc(10); head[0] = 0x80 | opcode; head[1] = 0x80 | 127; head.writeBigUInt64BE(BigInt(len), 2); }
    const masked = Buffer.alloc(len);
    for (let i = 0; i < len; i++) masked[i] = payload[i] ^ mask[i & 3];
    this.sock.write(Buffer.concat([head, mask, masked]));
  }
  send(text) { this._send(Buffer.from(text, 'utf8'), 0x1); }
  close() { this.open = false; try { this.sock.destroy(); } catch (e) {} }
}

class CDP {
  constructor(ws) {
    this.ws = ws; this.id = 0; this.pending = new Map();
    ws.onmessage = (raw) => {
      let m; try { m = JSON.parse(raw); } catch (e) { return; }
      if (m.id && this.pending.has(m.id)) {
        const { resolve, reject } = this.pending.get(m.id);
        this.pending.delete(m.id);
        if (m.error) reject(new Error(m.error.message || JSON.stringify(m.error)));
        else resolve(m.result);
      }
    };
  }
  send(method, params) {
    const id = ++this.id;
    this.ws.send(JSON.stringify({ id, method, params: params || {} }));
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      setTimeout(() => {
        if (this.pending.has(id)) { this.pending.delete(id); reject(new Error(method + ' timed out')); }
      }, 300000);
    });
  }
  // run an async function inside the page and get its value back
  async evalAsync(fnBody, timeoutMs) {
    const r = await this.send('Runtime.evaluate', {
      expression: '(async () => { ' + fnBody + ' })()',
      awaitPromise: true, returnByValue: true, timeout: timeoutMs || 300000
    });
    if (r.exceptionDetails) {
      const e = r.exceptionDetails;
      throw new Error((e.exception && (e.exception.description || e.exception.value)) || e.text || 'page error');
    }
    return r.result && r.result.value;
  }
}

// ---------------------------------------------------------------- browser
let chromeProc = null, cdp = null;

// Friendly name of whatever browser we ended up driving, for the messages.
let browserName = 'the browser';
function prettyName(exe) {
  const b = path.basename(exe).toLowerCase();
  if (b.includes('opera'))    return 'Opera';
  if (b.includes('msedge'))   return 'Edge';
  if (b.includes('brave'))    return 'Brave';
  if (b.includes('vivaldi'))  return 'Vivaldi';
  if (b.includes('chrom'))    return 'Chrome';
  if (b.includes('firefox'))  return 'Firefox';
  if (b.includes('safari'))   return 'Safari';
  return path.basename(exe);
}

/* The browser we start is the one the rocket opens its tabs in, so the
   Timecard wants to live in it too. Find it and use it as the start page. */
function findTimecard() {
  if (process.env.TIMECARD) {
    try { if (fs.statSync(process.env.TIMECARD).isFile()) return process.env.TIMECARD; } catch (e) {}
  }
  const dirs = [__dirname, path.dirname(__dirname), path.join(os.homedir(), 'Desktop'),
                path.join(os.homedir(), 'Downloads'), path.join(os.homedir(), 'Documents')];
  // If several Timecard files are lying around, the newest one is the one
  // being worked in - version numbers in the name are not reliable.
  let best = null, bestAge = -1;
  dirs.forEach(d => {
    try {
      fs.readdirSync(d).filter(f => /^timecard.*\.html?$/i.test(f)).forEach(f => {
        const p = path.join(d, f);
        try {
          const st = fs.statSync(p);
          if (st.isFile() && st.mtimeMs > bestAge) { bestAge = st.mtimeMs; best = p; }
        } catch (e) {}
      });
    } catch (e) {}
  });
  return best;
}

function startPage() {
  const tc = findTimecard();
  if (!tc) return NEXUS_URL;
  try { return require('url').pathToFileURL(tc).href; } catch (e) { return NEXUS_URL; }
}

async function chromeUp() {
  try { await httpGetJson('http://127.0.0.1:' + CDP_PORT + '/json/version'); return true; }
  catch (e) { return false; }
}

async function startChrome() {
  if (await chromeUp()) {
    if (pressedIn && browserName !== 'the browser' &&
        !browserName.toLowerCase().includes(pressedIn.replace('msedge', 'edge'))) {
      warn('you pressed the rocket in ' + prettyName(pressedIn) + ', but ' + browserName +
           ' is already open with debugging. Close that ' + browserName +
           ' window and press the rocket again to switch.');
    }
    log('a browser with debugging is already running — reusing it');
    return;
  }
  if (pressedIn && NOT_CHROMIUM.test(pressedIn))
    warn(prettyName(pressedIn) + ' cannot be automated (no DevTools Protocol) — using another browser instead');
  const list = browserCandidates();
  if (!list.length) throw new Error('No browser I can drive was found. Set BROWSER_PATH to your browser .exe and try again.');
  if (!fs.existsSync(PROFILE_DIR)) fs.mkdirSync(PROFILE_DIR, { recursive: true });

  const home = startPage();
  let lastErr = null;
  for (let i = 0; i < list.length; i++) {
    const exe = list[i];
    // A browser that can do this opens the port within seconds. Don't burn the
    // full wait on every candidate — only the last one gets the long grace.
    const grace = (i === list.length - 1) ? 30000 : 15000;
    log('starting ' + prettyName(exe) + ' …');
    let proc;
    try {
      proc = spawn(exe, [
        '--remote-debugging-port=' + CDP_PORT,
        '--user-data-dir=' + PROFILE_DIR,
        '--no-first-run', '--no-default-browser-check', '--start-maximized',
        home
      ], { detached: false, stdio: 'ignore' });
    } catch (e) { lastErr = e; warn('could not start it: ' + e.message); continue; }

    let died = false;
    proc.on('exit', () => { died = true; if (chromeProc === proc) { chromeProc = null; cdp = null; } });

    const t0 = Date.now();
    while (Date.now() - t0 < grace) {
      if (await chromeUp()) { chromeProc = proc; browserName = prettyName(exe); ok('using ' + exe); return; }
      if (died) break;
      await sleep(500);
    }
    warn(prettyName(exe) + ' did not open the debugging port — trying the next browser');
    try { proc.kill(); } catch (e) {}
    lastErr = new Error(prettyName(exe) + ' would not enable debugging');
  }
  throw new Error('No browser would start with debugging enabled. Tried: ' +
                  list.map(p => prettyName(p)).join(', ') +
                  (lastErr ? ' — last problem: ' + lastErr.message : ''));
}

/* Is this tab a Nexus tab? Anything that is not - your Timecard, your mail -
   must be left completely alone. We only ever take over a Nexus tab, and
   otherwise open a brand new one alongside whatever you already have open. */
let nexusHost = '';
try { nexusHost = new URL(NEXUS_URL).host; } catch (e) {}
function looksLikeNexus(u) {
  if (!u) return false;
  if (/protected\.php/i.test(u)) return true;
  try { return !!nexusHost && new URL(u).host === nexusHost; } catch (e) { return false; }
}

const tabs = () => httpGetJson('http://127.0.0.1:' + CDP_PORT + '/json')
                     .then(t => t.filter(x => x.type === 'page'))
                     .catch(() => []);

async function attach() {
  // Keep the existing connection only if that tab is still open and still Nexus.
  if (cdp && cdp.ws.open && cdp.targetId) {
    const still = (await tabs()).filter(x => x.id === cdp.targetId)[0];
    if (still && looksLikeNexus(still.url)) return cdp;
    try { cdp.ws.close(); } catch (e) {}
    cdp = null;
  }
  await startChrome();

  let page = (await tabs()).filter(x => looksLikeNexus(x.url))[0];
  if (page) {
    log('re-using the Nexus tab already open in ' + browserName);
  } else {
    log('opening a new Nexus tab in ' + browserName + '…');
    await cdpNewTab(NEXUS_URL).catch(e => warn('could not open a tab: ' + e.message));
    for (let i = 0; i < 40 && !page; i++) {         // give a slow server time
      await sleep(500);
      page = (await tabs()).filter(x => looksLikeNexus(x.url))[0];
    }
  }
  if (!page) throw new Error('could not open a Nexus tab in ' + browserName);

  const ws = await new WS(page.webSocketDebuggerUrl).connect();
  cdp = new CDP(ws);
  cdp.targetId = page.id;
  await cdp.send('Runtime.enable');
  await cdp.send('Page.enable');
  return cdp;
}

// ---------------------------------------------------------------- in-page work
/* Everything below runs INSIDE the Nexus tab. It waits for each thing to really
   be there, so a slow, far-away server only means it waits longer. */
const PAGE_SCRIPT = `
  const J = __JOB__;
  const log = [];
  const say = (m) => { log.push(m); try { window.__tcSay && window.__tcSay(m); } catch(e){} };
  const vis = (el) => !!el && el.offsetParent !== null && el.getBoundingClientRect().height > 0;
  const norm = (s) => String(s==null?'':s).replace(/\\s+/g,' ').trim().toUpperCase();
  const waitFor = (fn, label, ms) => new Promise((res, rej) => {
    const t0 = Date.now();
    (function tick(){
      let v = null; try { v = fn(); } catch(e){}
      if (v) return res(v);
      if (Date.now()-t0 > (ms||120000)) return rej(new Error(label));
      setTimeout(tick, 250);
    })();
  });
  const click = (el) => { try { el.scrollIntoView({block:'center'}); } catch(e){}
    ['pointerdown','mousedown','mouseup','click'].forEach(ty =>
      el.dispatchEvent(new MouseEvent(ty,{bubbles:true,cancelable:true,view:window}))); };
  const setVal = (el, v) => {
    const proto = el.tagName==='TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto,'value').set.call(el, v);
    ['input','change','blur'].forEach(t => el.dispatchEvent(new Event(t,{bubbles:true})));
    if (window.jQuery) window.jQuery(el).val(v).trigger('input').trigger('change');
  };

  // 0 — Nexus must have finished booting (its click handlers live on <body>)
  say('waiting for Nexus to finish loading');
  await waitFor(() => {
    if (!window.jQuery) return null;
    if (!document.querySelector('input.task_search')) return null;
    try { const ev = window.jQuery._data(document.body,'events');
          if (ev && ev.click && ev.click.length) return true; } catch(e){ return true; }
    return null;
  }, 'Nexus never finished loading its scripts', 180000);

  // 1 — open the task. Nexus's own handler needs only data-row.
  say('opening task ' + J.task);
  const real = document.querySelector('button.btn-edit[data-row="'+J.task+'"]');
  if (vis(real)) { click(real); }
  else {
    const b = document.createElement('button');
    b.className = 'btn btn-primary btn-sm btn-edit';
    b.setAttribute('data-row', J.task);
    b.style.cssText = 'position:fixed;left:-9999px;top:0';
    document.body.appendChild(b); b.click();
    setTimeout(()=>b.remove(), 2000);
  }

  // 2 — the task window arrives by AJAX
  say('waiting for the task window');
  const tab = await waitFor(() => {
    const a = document.querySelector('#EditProject a[data-target="#second"]');
    if (vis(a)) return a;
    const links = document.querySelectorAll('#EditProject .nav-tabs a, .modal.in .nav-tabs a');
    for (const l of links) if (norm(l.textContent)==='HOURS' && vis(l)) return l;
    return null;
  }, 'the task window never opened', 180000);

  // 3 — Hours tab (the form is inside it)
  say('opening the Hours tab');
  click(tab);
  if (window.jQuery) { try { window.jQuery(tab).tab('show'); } catch(e){} }
  const form = await waitFor(() => {
    const f = document.querySelector('#form-input_hours');
    return vis(f) ? f : null;
  }, 'the Hours form never appeared', 120000);

  // 4 — milestone first, then hours, date, description
  const out = { filled: [], missing: [] };
  if (J.milestone) {
    const sel = form.querySelector('select[name="ms_select"]') || form.querySelector('select');
    if (sel) {
      let hit = null;
      for (const o of sel.options)
        if (norm(o.value)===norm(J.milestone) || norm(o.textContent)===norm(J.milestone)) { hit = o; break; }
      if (!hit) for (const o of sel.options)
        if (norm(o.textContent).indexOf(norm(J.milestone))>=0) { hit = o; break; }
      if (hit) {
        sel.value = hit.value;
        sel.dispatchEvent(new Event('change',{bubbles:true}));
        if (window.jQuery) window.jQuery(sel).val(hit.value).trigger('change');
        const id = hit.getAttribute('data-ms-id');
        const hid = form.querySelector('input[name="ts_ms_id"]');
        if (id && hid) hid.value = id;                 // Nexus does not copy this itself
        out.filled.push('milestone=' + hit.value + (id ? ' (ms_id '+id+')' : ''));
      } else out.missing.push('milestone "'+J.milestone+'" not in the list');
    } else out.missing.push('milestone dropdown');
  }
  const h = form.querySelector('input[name="ts_hours"]');
  const d = form.querySelector('input[name="ts_date"]');
  const n = form.querySelector('textarea[name="ts_emp_description"], #ts_emp_description');
  if (h && J.hours) { setVal(h, String(J.hours)); out.filled.push('hours=' + J.hours); } else if (!h) out.missing.push('Hours box');
  if (d && J.date)  { setVal(d, String(J.date));
                      try { d.blur(); if (window.jQuery && window.jQuery(d).datepicker) window.jQuery(d).datepicker('hide'); } catch(e){}
                      const dp = document.getElementById('ui-datepicker-div'); if (dp) dp.style.display='none';
                      out.filled.push('date=' + J.date); } else if (!d) out.missing.push('Date box');
  if (n && J.desc)  { setVal(n, String(J.desc)); out.filled.push('description'); } else if (!n) out.missing.push('Description box');

  form.style.outline = '3px solid #16a34a'; form.style.outlineOffset = '3px';
  try { form.scrollIntoView({block:'center'}); } catch(e){}
  out.log = log;
  out.values = { ms: (form.querySelector('[name=ms_select]')||{}).value,
                 ms_id: (form.querySelector('[name=ts_ms_id]')||{}).value,
                 hours: (h||{}).value, date: (d||{}).value, desc: (n||{}).value };
  return out;
`;

// ---------------------------------------------------------------- one job
async function runJob(job) {
  console.log('');
  log('── job: task ' + job.task + ' · ' + (job.milestone || '?') + ' · ' +
      (job.hours || '0') + ' h · ' + (job.date || '') + ' ──');

  const c = await attach();

  // attach() only ever hands back a Nexus tab, but if it drifted, reload it
  const url = await c.evalAsync('return location.href;', 15000).catch(() => '');
  if (!looksLikeNexus(String(url))) {
    log('loading Nexus…');
    await c.send('Page.navigate', { url: NEXUS_URL });
    await sleep(3000);
  }
  await c.send('Page.bringToFront').catch(() => {});

  log('working in the browser (waiting on the server as long as it needs)…');
  const res = await c.evalAsync(PAGE_SCRIPT.replace('__JOB__', JSON.stringify(job)), 300000);

  (res && res.log ? res.log : []).forEach(m => log('   · ' + m));
  if (res && res.filled && res.filled.length) log('filled: ' + res.filled.join(', '));
  if (res && res.missing && res.missing.length) warn('could not fill: ' + res.missing.join(', '));
  ok('READY — check the form in ' + browserName + ' and press Submit yourself.');
  return res;
}

// ---------------------------------------------------------------- queue
let queue = [], busy = false;
async function pump() {
  if (busy || !queue.length) return;
  busy = true;
  const job = queue.shift();
  try { await runJob(job); }
  catch (e) {
    errl('stopped: ' + (e && e.message ? String(e.message).split('\n')[0] : e));
    errl('the ' + browserName + ' window is still open — finish that one by hand.');
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
      if (job && job.task) {
        const brand = brandFromUA(req.headers['user-agent']);
        if (brand && brand !== pressedIn) {
          pressedIn = brand;
          log('you pressed the rocket in ' + prettyName(brand) + ' — that is the browser I will use');
        }
        queue.push(job); log('got a job from the Timecard'); setImmediate(pump);
      }
    }
    res.writeHead(200, { 'Content-Type': 'image/gif', 'Access-Control-Allow-Origin': '*', 'Cache-Control': 'no-store' });
    return res.end(GIF);
  }
  res.writeHead(404, { 'Access-Control-Allow-Origin': '*' });
  res.end();
}).listen(PORT, '127.0.0.1', () => {
  console.log('\n  ┌──────────────────────────────────────────────────────────┐');
  console.log('  │  Timecard → Nexus helper  (no npm, no extra downloads)   │');
  console.log('  │                                                          │');
  console.log('  │  Leave this window open and minimise it.                 │');
  console.log('  │  Press a 🚀 in the Timecard and watch your browser.      │');
  console.log('  │  It never presses Submit — you do that.                  │');
  console.log('  └──────────────────────────────────────────────────────────┘');
  log('listening on http://127.0.0.1:' + PORT);
  log('Nexus: ' + NEXUS_URL);
  const list = browserCandidates();
  if (process.env.BROWSER_PATH || process.env.CHROME_PATH) {
    log('browser: forced by BROWSER_PATH/CHROME_PATH');
  } else if (defaultBrowser && defaultBrowser.exe && defaultBrowser.drivable) {
    log('your default browser: ' + prettyName(defaultBrowser.exe) + ' — using it');
  } else if (defaultBrowser && defaultBrowser.exe) {
    warn('your default browser is ' + prettyName(defaultBrowser.exe) + ', which cannot be automated ' +
         '(no DevTools Protocol) — using the next best browser instead');
  } else {
    warn('could not read your default browser from Windows — using the first browser I can find');
  }
  if (list.length) { log('browser: ' + prettyName(list[0]) + '  (' + list[0] + ')');
                     const fb = list.slice(1).map(p => prettyName(p)).filter((n, i, a) => a.indexOf(n) === i);
                     if (fb.length) log('fallbacks if it refuses debugging: ' + fb.join(', ')); }
  else log('browser: NOT FOUND — set BROWSER_PATH to your browser .exe');
  const tc = findTimecard();
  if (tc) log('opening your Timecard in this browser as the first tab: ' + tc);
  else    warn('Timecard not found — put Timecard*.html next to this helper, or open it yourself ' +
               'in the browser window this helper starts, so the rocket opens Nexus beside it.');
  if (!fs.existsSync(PROFILE_DIR)) log('first run: a fresh browser profile opens — sign in to Nexus once, it is remembered.');
});

process.on('unhandledRejection', (e) => errl('background error: ' + (e && e.message ? e.message : e)));
