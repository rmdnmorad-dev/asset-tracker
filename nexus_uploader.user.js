// ==UserScript==
// @name         Timecard → Nexus hours uploader
// @namespace    timecard.local
// @version      1.0
// @description  Logs the timecard's daily tasks into Nexus (search → Edit → Hours → Draft Package → hours/date → Submit)
// @match        https://nexus.tcs.local/*
// @run-at       document-idle
// @grant        none
// ==/UserScript==
(function(){
  'use strict';
  const LSKEY = 'tc_nexus_queue';
  const sleep = ms => new Promise(r=>setTimeout(r,ms));
  const visible = el => !!(el && el.offsetParent!==null);

  async function waitFor(fn, timeout=15000, step=250){
    const t0 = Date.now();
    for(;;){ let v; try{ v=fn(); }catch(e){ v=null; } if(v) return v; if(Date.now()-t0>timeout) return null; await sleep(step); }
  }
  // set a value the way React/jQuery pages expect (native setter + input/change)
  function setVal(el, val){
    if(!el) return;
    const proto = el.tagName==='TEXTAREA' ? HTMLTextAreaElement.prototype
                : el.tagName==='SELECT'   ? HTMLSelectElement.prototype
                :                           HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto,'value').set;
    setter.call(el, val);
    el.dispatchEvent(new Event('input',  {bubbles:true}));
    el.dispatchEvent(new Event('change', {bubbles:true}));
  }
  function byText(root, sel, text){
    text = text.toLowerCase();
    return [...root.querySelectorAll(sel)].find(e => e.textContent.trim().toLowerCase()===text && visible(e));
  }
  function panel(){
    let p = document.getElementById('tcup-panel');
    if(!p){ p = document.createElement('div'); p.id='tcup-panel';
      p.style.cssText='position:fixed;z-index:2147483647;right:16px;bottom:16px;max-width:380px;background:#111827;color:#fff;'
        +'padding:12px 16px;border-radius:10px;font:14px/1.45 Arial;box-shadow:0 8px 30px rgba(0,0,0,.45)';
      document.documentElement.appendChild(p); }
    return p;
  }
  function status(html, buttons){
    const p = panel();
    p.innerHTML = '<div>'+html+'</div>';
    (buttons||[]).forEach(b=>{ const el=document.createElement('button'); el.textContent=b.label;
      el.style.cssText='margin:8px 8px 0 0;padding:5px 12px;border:0;border-radius:6px;cursor:pointer;background:#e5e7eb;color:#111';
      el.onclick=b.fn; p.appendChild(el); });
  }

  function decodePayload(b64){
    return JSON.parse(decodeURIComponent(escape(atob(decodeURIComponent(b64)))));
  }
  function readHash(){
    const m = (location.hash||'').match(/tcup=([^&]+)/);
    if(!m) return null;
    try{ return decodePayload(m[1]); }catch(e){ return null; }
  }

  // ---- do ONE task, up to (but not including) the reload after submit ----
  async function fillTask(t){
    // 1) search
    const search = document.querySelector('input[placeholder="Task search"]')
                 || document.querySelector('input[placeholder*="Task" i]');
    if(!search) throw new Error('search box not found');
    setVal(search, t.task);
    ['keydown','keyup'].forEach(type=>search.dispatchEvent(new KeyboardEvent(type,{key:'Enter',keyCode:13,which:13,bubbles:true})));
    // 2) wait for the row (6-digit task # == row data-id)
    const row = await waitFor(()=>document.querySelector('#table1_body tr[data-id="'+t.task+'"]'), 12000);
    if(!row) throw new Error('task row not found for '+t.task);
    // 3) Edit
    const edit = row.querySelector('button.btn-edit'); if(!edit) throw new Error('Edit button not found');
    edit.click();
    // 4) Hours tab inside the modal
    const hoursTab = await waitFor(()=>{
      const modal = [...document.querySelectorAll('.modal')].find(visible) || document;
      // only clickable tab elements (an <li> wrapper also "contains" the text but isn't the handler)
      return byText(modal, 'a, button, [role="tab"], .nav-link, [data-toggle="tab"]', 'Hours');
    }, 12000);
    if(!hoursTab) throw new Error('Hours tab not found');
    hoursTab.click();
    // 5) the hours form
    const form = await waitFor(()=>{ const f=document.querySelector('#form-input_hours'); return visible(f)?f:null; }, 12000);
    if(!form) throw new Error('Hours form not found');
    // 6) Milestone = Draft Package
    const ms = form.querySelector('select[name="ms_select"]');
    if(ms){
      setVal(ms, t.milestone||'DRAFT PACKAGE');
      const opt = ms.querySelector('option[value="'+(t.milestone||'DRAFT PACKAGE')+'"]');
      const id  = opt && opt.getAttribute('data-ms-id');
      if(id) setVal(form.querySelector('input[name="ts_ms_id"]'), id);
    }
    // 7) hours + date
    setVal(form.querySelector('input[name="ts_hours"]'), String(t.hours));
    const dateEl = form.querySelector('input[name="ts_date"]');
    if(dateEl){ dateEl.removeAttribute('readonly'); setVal(dateEl, t.date); }
    await sleep(300);
    // 8) submit — return the button; the caller commits progress BEFORE clicking (a full POST reloads the page)
    const submit = form.querySelector('button[name="btn_submit-inputHours"]') || form.querySelector('[type="submit"]');
    if(!submit) throw new Error('Submit button not found');
    return submit;
  }

  async function run(){
    let q = null;
    try{ q = JSON.parse(localStorage.getItem(LSKEY)||'null'); }catch(e){}

    const fromHash = readHash();
    if(fromHash){
      history.replaceState(null, '', location.pathname + location.search);   // drop hash so a reload can't retrigger
      const summary = fromHash.map(t=>'• '+t.task+' — '+t.hours+'h — '+t.date).join('\n');
      if(!confirm('Timecard → Nexus\nLog these '+fromHash.length+' hour entr'+(fromHash.length===1?'y':'ies')+
                  ' (Milestone: Draft Package)?\n\n'+summary)){ return; }
      q = { tasks:fromHash, i:0, results:[], ts:Date.now() };
      localStorage.setItem(LSKEY, JSON.stringify(q));
    }
    if(!q) return;
    if(Date.now()-(q.ts||0) > 20*60000){ localStorage.removeItem(LSKEY); return; }   // stale/abandoned run

    if(q.i >= q.tasks.length){                       // finished — report
      localStorage.removeItem(LSKEY);
      const ok = q.results.filter(r=>r.ok).length;
      const bad = q.results.filter(r=>!r.ok);
      status('✅ <b>Nexus upload finished:</b> '+ok+'/'+q.tasks.length+' submitted.'
        + (bad.length ? '<br>❌ Failed: '+bad.map(b=>b.task+' ('+b.err+')').join(', ') : '')
        + '<br><span style="opacity:.8">Please spot-check in Nexus.</span>',
        [{label:'OK', fn:()=>panel().remove()}]);
      return;
    }

    const t = q.tasks[q.i];
    status('⏳ Nexus '+(q.i+1)+'/'+q.tasks.length+' — task <b>'+t.task+'</b> ('+t.hours+'h)…',
           [{label:'Cancel', fn:()=>{ localStorage.removeItem(LSKEY); panel().remove(); }}]);
    try{
      const submit = await fillTask(t);
      // commit success + advance BEFORE the submit (a full-page POST would reload us mid-step)
      console.log('[tcup] filled OK', t.task); q.i++; q.results.push({task:t.task, ok:true}); q.ts=Date.now();
      localStorage.setItem(LSKEY, JSON.stringify(q));
      submit.click();
      await sleep(2600);              // if it was AJAX (no reload), move on ourselves
      location.reload();
    }catch(e){
      console.log('[tcup] FAIL', t.task, e.message); q.i++; q.results.push({task:t.task, ok:false, err:e.message}); q.ts=Date.now();
      localStorage.setItem(LSKEY, JSON.stringify(q));
      await sleep(600);
      location.reload();
    }
  }

  setTimeout(run, 900);   // let Nexus finish its own load
})();
