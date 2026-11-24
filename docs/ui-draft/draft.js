const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
const titles = ['Getting started','Reading','Zoom and layout','Searching','Bookmarks','Organizing pages','Cut and paste','Combining files','Rotating','A landscape page','Printing','Keyboard shortcuts','Saving','More information'];
let pages = titles.map((title, index) => ({id:index + 1, title, source:index + 1, rotation:0}));
let current = 0;
let selected = new Set([1]);
let mode = new URLSearchParams(location.search).get('mode') === 'organizing' ? 'organizing' : 'reading';
let navigation = 'outline';
let zoom = 1;
let nextId = 15;
const history = [];
function icon(name) { return (window.gravityIcons[name] || '').replace('<svg ', '<svg aria-hidden="true" focusable="false" '); }
function icons(root = document) { root.querySelectorAll('[data-icon]').forEach(element => { element.outerHTML = icon(element.dataset.icon); }); }
icons();
function notify(message) { $('.notice span').textContent = message; $('.notice').hidden = false; }
function remember() { history.push({pages:pages.map(page => ({...page})), current, selected:[...selected]}); }
function thumb(page) { return `assets/${page.source <= 12 ? `thumb-${page.source}` : 'page-1'}.png`; }
function renderNavigation() {
  const query = $('#find-input').value.toLowerCase().trim();
  $('.outline').innerHTML = pages.map((page,index) => `<button class="chapter" data-page="${index}" ${index === current ? 'aria-current="page"' : ''} ${query && !`${index + 1} ${page.title}`.toLowerCase().includes(query) ? 'hidden' : ''}><span class="number">${index + 1}</span><span>${page.title}</span></button>`).join('');
  $$('.chapter').forEach(button => button.addEventListener('click', () => goTo(Number(button.dataset.page))));
  $('#chapter-position').textContent = pages.length ? `Chapter ${current + 1} of ${pages.length}` : 'No pages';
  $('.page-total').textContent = `${pages.length} pages`;
  $('.page-rail').innerHTML = pages.map((page,index) => tile(page,index,true)).join('');
  $('.page-rail').querySelectorAll('.page-tile').forEach(button => button.addEventListener('click', () => goTo(Number(button.dataset.index))));
  if (query) { const count = $$('.chapter:not([hidden])').length; $('#find-result').textContent = count ? `${count} ${count === 1 ? 'chapter' : 'chapters'}` : 'No chapters found. Try another title.'; }
  else $('#find-result').textContent = '';
}
function tile(page,index,rail=false) {
  const active = rail ? index === current : selected.has(page.id);
  const image = page.source <= 12 ? `<img src="${thumb(page)}" alt="" style="transform:rotate(${page.rotation}deg)">` : `<span class="thumb-placeholder" style="transform:rotate(${page.rotation}deg)"><strong>${page.title}</strong><span>Preview unavailable</span></span>`;
  return `<button class="page-tile" data-index="${index}" aria-label="Page ${index+1}: ${page.title}" aria-pressed="${active}"><span class="thumb-wrap">${image}<span class="selection-check">${icon('check')}</span></span><span class="page-label"><strong>${index+1}</strong><span>${page.title}</span></span></button>`;
}
function renderOrganizer() {
  $('.organizer').innerHTML = pages.map((page,index) => tile(page,index)).join('');
  $('.organizer').querySelectorAll('.page-tile').forEach(button => {
    button.addEventListener('click', event => {
      const index = Number(button.dataset.index), id = pages[index].id;
      if (event.shiftKey) {
        const a = Math.min(current,index), b = Math.max(current,index);
        for (let i=a;i<=b;i++) selected.add(pages[i].id);
      } else if (event.ctrlKey || event.metaKey) {
        selected.has(id) ? selected.delete(id) : selected.add(id);
      } else selected = new Set([id]);
      current = index;
      render();
      $('.organizer').querySelector(`[data-index="${index}"]`)?.focus({preventScroll:true});
    });
    button.addEventListener('dblclick', () => { goTo(Number(button.dataset.index)); setMode('reading'); });
    button.addEventListener('keydown', event => {
      if (event.key === ' ') {
        event.preventDefault();
        const index = Number(button.dataset.index), id = pages[index].id;
        selected.has(id) ? selected.delete(id) : selected.add(id);
        current = index; render();
        $('.organizer').querySelector(`[data-index="${index}"]`)?.focus({preventScroll:true});
      }
    });
  });
}
function render() {
  current = Math.max(0,Math.min(current,pages.length-1));
  renderNavigation(); renderOrganizer();
  $$('.page-count').forEach(element => {element.textContent = `of ${pages.length}`;});
  $('#page-number').value = pages.length ? current+1 : 0;
  $('#page-number').max = pages.length;
  $('#page-number').disabled = !pages.length;
  const page = pages[current];
  if (page) {
    $('#page-image').src = page.source === 1 ? 'assets/page-1.png' : thumb(page);
    $('#page-image').alt = `Sample document, page ${current+1}: ${page.title}`;
    $('.paper').style.transform = `scale(${zoom}) rotate(${page.rotation}deg)`;
    $('.sample-page').hidden = page.source <= 12;
    $('#sample-heading').textContent = `Chapter ${current+1}: ${page.title}`;
  }
  $('#selected-count').textContent = `${selected.size} ${selected.size === 1 ? 'page' : 'pages'} selected`;
  $('#status-left').textContent = mode === 'reading' ? (pages.length ? `Page ${current+1} of ${pages.length}` : '0 pages') : `${pages.length} pages · ${selected.size} selected`;
  $('#status-right').textContent = mode === 'reading' ? 'Nothing is written until you save' : 'Ctrl / ⌘ click to select · Double-click to read';
  $('#save-state').textContent = history.length ? 'Preview changes · not saved to a PDF' : 'Sample document';
  $$('[data-action="undo"]').forEach(button => {button.disabled = !history.length;});
  $$('[data-action="rotate"],[data-action="duplicate"],[data-action="delete"]').forEach(button => {button.disabled = !selected.size;});
  $('[data-action="move-up"]').disabled = selected.size !== 1 || pages.findIndex(p => selected.has(p.id)) <= 0;
  $('[data-action="move-down"]').disabled = selected.size !== 1 || pages.findIndex(p => selected.has(p.id)) >= pages.length-1;
  $('[data-action="previous"]').disabled = !pages.length || current === 0;
  $('[data-action="next"]').disabled = !pages.length || current === pages.length-1;
  $('.reader').hidden = mode !== 'reading' || !pages.length;
  $('.organizer').hidden = mode !== 'organizing' || !pages.length;
  $('.empty-state').hidden = !!pages.length;
}
function goTo(index) { current = Math.max(0,Math.min(index,pages.length-1)); if(pages[current]) selected = new Set([pages[current].id]); zoom=1; $('#zoom-label').textContent='Fit page'; render(); if(innerWidth<=600) setSidebar(false); }
function setMode(value) {
  mode = value;
  $$('[data-mode]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.mode===mode)));
  $('.read-controls').hidden = mode !== 'reading';
  $('.organize-controls').hidden = mode !== 'organizing';
  render();
}
function setSidebar(show) { $('.sidebar').hidden=!show; $('[data-action="sidebar"]').setAttribute('aria-expanded',String(show)); $('[data-action="sidebar"]').setAttribute('aria-label',`${show?'Hide':'Show'} navigation sidebar`); }
function search(show) { $('.findbar').hidden=!show; $('[data-action="find"]').setAttribute('aria-expanded',String(show)); if(show) { setSidebar(true); setNavigation('outline'); $('#find-input').focus(); } else { $('#find-input').value=''; renderNavigation(); $('[data-action="find"]').focus(); } }
function setNavigation(value) { navigation=value; $('.outline').hidden=value!=='outline'; $('.page-rail').hidden=value!=='pages'; $$('[data-nav]').forEach(b => b.setAttribute('aria-pressed',String(b.dataset.nav===value))); }
function undo() { if(!history.length)return; const state=history.pop(); pages=state.pages; current=state.current; selected=new Set(state.selected); render(); notify('Page change undone.'); }
const actions = {
  sidebar:()=>setSidebar($('.sidebar').hidden), find:()=>search($('.findbar').hidden), 'close-find':()=>search(false),
  previous:()=>goTo(current-1), next:()=>goTo(current+1), undo,
  'select-all':()=>{ selected=new Set(pages.map(p=>p.id)); render(); },
  rotate:()=>{ if(!selected.size)return; remember(); pages.forEach(p=>{if(selected.has(p.id))p.rotation=(p.rotation+90)%360;}); render(); },
  duplicate:()=>{ if(!selected.size)return; remember(); const ids=[]; pages=pages.flatMap(p=>{if(!selected.has(p.id))return [p]; const copy={...p,id:nextId++}; ids.push(copy.id); return [p,copy];}); selected=new Set(ids); render(); notify('Selected pages duplicated in the preview.'); },
  delete:()=>{ if(!selected.size)return; remember(); pages=pages.filter(p=>!selected.has(p.id)); current=Math.min(current,pages.length-1); selected=new Set(pages[current]?[pages[current].id]:[]); render(); notify('Selected pages removed. Use Undo to restore them.'); },
  'move-up':()=>move(-1), 'move-down':()=>move(1),
  'zoom-in':()=>changeZoom(.15), 'zoom-out':()=>changeZoom(-.15), fit:()=>{zoom=1; $('#zoom-label').textContent='Fit page';render();},
  'dismiss-notice':()=>{$('.notice').hidden=true;}
};
function move(delta) { if(selected.size!==1)return; const index=pages.findIndex(p=>selected.has(p.id)), target=index+delta; if(target<0||target>=pages.length)return; remember(); [pages[index],pages[target]]=[pages[target],pages[index]]; current=target;render(); }
function changeZoom(delta) { zoom=Math.max(.55,Math.min(2,zoom+delta)); $('#zoom-label').textContent=`${Math.round(zoom*100)}%`; render(); }
$$('[data-action]').forEach(button=>button.addEventListener('click',()=>actions[button.dataset.action]?.()));
$$('[data-mode]').forEach(button=>button.addEventListener('click',()=>setMode(button.dataset.mode)));
$$('[data-nav]').forEach(button=>button.addEventListener('click',()=>setNavigation(button.dataset.nav)));
$$('[data-notice]').forEach(button=>button.addEventListener('click',()=>notify(button.dataset.notice)));
$('#find-input').addEventListener('input',renderNavigation);
$('#page-number').addEventListener('change',event=>{const value=Number(event.target.value); if(Number.isInteger(value)&&value>=1&&value<=pages.length)goTo(value-1);else {event.target.value=current+1;notify(`Enter a page number between 1 and ${pages.length}.`);}});
document.addEventListener('keydown',event=>{
  if(event.key==='Escape') { $('.notice').hidden=true; if(!$('.findbar').hidden)search(false); }
  if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='z'&&!['INPUT','TEXTAREA'].includes(document.activeElement.tagName)) {event.preventDefault();undo();}
});
if(innerWidth<=600)setSidebar(false);
matchMedia('(max-width:600px)').addEventListener('change',event=>setSidebar(!event.matches));
setMode(mode);
