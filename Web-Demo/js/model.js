// 棋盘数据模型 v1.0：20×20，每格两层 —— 地形 + 特效（对齐 docs/edge-grid-design.md）
export const W = 20, H = 20;

export const TERRAIN = { EMPTY: 0, LAND: 1, ICE: 2, FIRE: 3, FORBIDDEN: 4 };
export const TERRAIN_INFO = {
  [TERRAIN.EMPTY]:     { name: '空',       color: 0x141a30, desc: '谁都不能走（可铺设）' },
  [TERRAIN.LAND]:      { name: '陆地',     color: 0x4c6b3c, desc: '任何属性都能走' },
  [TERRAIN.ICE]:       { name: '冰',       color: 0x62c8f2, desc: '只有冰属性能走' },
  [TERRAIN.FIRE]:      { name: '火',       color: 0xe0682e, desc: '只有火属性能走' },
  [TERRAIN.FORBIDDEN]: { name: '绝对禁止', color: 0x300a14, desc: '不能走，也不能被覆盖' },
};

export const EFFECT = { NONE: 0, PORTAL: 1, FORCE_UP: 2, FORCE_DOWN: 3, FORCE_DIAG: 4, SLIDE: 5, CONVERT: 6 };
export const EFFECT_INFO = {
  [EFFECT.NONE]:       { name: '',         icon: '',    desc: '' },
  [EFFECT.PORTAL]:     { name: '传送门',   icon: '◎',   desc: '传送到最近的另一个传送门（只有 1 个时不触发）' },
  [EFFECT.FORCE_UP]:   { name: '强制位移', icon: '↑4',  desc: '直接往上走 4 格' },
  [EFFECT.FORCE_DOWN]: { name: '强制位移', icon: '↓3',  desc: '直接往下走 3 格' },
  [EFFECT.FORCE_DIAG]: { name: '强制位移', icon: '↖2',  desc: '斜着往左走 2 格' },
  [EFFECT.SLIDE]:      { name: '持续滑行', icon: '≫',   desc: '沿进入方向滑到最外围的可走格' },
  [EFFECT.CONVERT]:    { name: '属性转化', icon: '⇄',   desc: '冰 ↔ 火 属性切换（只由关卡生成）' },
};
export const FORCE_VECTORS = {
  [EFFECT.FORCE_UP]:   { dx: 0,  dy: -1, dist: 4 },
  [EFFECT.FORCE_DOWN]: { dx: 0,  dy: 1,  dist: 3 },
  [EFFECT.FORCE_DIAG]: { dx: -1, dy: -1, dist: 2 },
};

const mrand = (n) => Math.floor(Math.random() * n);

export class Board {
  constructor() {
    this.terrain = new Uint8Array(W * H);
    this.effect = new Uint8Array(W * H);
    this.coins = new Set();   // idx
    this.locked = new Set();  // 标记格（起点/终点/转化格/金币）+ 绝对禁止格：不可覆盖
    this.start = null;
    this.end = null;
  }

  idx(x, y) { return y * W + x; }
  inBounds(x, y) { return x >= 0 && x < W && y >= 0 && y < H; }
  terrainAt(x, y) { return this.inBounds(x, y) ? this.terrain[this.idx(x, y)] : TERRAIN.FORBIDDEN; }
  effectAt(x, y) { return this.inBounds(x, y) ? this.effect[this.idx(x, y)] : EFFECT.NONE; }
  hasCoin(x, y) { return this.coins.has(this.idx(x, y)); }

  setCell(x, y, terrain, effect = EFFECT.NONE) {
    const i = this.idx(x, y);
    this.terrain[i] = terrain;
    this.effect[i] = effect;
  }

  // 元素感知可走性：陆地都能走；冰/火要属性匹配；空/绝对禁止不能走
  walkable(x, y, element) {
    if (!this.inBounds(x, y)) return false;
    const t = this.terrain[this.idx(x, y)];
    if (t === TERRAIN.LAND) return true;
    if (t === TERRAIN.ICE && element === 'ice') return true;
    if (t === TERRAIN.FIRE && element === 'fire') return true;
    return false;
  }

  canOverwrite(x, y) {
    if (!this.inBounds(x, y)) return false;
    const i = this.idx(x, y);
    return !this.locked.has(i) && this.terrain[i] !== TERRAIN.FORBIDDEN;
  }

  portalCells() {
    const out = [];
    for (let i = 0; i < W * H; i++) if (this.effect[i] === EFFECT.PORTAL) out.push({ x: i % W, y: Math.floor(i / W) });
    return out;
  }

  nearestOtherPortal(x, y) {
    let best = null, bestD = Infinity;
    for (const p of this.portalCells()) {
      if (p.x === x && p.y === y) continue;
      const d = (p.x - x) ** 2 + (p.y - y) ** 2;
      if (d < bestD) { bestD = d; best = p; }
    }
    return best;
  }
}

// ---------- 关卡生成（策划案 §6）----------
export const LEVEL_CONFIG = {
  pieceLimit: 40,   // 每关块数上限
  targetTime: 90,   // 三星时间（秒）
};

function farEnough(x, y, others, minD) {
  return others.every(o => Math.abs(o.x - x) + Math.abs(o.y - y) >= minD);
}
function randomCell(others, minD, xRange = [1, W - 2], yRange = [1, H - 2]) {
  for (let t = 0; t < 300; t++) {
    const x = xRange[0] + mrand(xRange[1] - xRange[0] + 1);
    const y = yRange[0] + mrand(yRange[1] - yRange[0] + 1);
    if (farEnough(x, y, others, minD)) return { x, y };
  }
  return { x: xRange[0], y: yRange[0] };
}

// 在棋盘上随机游走铺一个碎块（n 格同地形）
function paintFragment(b, x, y, terrain, n, forbidden) {
  let cx = x, cy = y;
  for (let s = 0; s < n; s++) {
    if (!b.inBounds(cx, cy)) break;
    const i = b.idx(cx, cy);
    if (!b.locked.has(i) && b.terrain[i] !== TERRAIN.FORBIDDEN && !forbidden.has(i)) {
      b.terrain[i] = terrain;
    }
    const dirs = [[1, 0], [-1, 0], [0, 1], [0, -1]];
    const d = dirs[mrand(4)];
    cx += d[0]; cy += d[1];
  }
}

export function generateLevel(level) {
  const b = new Board();
  const used = [];

  b.start = randomCell(used, 0, [0, 1], [2, H - 3]); used.push(b.start);
  b.end = randomCell(used, 12, [W - 2, W - 1], [2, H - 3]); used.push(b.end);

  // 属性转化格：第 1 关 1 个，之后每 2 关 +1（最多 3 个），放中央区域
  const convertCount = Math.min(1 + Math.floor((level - 1) / 2), 3);
  const converts = [];
  for (let i = 0; i < convertCount; i++) {
    const c = randomCell(used, 5, [7, 12], [2, H - 3]); used.push(c); converts.push(c);
  }

  // 金币 1 枚（可选）
  const coin = randomCell(used, 4); used.push(coin);
  b.coins.add(b.idx(coin.x, coin.y));

  // 绝对禁止格：密度随关卡上升（12 + 2/关，封顶 34）
  const forbCount = Math.min(12 + level * 2, 34);
  const forbIdx = new Set();
  for (let i = 0; i < forbCount; i++) {
    const c = randomCell(used, 2);
    const ii = b.idx(c.x, c.y);
    if (forbIdx.has(ii)) continue;
    forbIdx.add(ii);
    b.terrain[ii] = TERRAIN.FORBIDDEN;
    b.locked.add(ii);
    used.push(c);
  }

  // 预铺地形碎块：6~10 条不连通的陆地/冰/火残局，高关冰火占比提高
  const fragCount = 6 + Math.min(level, 4);
  for (let f = 0; f < fragCount; f++) {
    const c = randomCell(used, 1);
    const r = Math.random();
    const iceBias = Math.min(0.2 + level * 0.06, 0.45);
    const terrain = r < iceBias ? TERRAIN.ICE : r < iceBias * 2 ? TERRAIN.FIRE : TERRAIN.LAND;
    paintFragment(b, c.x, c.y, terrain, 2 + mrand(4), forbIdx);
    used.push(c);
  }

  // 标记格铺底并锁定：起点/终点/转化格 = 陆地，金币格 = 陆地
  for (const c of [b.start, b.end, ...converts, coin]) {
    const i = b.idx(c.x, c.y);
    b.terrain[i] = TERRAIN.LAND;
    b.effect[i] = EFFECT.NONE;
    b.locked.add(i);
  }
  for (const c of converts) b.effect[b.idx(c.x, c.y)] = EFFECT.CONVERT;

  return b;
}

// 每关指定的 AI 初始属性：奇数关冰人，偶数关火人
export function levelElement(level) {
  return level % 2 === 1 ? 'ice' : 'fire';
}
