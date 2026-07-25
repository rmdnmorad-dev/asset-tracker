// ==UserScript==
// @name         ISAT Timecard → Nexus hours filler
// @namespace    isat.timecard.nexus
// @version      1.0
// @description  Fills the Nexus "Hours" form (Milestone / Hours / Date / Description) for each task from the ISAT Timecard — one task at a time. Never submits; you review and click Submit.
// @match        *://nexus.tcs.local/*
// @run-at       document-idle
// @grant        none
// ==/UserScript==
(function(){
'use strict';

/* ---------- small DOM helpers ---------- */
function vis(list){ for(const el of list){ if(el && el.offsetParent!==null) return el; } return null; }
function waitFor(getter, timeout, what, interval){
  interval = interval||120;
  return new Promise((res,rej)=>{ const t0=Date.now();
    (function poll(){ let el=null; try{ el=getter(); }catch(e){}
      if(el) return res(el);
      if(Date.now()-t0>timeout) return rej(new Error('Timed out waiting for '+(what||'element')));
      setTimeout(poll,interval); })();
  });
}
function modalOpen(){ return !!vis(document.querySelectorAll('a[data-target="#second"]')); }   // a visible Hours tab = the task window is open
function nativeSet(el,val){
  const proto = el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype
              : el.tagName==='SELECT'  ?HTMLSelectElement.prototype
              :                         HTMLInputElement.prototype;
  const d=Object.getOwnPropertyDescriptor(proto,'value');
  (d&&d.set ? d.set : Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set).call(el,val);
}
function fire(el,types){ types.forEach(t=>el.dispatchEvent(new Event(t,{bubbles:true}))); }
function setVal(el,val){ if(!el) return; nativeSet(el,String(val)); fire(el,['input','change','keyup','blur']); }
function setSelect(sel,val){
  if(!sel) return null; const want=String(val).trim().toUpperCase();
  let opt=null;
  for(const o of sel.options){ if((o.value||'').trim().toUpperCase()===want || (o.textContent||'').trim().toUpperCase()===want){ opt=o; break; } }
  if(opt){ nativeSet(sel,opt.value); } else { nativeSet(sel,val); }
  fire(sel,['input','change']);
  return opt;
}
function setDate(el,mdy){
  if(!el) return; const ro=el.hasAttribute('readonly'); if(ro) el.removeAttribute('readonly');
  try{ if(window.jQuery && window.jQuery(el).hasClass('hasDatepicker')){ const p=parseMDY(mdy); if(p) window.jQuery(el).datepicker('setDate',p); } }catch(e){}
  nativeSet(el,mdy); fire(el,['input','change','blur']);
  if(ro) el.setAttribute('readonly','readonly');
}

/* ---------- dates / hours ---------- */
function parseMDY(s){ const m=/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(String(s||'').trim()); return m? new Date(+m[3],+m[1]-1,+m[2]) : null; }
function fmtMDY(d){ return String(d.getMonth()+1).padStart(2,'0')+'/'+String(d.getDate()).padStart(2,'0')+'/'+d.getFullYear(); }
function todayMid(){ const n=new Date(); return new Date(n.getFullYear(),n.getMonth(),n.getDate()); }
function clampDate(s){ const d=parseMDY(s), t=todayMid(); if(!d) return fmtMDY(t); return d>t? fmtMDY(t) : fmtMDY(d); }
function cleanHours(h){ const n=parseFloat(String(h==null?'':h)); if(isNaN(n)) return ''; return String(Math.round(n*100)/100); }

function sleep(ms){ return new Promise(r=>setTimeout(r,ms)); }
// the REAL Hours form = a #form-input_hours whose milestone dropdown is populated (not the hidden stub)
function realForm(mustBeVisible){
  const forms=[...document.querySelectorAll('form#form-input_hours')];
  for(const f of forms){ const s=f.querySelector('select[name="ms_select"]');
    if(s && s.options.length>2 && (!mustBeVisible || f.offsetParent!==null)) return f; }
  return null;
}
function applyForm(form, t){
  const sel = form.querySelector('select[name="ms_select"]');
  const opt = setSelect(sel, t.milestone || 'DRAFT PACKAGE');
  const msHid = form.querySelector('input[name="ts_ms_id"]'); if(msHid && opt) setVal(msHid, opt.getAttribute('data-ms-id')||'');
  setVal(form.querySelector('input[name="ts_hours"]'), cleanHours(t.hours));
  setDate(form.querySelector('input[name="ts_date"]'), clampDate(t.date));
  setVal(form.querySelector('textarea[name="ts_emp_description"]'), t.desc||'');
}
// the form counts as filled only when the ACTUAL values match this task (not just "non-empty" — avoids accepting a previous task's leftovers)
function formValuesMatch(form, t){
  if(!form) return false;
  const ms=form.querySelector('select[name="ms_select"]'), h=form.querySelector('input[name="ts_hours"]'), d=form.querySelector('input[name="ts_date"]');
  return ms && (ms.value||'').toUpperCase()===String(t.milestone||'DRAFT PACKAGE').toUpperCase()
      && h && h.value===cleanHours(t.hours)
      && d && d.value===clampDate(t.date);
}

/* ---------- the fill for ONE task (retries every step; stops before Submit) ---------- */
async function fillTask(t){
  const task = String(t.task);
  // STEP 1 — search: type the task # (re-type only if the box gets cleared), wait for its row through the loading screen
  const search = await waitFor(()=>vis(document.querySelectorAll('input.task_search')), 15000, 'the task-search box');
  log('🔎 Searching '+task+' …');
  setVal(search, task);
  await waitFor(()=>{
      const s=vis(document.querySelectorAll('input.task_search')); if(s && (s.value||'').trim()==='') setVal(s, task);
      return vis(document.querySelectorAll('button.btn-edit[data-row="'+task+'"]'));
    }, 60000, 'the '+task+' row (loading… / are you logged in?)', 600);
  // STEP 2 — click Edit, then give that click several seconds to open the window; only re-click if it was truly lost
  //          (spamming clicks mid-load would restart the loading screen every time)
  log('📝 Opening task '+task+' — waiting for the loading screen…');
  for(let a=0; a<8 && !modalOpen(); a++){
    const b=vis(document.querySelectorAll('button.btn-edit[data-row="'+task+'"]')); if(b) b.click();
    try{ await waitFor(()=>modalOpen()?true:null, 6000, 'window', 200); }catch(e){}
  }
  if(!modalOpen()) throw new Error('the task window would not open (the Edit button didn’t respond) — press Fill again.');
  // STEP 3 — click the Hours tab until the populated Hours form is visible (tab clicks don’t reload, so this can be tight)
  log('📂 Opening the Hours tab…');
  let form = null;
  for(let a=0; a<15 && !form; a++){
    const tab=vis(document.querySelectorAll('a[data-target="#second"]')); if(tab) tab.click();
    try{ form=await waitFor(()=>realForm(true), 2500, 'form', 200); }catch(e){}
  }
  if(!form) throw new Error('the Hours form did not appear — press Fill again.');
  // STEP 4 — fill, and keep it filled while the modal finishes its late AJAX render
  log('✍ Filling task '+task+' …');
  applyForm(form, t);
  const t0=Date.now();
  while(Date.now()-t0 < 3200){ await sleep(300); const f=realForm(true)||form; if(!formValuesMatch(f,t)) applyForm(f,t); }
  const fin=realForm(true)||form; if(!formValuesMatch(fin,t)) applyForm(fin,t);
  if(formValuesMatch(realForm(true)||form, t)) log('✅ Task '+task+' filled — review it and click <b>Submit</b>.');
  else log('⚠ Task '+task+' — the form didn\'t hold the values; press <b>Fill</b> again.');
}

/* ================= the on-page panel ================= */
let TASKS=[], idx=0, running=false;

function log(html){ const el=document.getElementById('nxup-log'); if(el) el.innerHTML=html; }
function escapeHtml(s){ return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
function renderList(){
  const box=document.getElementById('nxup-list'); if(!box) return;
  box.innerHTML = TASKS.map((t,i)=>{
    const state = t.__done ? '✅' : (i===idx ? '▶️' : '·');
    return '<div style="padding:2px 0;'+(i===idx?'font-weight:bold':'')+'">'+state+' '+
      escapeHtml(t.task||'?')+' — '+cleanHours(t.hours)+'h · '+clampDate(t.date)+
      (t.desc? ' · '+escapeHtml(t.desc).slice(0,26)+(t.desc.length>26?'…':'') : '')+'</div>';
  }).join('') || '<div style="color:#888">No tasks loaded yet.</div>';
  const btn=document.getElementById('nxup-fill'); if(!btn) return;
  if(idx>=TASKS.length && TASKS.length){ btn.textContent='All '+TASKS.length+' tasks done'; btn.disabled=true; }
  else { btn.textContent='▶ Fill task '+(idx+1)+' of '+TASKS.length; btn.disabled=!TASKS.length||running; }
}

function loadFrom(text){
  let data; try{ data=JSON.parse(text); }catch(e){ log('⚠ That is not valid task JSON.'); return; }
  const arr = Array.isArray(data)? data : (data && Array.isArray(data.tasks)? data.tasks : null);
  if(!arr){ log('⚠ Expected a list of tasks.'); return; }
  TASKS = arr.filter(t=>/^\d{6}$/.test(String(t.task||'').trim()) && cleanHours(t.hours)!=='' && +cleanHours(t.hours)>0)
             .map(t=>({task:String(t.task).trim(), hours:t.hours, date:t.date, desc:t.desc||t.notes||'', milestone:t.milestone||'DRAFT PACKAGE'}));
  idx=0; renderList();
  log(TASKS.length? '📋 Loaded <b>'+TASKS.length+'</b> task(s). Click “Fill task 1”.' : '⚠ No valid 6-digit tasks with hours found.');
}

async function onFill(){
  if(running || idx>=TASKS.length) return;
  running=true; renderList();
  try{ await fillTask(TASKS[idx]); TASKS[idx].__done=true; idx++; }
  catch(e){ log('⚠ '+e.message+' — fix it and press the button again.'); }
  running=false; renderList();
}

function buildPanel(){
  if(document.getElementById('nxup-panel')) return;
  const p=document.createElement('div'); p.id='nxup-panel';
  p.style.cssText='position:fixed;top:70px;right:14px;z-index:2147483647;width:290px;background:#fff;border:1px solid #123;'+
    'border-radius:10px;box-shadow:0 8px 30px rgba(0,0,0,.3);font:13px Arial;color:#111;overflow:hidden';
  p.innerHTML=
    '<div style="background:#123;color:#fff;padding:8px 10px;font-weight:bold;display:flex;justify-content:space-between;align-items:center">'+
      '<span>⬆ Timecard → Nexus</span><span id="nxup-min" style="cursor:pointer">–</span></div>'+
    '<div id="nxup-body" style="padding:10px">'+
      '<div style="font-size:11.5px;color:#555;margin-bottom:5px">Click the box and paste (Ctrl+V) the tasks copied from the Timecard:</div>'+
      '<textarea id="nxup-paste" placeholder="paste tasks here…" style="width:100%;height:46px;box-sizing:border-box;border:1px solid #aaa;border-radius:6px;font:12px monospace"></textarea>'+
      '<div id="nxup-list" style="margin:8px 0;max-height:150px;overflow:auto;font-size:12px"></div>'+
      '<button id="nxup-fill" style="width:100%;padding:9px;border:0;border-radius:8px;background:#2f6fb0;color:#fff;font-weight:bold;cursor:pointer" disabled>▶ Fill task</button>'+
      '<div id="nxup-log" style="margin-top:8px;font-size:12px;color:#123;min-height:18px"></div>'+
      '<div style="margin-top:6px;font-size:11px;color:#a00">The script never submits — you review each task and click <b>Submit</b>.</div>'+
      '<a href="#" id="nxup-reset" style="font-size:11px">reset</a>'+
    '</div>';
  (document.body||document.documentElement).appendChild(p);   // documentElement survives a body re-render
  const paste=document.getElementById('nxup-paste');
  paste.addEventListener('input',()=>{ if(paste.value.trim()) loadFrom(paste.value.trim()); });
  paste.addEventListener('paste',()=>{ setTimeout(()=>loadFrom(paste.value.trim()),0); });
  document.getElementById('nxup-fill').addEventListener('click',onFill);
  document.getElementById('nxup-reset').addEventListener('click',e=>{ e.preventDefault(); TASKS=[];idx=0;paste.value='';renderList();log(''); });
  document.getElementById('nxup-min').addEventListener('click',()=>{ const b=document.getElementById('nxup-body'); b.style.display=b.style.display==='none'?'':'none'; });
  renderList();
  autoLoadFromUrl();
}

/* the rocket on the Timecard opens Nexus with the data in the URL hash:
   #tcupload=<encoded JSON>.  Read it once, load the tasks, and auto-fill the first one. */
let autoLoaded=false;
function autoLoadFromUrl(){
  if(autoLoaded) return;
  const m=/[#&]tcupload=([^&]+)/.exec(location.hash||'');
  if(!m) return;
  autoLoaded=true;
  let json=null; try{ json=decodeURIComponent(m[1]); }catch(e){ return; }
  loadFrom(json);
  try{ history.replaceState(null,'',location.pathname+location.search); }catch(e){}   // clear the hash (refresh won't re-fill)
  if(!TASKS.length) return;
  log('📋 Loaded '+TASKS.length+' task(s) from the Timecard — starting…');
  onFill();   // fillTask() itself waits for the search box + results through the loading screen
}

// keep the panel alive even if Nexus re-renders the page, and (re)run the auto-load once
function ensurePanel(){ try{ buildPanel(); autoLoadFromUrl(); }catch(e){} }

// expose for automated testing
window.__nxup = { fillTask, loadFrom, onFill, get tasks(){return TASKS;}, get idx(){return idx;} };

ensurePanel();
document.addEventListener('DOMContentLoaded', ensurePanel);
window.addEventListener('load', ensurePanel);
setInterval(ensurePanel, 1500);   // Nexus can wipe injected nodes on its own render — re-add the panel if it disappears

})();
