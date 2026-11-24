const stage = document.querySelector('.comparison-stage');
const reference = document.querySelector('#reference');
const draft = document.querySelector('#draft');
function scaleScreens() {
  document.querySelectorAll('.screen').forEach(screen => {
    if (!screen.clientWidth) return;
    const scale = screen.clientWidth / 1264;
    screen.style.height = `${780 * scale}px`;
    screen.firstElementChild.style.transform = `scale(${scale})`;
  });
}
document.querySelectorAll('[data-scene]').forEach(button => button.addEventListener('click', () => {
  const scene = button.dataset.scene;
  document.querySelectorAll('[data-scene]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
  reference.src = scene === 'reading' ? '../reading.png' : '../organize.png';
  reference.alt = `Existing Gravity ${scene} view, saved reference`;
  draft.src = `draft.html?mode=${scene}&embed=1`;
  document.querySelector('.open-draft').href = `draft.html?mode=${scene}`;
}));
document.querySelectorAll('[data-layout]').forEach(button => {
  if (button.tagName !== 'BUTTON') return;
  button.addEventListener('click', () => {
    stage.dataset.layout = button.dataset.layout;
    document.querySelectorAll('button[data-layout]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
    scaleScreens();
  });
});
new ResizeObserver(scaleScreens).observe(stage);
scaleScreens();
