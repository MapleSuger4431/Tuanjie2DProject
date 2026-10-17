// 主逻辑 v1.0（策划案 §2 核心循环）：实时铺格（可覆盖、有上限）→ AI 按偏好走 → 三星结算
import { W, H, TERRAIN, EFFECT, EFFECT_INFO, generateLevel, levelElement, LEVEL_CONFIG } from './model.js';
import { randomPiece, rotated } from './pieces.js';
import { simulate, prefOrder } from './walker.js';
import { Renderer, cellToWorld } from './render.js';

const $ = (id) => document.getElementById(id);

const state = {
  level: 1,
  element: 'ice',
  pref: 'right',
  phase: 'build',      // build | run
  board: null,
  piece: null,
  ghost: null,
  usedPieces: 0,
  history: [],         // [{cells:[{x,y,terrain,effect}]}] 铺前快照，用于撤销
  coinGot: false,
  startTime: 0,        // 本关计时起点（ms）
  lastSim: null,       // 最近一次可达性模拟
};

let renderer;

function toast(msg, ms = 2400) {
  const el = $('toast');
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(toast._t);
  toast._t = setTimeout(() => el.classList.remove('show'), ms);
}

function elapsedSec() {
  return (performance.now() - state.startTime) / 1000;
}
function fmtTime(sec) {
  const m = Math.floor(sec / 60), s = Math.floor(sec % 60);
  return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
}

function updateHUD() {
  $('hud-level').textContent = state.level;
  $('hud-pieces').textContent = state.usedPieces + '/' + LEVEL_CONFIG.pieceLimit;
  $('hud-element').textContent = state.element === 'ice' ? '🧊 冰' : '🔥 火';
  $('hud-coin').textContent = state.coinGot ? '1/1' : '0/1';
}

setInterval(() => {
  if (state.phase !== 'run' && !state.startTime) return;
  $('hud-time').textContent = fmtTime(elapsedSec());
}, 250);

// 可达性预检：同规则模拟一遍（策划案 §9）
function precheck() {
  const sim = simulate(state.board, state.board.start, state.board.end, state.element, prefOrder(state.pref));
  state.lastSim = sim;
  const badge = $('hud-reach');
  if (sim.success) {
    badge.textContent = '✓ 可达';
    badge.className = 'reach ok';
    $('btn-run').disabled = false;
  } else {
    badge.textContent = '✗ 不可达';
    badge.className = 'reach bad';
    $('btn-run').disabled = true;
  }
  return sim;
}

const TERRAIN_CSS = { [TERRAIN.LAND]: '#4c6b3c', [TERRAIN.ICE]: '#62c8f2', [TERRAIN.FIRE]: '#e0682e' };
function drawPreview() {
  const cv = $('preview');
  const g = cv.getContext('2d');
  g.clearRect(0, 0, cv.width, cv.height);
  if (!state.piece) return;
  const s = 26;
  const ox = (cv.width - state.piece.w * s) / 2;
  const oy = (cv.height - state.piece.h * s) / 2;
  for (const c of state.piece.cells) {
    g.fillStyle = TERRAIN_CSS[c.terrain];
    g.fillRect(ox + c.x * s + 1, oy + c.y * s + 1, s - 2, s - 2);
    if (c.effect !== EFFECT.NONE) {
      g.fillStyle = '#fff';
      g.font = 'bold 13px sans-serif';
      g.textAlign = 'center';
      g.textBaseline = 'middle';
      g.fillText(EFFECT_INFO[c.effect].icon, ox + c.x * s + s / 2, oy + c.y * s + s / 2 + 1);
    }
  }
}

function loadLevel() {
  state.board = generateLevel(state.level);
  state.element = levelElement(state.level);
  state.usedPieces = 0;
  state.history = [];
  state.coinGot = false;
  state.startTime = performance.now();
  state.phase = 'build';
  state.piece = randomPiece();
  renderer.refreshCells(state.board);
  renderer.buildMarkers(state.board);
  renderer.hero.visible = true;
  renderer.setHeroElement(state.element);
  renderer.setHeroCell(state.board.start.x, state.board.start.y);
  drawPreview();
  updateHUD();
  precheck();
}

// ---------- 铺格（任意位置、可覆盖；锁定格/绝对禁止格除外）----------
function ghostOrigin() {
  if (!state.ghost || !state.piece) return null;
  return {
    x: state.ghost.x - Math.floor(state.piece.w / 2),
    y: state.ghost.y - Math.floor(state.piece.h / 2),
  };
}

function canPlace(ox, oy) {
  if (state.usedPieces >= LEVEL_CONFIG.pieceLimit) return false;
  return state.piece.cells.every(c => state.board.canOverwrite(ox + c.x, oy + c.y));
}

function refreshGhost() {
  if (state.phase !== 'build') { renderer.hideGhost(); return; }
  const o = ghostOrigin();
  if (!o) { renderer.hideGhost(); return; }
  renderer.showGhost(state.piece, o.x, o.y, canPlace(o.x, o.y));
}

function placePiece() {
  const o = ghostOrigin();
  if (!o) return;
  if (state.usedPieces >= LEVEL_CONFIG.pieceLimit) { toast('块数用完了，只能出发！'); return; }
  if (!canPlace(o.x, o.y)) { toast('不能放在「绝对禁止」格或标记格上'); return; }
  const prev = [];
  for (const c of state.piece.cells) {
    const x = o.x + c.x, y = o.y + c.y;
    prev.push({ x, y, terrain: state.board.terrainAt(x, y), effect: state.board.effectAt(x, y) });
    state.board.setCell(x, y, c.terrain, c.effect);
  }
  state.history.push(prev);
  state.usedPieces++;
  state.piece = randomPiece();
  renderer.refreshCells(state.board);
  drawPreview();
  updateHUD();
  precheck();
  refreshGhost();
  if (state.usedPieces >= LEVEL_CONFIG.pieceLimit) toast('块数已用完，点「出发」！');
}

function undo() {
  const prev = state.history.pop();
  if (!prev) { toast('没有可撤销的铺设'); return; }
  for (const c of prev) state.board.setCell(c.x, c.y, c.terrain, c.effect);
  state.usedPieces--;
  renderer.refreshCells(state.board);
  updateHUD();
  precheck();
  refreshGhost();
}

// ---------- 执行阶段：按模拟事件驱动动画 ----------
function moveHeroTo(from, to, ms = 140) {
  return new Promise((resolve) => {
    const a = cellToWorld(from.x, from.y);
    const b = cellToWorld(to.x, to.y);
    const t0 = performance.now();
    const off = renderer.addFrameCallback(() => {
      const k = Math.min(1, (performance.now() - t0) / ms);
      const e = k * (2 - k);
      renderer.hero.position.set(
        a.wx + (b.wx - a.wx) * e,
        0.2 + Math.sin(k * Math.PI) * 0.14,
        a.wz + (b.wz - a.wz) * e
      );
      if (k >= 1) { off(); resolve(); }
    });
  });
}

async function playEvents(sim) {
  for (const ev of sim.events) {
    if (ev.type === 'move') {
      await moveHeroTo(ev.from, ev.to);
    } else if (ev.type === 'backtrack') {
      await moveHeroTo(ev.from, ev.to, 90);
    } else if (ev.type === 'portal') {
      renderer.flashAt(ev.from.x, ev.from.y, 0xc95cff);
      await new Promise(r => setTimeout(r, 200));
      renderer.setHeroCell(ev.to.x, ev.to.y);
      renderer.flashAt(ev.to.x, ev.to.y, 0xc95cff);
      await new Promise(r => setTimeout(r, 120));
    } else if (ev.type === 'force' || ev.type === 'slide') {
      const dist = Math.abs(ev.to.x - ev.from.x) + Math.abs(ev.to.y - ev.from.y);
      renderer.flashAt(ev.from.x, ev.from.y, ev.type === 'force' ? 0xffd84a : 0x4affd8);
      await moveHeroTo(ev.from, ev.to, Math.max(120, dist * 70));
    } else if (ev.type === 'convert') {
      renderer.flashAt(ev.x, ev.y, 0xff9df3);
      renderer.setHeroElement(ev.element);
      state.element = ev.element;
      updateHUD();
      toast(ev.element === 'ice' ? '属性转化 → 🧊 冰' : '属性转化 → 🔥 火', 1200);
      await new Promise(r => setTimeout(r, 220));
    } else if (ev.type === 'coin') {
      state.coinGot = true;
      renderer.removeCoin(ev.idx);
      renderer.flashAt(ev.x, ev.y, 0xffd84a);
      updateHUD();
      toast('捡到金币 ★', 1200);
    }
  }
}

function showSettle(stars, sec) {
  $('settle-level').textContent = state.level;
  $('settle-stars').textContent = '★'.repeat(stars) + '☆'.repeat(3 - stars);
  $('settle-detail').textContent =
    `用时 ${fmtTime(sec)}（三星线 ${LEVEL_CONFIG.targetTime}s）` +
    ` · 金币 ${state.coinGot ? '✓' : '✗'} · 铺设 ${state.usedPieces} 块`;
  $('settle').classList.remove('hidden');
}

async function run() {
  if (state.phase !== 'build') return;
  const sim = state.lastSim || precheck();
  if (!sim.success) { toast('当前棋盘走不通，继续调整'); return; }
  state.phase = 'run';
  renderer.hideGhost();
  $('btn-run').disabled = true;

  await playEvents(sim);

  if (sim.success) {
    const sec = elapsedSec();
    const stars = 1 + (sim.coin ? 1 : 0) + (sec <= LEVEL_CONFIG.targetTime ? 1 : 0);
    renderer.flashAt(state.board.end.x, state.board.end.y, 0xffd54a);
    state.phase = 'build';
    showSettle(stars, sec);
  } else {
    toast(`失败：${sim.reason} —— 棋盘保留，继续调整后再出发`);
    renderer.setHeroCell(state.board.start.x, state.board.start.y);
    state.element = levelElement(state.level);
    renderer.setHeroElement(state.element);
    state.phase = 'build';
    $('btn-run').disabled = false;
    updateHUD();
    precheck();
    refreshGhost();
  }
}

// ---------- 输入与初始化 ----------
function init() {
  renderer = new Renderer($('scene'));

  const cv = renderer.renderer.domElement;
  cv.addEventListener('mousemove', (e) => {
    if (state.phase !== 'build') return;
    const r = cv.getBoundingClientRect();
    state.ghost = renderer.pick(
      ((e.clientX - r.left) / r.width) * 2 - 1,
      -((e.clientY - r.top) / r.height) * 2 + 1
    );
    refreshGhost();
  });
  cv.addEventListener('mouseleave', () => { state.ghost = null; renderer.hideGhost(); });
  cv.addEventListener('click', () => {
    if (state.phase !== 'build' || !state.ghost) return;
    placePiece();
  });
  cv.addEventListener('contextmenu', (e) => e.preventDefault());

  window.addEventListener('keydown', (e) => {
    if (e.code === 'KeyR' && state.phase === 'build') {
      state.piece = rotated(state.piece);
      drawPreview();
      refreshGhost();
    } else if (e.code === 'KeyZ' && state.phase === 'build') {
      undo();
    } else if (e.code === 'Space') {
      e.preventDefault();
      run();
    } else if (e.code === 'Escape') {
      $('help').classList.add('hidden');
    }
  });

  $('btn-run').addEventListener('click', run);
  $('btn-undo').addEventListener('click', () => state.phase === 'build' && undo());
  $('btn-reset').addEventListener('click', () => {
    if (state.phase !== 'build') return;
    loadLevel();
    toast('本关已重置，计时清零');
  });
  $('btn-help').addEventListener('click', () => $('help').classList.toggle('hidden'));
  $('help-close').addEventListener('click', () => $('help').classList.add('hidden'));
  $('btn-next').addEventListener('click', () => {
    $('settle').classList.add('hidden');
    state.level++;
    loadLevel();
  });

  for (const btn of document.querySelectorAll('#pref button')) {
    btn.addEventListener('click', () => {
      state.pref = btn.dataset.p;
      document.querySelectorAll('#pref button').forEach(b => b.classList.toggle('active', b === btn));
      precheck();
    });
  }

  loadLevel();
}

init();
