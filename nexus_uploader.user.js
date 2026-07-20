// ==UserScript==
// @name         Timecard → Nexus hours uploader
// @namespace    timecard.local
// @version      3.3
// @description  Fills the timecard's daily tasks into Nexus (Draft Package, hours, date, notes). YOU click Submit. Reads tasks from the clipboard (reliable) or the URL. Has a "Copy page HTML" button so selectors can be pinned to the real page.
// @match        https://nexus.tcs.local/*
// @run-at        document-idle
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
  function setVal(el, val){
    if(!el) return;
    const proto = el.tagName==='TEXTAREA' ? HTMLTextAreaElement.prototype
                : el.tagName==='SELECT'   ? HTMLSelectElement.prototype
                :                           HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto,'value').set;
    setter.call(el, val==null?'':String(val));
    ['input','change','keyup','blur'].forEach(t=>el.dispatchEvent(new Event(t,{bubbles:true})));
  }
  function clickableByText(root, text){
    text = text.trim().toLowerCase();
    return [...root.querySelectorAll('a, button, [role="tab"], .nav-link, [data-toggle="tab"]')]
      .find(e => visible(e) && e.textContent.trim().toLowerCase()===text);
  }

  /* ---------- on-screen panel + log ---------- */
  let logBox;
  function panel(){
    let p = document.getElementById('tcup-panel');
    if(!p){
      if(!document.getElementById('tcup-anim')){
        const st=document.createElement('style'); st.id='tcup-anim';
        st.textContent='@keyframes tcupPulse{0%,100%{box-shadow:0 0 0 3px #22c55e,0 0 20px 4px rgba(34,197,94,.85),0 12px 40px rgba(0,0,0,.6)}'
          +'50%{box-shadow:0 0 0 3px #22c55e,0 0 44px 14px rgba(34,197,94,.95),0 12px 40px rgba(0,0,0,.6)}}';
        document.documentElement.appendChild(st);
      }
      p = document.createElement('div'); p.id='tcup-panel';
      p.style.cssText='position:fixed;z-index:2147483647;right:16px;bottom:16px;width:370px;background:#0b1220;color:#e2e8f0;'
        +'border:3px solid #22c55e;border-radius:12px;font:13px/1.45 Arial;animation:tcupPulse 1.6s ease-in-out infinite';
      p.innerHTML='<div style="padding:11px 14px;font-weight:bold;font-size:15px;color:#052e13;background:linear-gradient(90deg,#4ade80,#22c55e);border-radius:9px 9px 0 0;letter-spacing:.3px">🚀 TIMECARD HELPER — this box</div>'
        +'<div id="tcup-log" style="max-height:240px;overflow:auto;padding:9px 14px"></div>'
        +'<div id="tcup-btns" style="padding:9px 14px;border-top:1px solid #1e293b"></div>';
      document.documentElement.appendChild(p);
      logBox = p.querySelector('#tcup-log');
    }
    return p;
  }
  function log(html){ panel(); const d=document.createElement('div'); d.style.margin='3px 0'; d.innerHTML=html; logBox.appendChild(d); logBox.scrollTop=logBox.scrollHeight; }
  function setButtons(list){
    const b=panel().querySelector('#tcup-btns'); b.innerHTML='';
    list.forEach(x=>{ const el=document.createElement('button'); el.textContent=x.label;
      el.style.cssText='margin:2px 8px 2px 0;padding:5px 12px;border:0;border-radius:6px;cursor:pointer;background:'+(x.bg||'#e2e8f0')+';color:'+(x.fg||'#0f172a')+';font-weight:bold';
      el.onclick=x.fn; b.appendChild(el); });
  }
  function post(task, date, ok, err){ try{ if(window.opener) window.opener.postMessage({__tcnexus:true, task, date, ok, err}, '*'); }catch(e){} }

  function getQ(){ try{ return JSON.parse(localStorage.getItem(LSKEY)||'null'); }catch(e){ return null; } }
  function setQ(q){ localStorage.setItem(LSKEY, JSON.stringify(q)); }
  function clearQ(){ localStorage.removeItem(LSKEY); }
  function decode(b64){ return JSON.parse(decodeURIComponent(escape(atob(decodeURIComponent(b64))))); }
  function readHash(){ const m=(location.hash||'').match(/tcup=([^&]+)/); if(!m) return null; try{ return decode(m[1]); }catch(e){ return null; } }

  /* ---------- find things on the Nexus page ---------- */
  function findSearch(){
    return document.querySelector('input[placeholder="Task search"]')
        || document.querySelector('input[placeholder*="task" i]')
        || document.querySelector('input.task_search, input[name*="task" i]')
        || [...document.querySelectorAll('input[type="text"], input:not([type])')].find(i=>visible(i) && /task/i.test((i.placeholder||'')+(i.name||'')+(i.id||'')));
  }
  function findRow(task){
    return document.querySelector('#table1_body tr[data-id="'+task+'"]')
        || document.querySelector('tr[data-id="'+task+'"]')
        || document.querySelector('tr[data-row="'+task+'"]')
        || [...document.querySelectorAll('table tbody tr')].find(tr=>{
             if(!visible(tr)) return false;
             const id=tr.getAttribute('data-id')||tr.getAttribute('data-row');
             if(id===task) return true;
             return [...tr.querySelectorAll('.highlight, span, td')].some(el=>{ const tx=el.textContent.trim(); return tx===task || tx.endsWith('.'+task); });
           });
  }
  function runSearch(search, task){
    search.focus();
    setVal(search, task);
    ['keydown','keypress','keyup'].forEach(t=>search.dispatchEvent(new KeyboardEvent(t,{key:'Enter',code:'Enter',keyCode:13,which:13,bubbles:true})));
    const btn=[...document.querySelectorAll('button,.btn,i.fa-search,.search-btn,[type="submit"]')]
      .find(b=>visible(b) && /search|find/i.test((b.getAttribute('title')||'')+' '+(b.getAttribute('aria-label')||'')+' '+b.className+' '+b.textContent));
    if(btn) btn.click();
  }

  /* ---------- fill ONE task (never submits) ---------- */
  async function fillTask(t){
    const search = findSearch();
    if(!search) throw new Error('search box not found on this page');
    log('🔎 Searching <b>'+t.task+'</b>…');
    runSearch(search, t.task);
    const row = await waitFor(()=>findRow(t.task), 15000);
    if(!row){
      const ids=[...document.querySelectorAll('tr[data-id],tr[data-row]')].map(r=>r.getAttribute('data-id')||r.getAttribute('data-row')).slice(0,15);
      throw new Error('task row not found. Rows on screen: '+(ids.length?ids.join(', '):'none'));
    }
    log('✓ Found the task row');
    let edit = row.querySelector('button.btn-edit, a.btn-edit, .btn-edit')
      || clickableByText(row,'Edit')
      || [...row.querySelectorAll('button,a,i,span,[role="button"],[onclick]')].find(el=>visible(el) &&
           /\bedit\b|pencil|fa-edit|fa-pencil/i.test((el.className||'')+' '+(el.getAttribute('title')||'')+' '+(el.getAttribute('data-original-title')||'')+' '+(el.textContent||'')));
    if(!edit){ edit = [...row.querySelectorAll('button,a[href="#"],a[role="button"],[onclick]')].find(visible); if(edit) log('⚠ Using the row’s first button as Edit'); }
    if(!edit){
      const cls=[...row.querySelectorAll('button,a,[role="button"]')].filter(visible).map(b=>b.tagName.toLowerCase()+'.'+((b.className||'').trim().split(/\s+/)[0]||'?')).slice(0,8);
      throw new Error('no Edit button in the row. Buttons there: '+(cls.join(', ')||'none'));
    }
    edit.click(); log('✓ Clicked <b>Edit</b>');
    const hoursTab = await waitFor(()=>{ const modal=[...document.querySelectorAll('.modal,.modal-dialog,[role="dialog"]')].find(visible)||document.body; return clickableByText(modal,'Hours'); }, 15000);
    if(!hoursTab) throw new Error('Hours tab not found in the pop-up');
    hoursTab.click(); log('✓ Opened the <b>Hours</b> tab');
    const form = await waitFor(()=>{ const f=document.querySelector('#form-input_hours'); return visible(f)?f:null; }, 15000);
    if(!form) throw new Error('Hours form did not load');
    const ms = form.querySelector('select[name="ms_select"]');
    if(ms){ setVal(ms, t.milestone||'DRAFT PACKAGE');
      const opt=ms.querySelector('option[value="'+(t.milestone||'DRAFT PACKAGE')+'"]'); const id=opt&&opt.getAttribute('data-ms-id');
      if(id){ const hid=form.querySelector('input[name="ts_ms_id"]'); if(hid) setVal(hid,id); } }
    setVal(form.querySelector('input[name="ts_hours"]'), t.hours);
    const dateEl=form.querySelector('input[name="ts_date"]'); if(dateEl){ dateEl.removeAttribute('readonly'); setVal(dateEl, t.date); }
    const desc=form.querySelector('textarea[name="ts_emp_description"]'); if(desc) setVal(desc, t.desc||'');
    log('✓ Filled milestone, hours ('+t.hours+'), date'+(t.desc?' &amp; notes':''));
    return form;
  }
  async function closeAnyModal(){
    const m=[...document.querySelectorAll('.modal,[role="dialog"]')].find(visible); if(!m) return;
    const x=m.querySelector('.close,[data-dismiss="modal"],.btn-close'); if(x) x.click();
    else document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',keyCode:27,bubbles:true}));
    await sleep(600);
  }

  async function processNext(){
    let q=getQ(); if(!q) return;
    if(Date.now()-(q.ts||0) > 30*60000){ clearQ(); return; }
    if(q.i>=q.tasks.length){
      const ok=q.results.filter(r=>r.ok).length;
      log('🏁 <b>Done.</b> '+ok+' of '+q.tasks.length+' submitted.');
      setButtons([{label:'Close', fn:()=>panel().remove()}]);
      clearQ(); return;
    }
    const t=q.tasks[q.i];
    log('— — <b>Task '+(q.i+1)+'/'+q.tasks.length+'</b> ('+t.task+') — —');
    try{
      const form=await fillTask(t);
      const submit=form.querySelector('button[name="btn_submit-inputHours"]')||form.querySelector('[type="submit"]');
      log('✋ <b>Review, then click Submit</b> in Nexus to log this task.');
      setButtons([
        {label:'Skip this task', fn:()=>{ post(t.task,t.date,false,'skipped'); const qq=getQ(); qq.i++; qq.results.push({task:t.task,date:t.date,ok:false,err:'skipped'}); setQ(qq); closeAnyModal().then(processNext); }},
        {label:'Stop', bg:'#7f1d1d', fg:'#fff', fn:()=>{ post(t.task,t.date,false,'stopped'); clearQ(); log('⏹ Stopped.'); setButtons([{label:'Close',fn:()=>panel().remove()}]); }}
      ]);
      if(submit){
        const onSubmit=()=>{ submit.removeEventListener('click',onSubmit);
          log('✅ Submitted <b>'+t.task+'</b>'); post(t.task,t.date,true);
          const qq=getQ()||q; qq.i++; qq.results.push({task:t.task,date:t.date,ok:true}); qq.ts=Date.now(); setQ(qq);
          setTimeout(()=>{ if(getQ()) closeAnyModal().then(processNext); }, 1600);
        };
        submit.addEventListener('click', onSubmit);
      }
    }catch(e){
      log('❌ <b>'+t.task+'</b>: '+e.message);
      post(t.task,t.date,false,e.message);
      const qq=getQ()||q; qq.i++; qq.results.push({task:t.task,date:t.date,ok:false,err:e.message}); qq.ts=Date.now(); setQ(qq);
      setButtons([
        HTMLBTN,
        {label:'Skip to next', fn:()=>closeAnyModal().then(processNext)},
        {label:'Stop', bg:'#7f1d1d', fg:'#fff', fn:()=>{ clearQ(); setButtons([{label:'Close',fn:()=>panel().remove()}]); }}
      ]);
    }
  }

  function startQueue(tasks){
    if(!Array.isArray(tasks) || !tasks.length){ log('❌ No tasks to send.'); return; }
    setQ({ tasks, i:0, results:[], ts:Date.now() });
    try{ history.replaceState(null,'',location.pathname+location.search); }catch(e){}
    processNext();
  }
  async function fillFromClipboard(){
    let txt=''; try{ txt=await navigator.clipboard.readText(); }catch(e){ log('❌ Couldn’t read the clipboard: '+e.message+'. Click a 🚀 in the timecard first.'); return; }
    let tasks; try{ tasks=JSON.parse(txt); }catch(e){ log('❌ The clipboard isn’t timecard tasks. In the timecard, click a task’s 🚀 button (that copies them), then come back and click this.'); return; }
    startQueue(tasks);
  }

  /* ---------- capture the page's structure so the selectors can be pinned ---------- */
  // Copies a trimmed snapshot of the live DOM: keeps every tag, class, id, name,
  // placeholder, role and data-* (that's what the clicks key off), drops
  // scripts/styles/svg/images, and truncates long text so no bulk data leaks.
  function snapshot(){
    const clone = document.documentElement.cloneNode(true);
    clone.querySelectorAll('#tcup-panel,script,style,svg,noscript,link,img,iframe,canvas,picture,video,audio').forEach(n=>n.remove());
    try{
      const walk=document.createTreeWalker(clone, NodeFilter.SHOW_TEXT, null);
      let n; while((n=walk.nextNode())){ const t=n.nodeValue; if(t && t.length>60) n.nodeValue=t.slice(0,60)+'…'; }
    }catch(e){}
    return clone.outerHTML;
  }
  async function copyHtml(){
    const html=snapshot();
    try{ await navigator.clipboard.writeText(html); log('📋 Copied <b>'+Math.ceil(html.length/1024)+' KB</b> of page HTML. Paste it back to support.'); }
    catch(e){ log('❌ Clipboard blocked: '+e.message+'. Open DevTools console and run: <code>copy(document.documentElement.outerHTML)</code>'); }
  }
  const HTMLBTN = {label:'📋 Copy page HTML', bg:'#334155', fg:'#fff', fn:copyHtml};

  function diagnostics(){
    const s=findSearch();
    log('✅ <b>Helper is running.</b> This green glowing box is mine.');
    log('Search box: '+(s?'✓ found (placeholder="'+(s.getAttribute('placeholder')||'')+'")':'<b style="color:#fca5a5">NOT found</b>'));
    const rows=document.querySelectorAll('tr[data-id],tr[data-row]');
    log('Task rows detected: '+rows.length);
    if(!s){
      const inps=[...document.querySelectorAll('input')].filter(visible).map(i=>'"'+(i.getAttribute('placeholder')||i.name||i.id||'?')+'"').slice(0,12);
      log('Text inputs I can see: '+(inps.join(', ')||'none'));
    }
    setButtons([
      {label:'▶ Fill tasks from clipboard', bg:'#2563eb', fg:'#fff', fn:fillFromClipboard},
      HTMLBTN,
      {label:'Close', fn:()=>panel().remove()}
    ]);
  }

  function boot(){
    const hash = readHash();
    const q = getQ();
    if(hash){ panel(); startQueue(hash); return; }                 // came in via the URL (if it survived)
    if(q && (Date.now()-(q.ts||0) < 30*60000) && q.i < q.tasks.length){ panel(); processNext(); return; }   // resume after a Submit reload
    if(q) clearQ();
    panel(); diagnostics();                                        // idle: show status + the clipboard button
  }
  setTimeout(boot, 1000);
})();
