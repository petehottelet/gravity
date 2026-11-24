const $ = s=>document.querySelector(s), $$ = s=>[...document.querySelectorAll(s)];
const icon = n=>(window.gravityIcons[n]||'').replace('<svg ','<svg aria-hidden="true" focusable="false" ');
$$('[data-icon]').forEach(e=>{e.outerHTML=icon(e.dataset.icon);});
let pages=Array.from({length:14},(_,i)=>({id:i+1,source:i+1,rotation:0}));
let current=0, selected=new Set([1]), organizing=new URLSearchParams(location.search).get('mode')==='organizing', scale=1, viewRotation=0, nextId=15, clipboard=[], matchCase=false;
const undoStack=[], redoStack=[];
const snapshot=()=>({pages:pages.map(p=>({...p})),current,selected:[...selected]});
function remember(){undoStack.push(snapshot());redoStack.length=0;}
function restore(state){pages=state.pages;current=state.current;selected=new Set(state.selected);render();}
function notice(text){$('.notice span').textContent=text;$('.notice').hidden=false;}
function imageFor(p){return `assets/${p.source===1?'page-1':`thumb-${Math.min(p.source,12)}`}.png`;}
function go(index){current=Math.max(0,Math.min(pages.length-1,index));if(pages[current])selected=new Set([pages[current].id]);render();}
function render(){
 current=Math.max(0,Math.min(current,pages.length-1));
 document.body.classList.toggle('organizing',organizing);
 $('.read-tools').hidden=organizing;$('.edit-tools').hidden=!organizing;$('.find-tools').hidden=organizing;
 $('.reader').hidden=organizing||!pages.length;$('.organizer').hidden=!organizing||!pages.length;$('.empty').hidden=!!pages.length;
 $('[data-action="organize"]').setAttribute('aria-pressed',String(organizing));
 $('.bookmarks nav').innerHTML=pages.map((p,i)=>`<button data-index="${i}" ${i===current?'aria-current="page"':''}>Chapter ${i+1}</button>`).join('');
 $('.bookmarks nav').querySelectorAll('button').forEach(b=>b.onclick=()=>go(Number(b.dataset.index)));
 $('#page-number').value=pages.length?current+1:0;$('#page-number').max=pages.length;$('#page-count').textContent=`of ${pages.length}`;
 $('[data-action="previous"]').disabled=!pages.length||current===0;$('[data-action="next"]').disabled=!pages.length||current===pages.length-1;
 $$('[data-action="undo"]').forEach(b=>{b.disabled=!undoStack.length;});$('[data-action="redo"]').disabled=!redoStack.length;$('[data-action="save"]').disabled=!undoStack.length;$('[data-action="paste"]').disabled=!clipboard.length;
 if(pages[current]){const p=pages[current];$('#document-page').src=imageFor(p);$('#document-page').alt=`Sample Guide, chapter ${current+1}`;$('.paper').style.transform=`scale(${scale}) rotate(${viewRotation+p.rotation}deg)`;$('.unavailable').hidden=p.source<=12;$('.unavailable strong').textContent=`Chapter ${current+1}`;}
 $('.organizer').innerHTML=pages.map((p,i)=>`<button class="tile" aria-label="Page ${i+1}" aria-pressed="${selected.has(p.id)}" data-index="${i}"><span class="thumb">${p.source<=12?`<img src="assets/thumb-${p.source}.png" alt="" style="transform:rotate(${p.rotation}deg)">`:'<span class="thumb-placeholder">Preview unavailable</span>'}</span><span class="page-label">${i+1}</span></button>`).join('');
 $$('.tile').forEach(b=>{b.onclick=e=>{const i=Number(b.dataset.index),id=pages[i].id;if(e.shiftKey){for(let j=Math.min(i,current);j<=Math.max(i,current);j++)selected.add(pages[j].id);}else if(e.ctrlKey||e.metaKey){selected.has(id)?selected.delete(id):selected.add(id);}else selected=new Set([id]);current=i;render();$(`.tile[data-index="${i}"]`)?.focus({preventScroll:true});};b.ondblclick=()=>{current=Number(b.dataset.index);organizing=false;render();};});
}
function rotate(delta){if(organizing){if(!selected.size)return;remember();pages.forEach(p=>{if(selected.has(p.id))p.rotation=(p.rotation+delta+360)%360;});}else viewRotation=(viewRotation+delta+360)%360;render();}
function find(direction){const query=matchCase?$('#find').value:$('#find').value.toLowerCase();if(!query)return;const candidates=pages.map((_,i)=>`Chapter ${i+1}`);for(let step=1;step<=pages.length;step++){const i=(current+direction*step+pages.length)%pages.length;const title=matchCase?candidates[i]:candidates[i].toLowerCase();if(title.includes(query)){go(i);return;}}notice('No matching chapter. This preview searches chapter labels only.');}
const actions={
 organize:()=>{organizing=!organizing;render();}, bookmarks:()=>{$('.bookmarks').hidden=!$('.bookmarks').hidden;$('[data-action="bookmarks"]').setAttribute('aria-pressed',String(!$('.bookmarks').hidden));},
 previous:()=>go(current-1),next:()=>go(current+1),
 'rotate-left':()=>rotate(-90),'rotate-right':()=>rotate(90),
 'zoom-out':()=>{scale=Math.max(.5,scale-.1);render();},'zoom-in':()=>{scale=Math.min(2,scale+.1);render();},
 undo:()=>{if(undoStack.length){redoStack.push(snapshot());restore(undoStack.pop());}},redo:()=>{if(redoStack.length){undoStack.push(snapshot());restore(redoStack.pop());}},
 copy:()=>{clipboard=pages.filter(p=>selected.has(p.id)).map(p=>({...p}));render();notice(`${clipboard.length} pages copied in the preview.`);},
 cut:()=>{actions.copy();actions.delete();},
 paste:()=>{if(!clipboard.length)return;remember();const additions=clipboard.map(p=>({...p,id:nextId++}));pages.splice(current+1,0,...additions);selected=new Set(additions.map(p=>p.id));render();},
 delete:()=>{if(!selected.size)return;remember();pages=pages.filter(p=>!selected.has(p.id));current=Math.min(current,pages.length-1);selected=new Set(pages[current]?[pages[current].id]:[]);render();},
 insert:()=>notice('Insert pages is available in the native app. This is a visual styling draft.'),extract:()=>notice('Extract is available in the native app. No PDF is created by this draft.'),
 open:()=>notice('This visual draft uses Sample Guide.pdf. The native app opens your PDF files.'),save:()=>notice('Preview changes only. No PDF has been written.'),print:()=>notice('Print is available in the native app. No print job was created.'),'close-tab':()=>notice('This sample stays open so you can compare the styling.'),
 'find-previous':()=>find(-1),'find-next':()=>find(1),'match-case':()=>{matchCase=!matchCase;$('[data-action="match-case"]').setAttribute('aria-pressed',String(matchCase));},dismiss:()=>{$('.notice').hidden=true;}
};
$$('[data-action]').forEach(b=>b.onclick=()=>actions[b.dataset.action]?.());
$('#page-number').onchange=e=>{const n=Number(e.target.value);if(Number.isInteger(n)&&n>=1&&n<=pages.length)go(n-1);else{e.target.value=current+1;notice(`Enter a page from 1 to ${pages.length}.`);}};
$('#zoom').onchange=e=>{const v=e.target.value;scale=v==='Fit Page'?1:v==='Fit Width'?1.35:v==='Actual Size'?1.2:parseInt(v,10)/100;render();};
$('#find').onkeydown=e=>{if(e.key==='Enter')find(e.shiftKey?-1:1);};
const menus={File:[['Open…','open'],['Save','save'],['Print…','print']],Edit:[['Undo','undo'],['Redo','redo'],['Copy pages','copy']],View:[['Bookmarks','bookmarks'],['Organize pages','organize']],Go:[['Previous page','previous'],['Next page','next']],Pages:[['Cut pages','cut'],['Copy pages','copy'],['Paste pages','paste'],['Delete pages','delete'],['Rotate left','rotate-left'],['Rotate right','rotate-right']],Help:[['About this draft','about']]};
$$('[data-menu]').forEach(b=>b.onclick=e=>{e.stopPropagation();const menu=$('.menu');menu.style.left=`${b.getBoundingClientRect().left}px`;menu.innerHTML=menus[b.dataset.menu].map(([label,action])=>`<button data-command="${action}">${label}</button>`).join('');menu.hidden=false;menu.querySelectorAll('button').forEach(item=>item.onclick=()=>{menu.hidden=true;if(item.dataset.command==='about')notice('Current layout, Lucide icons, rounded controls, improved contrast. Visual prototype only.');else actions[item.dataset.command]?.();});});
document.addEventListener('click',e=>{if(!e.target.closest('.menu')&&!e.target.closest('[data-menu]'))$('.menu').hidden=true;});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){$('.menu').hidden=true;$('.notice').hidden=true;}if((e.ctrlKey||e.metaKey)&&e.key==='z'&&document.activeElement.tagName!=='INPUT'){e.preventDefault();actions.undo();}});
render();
