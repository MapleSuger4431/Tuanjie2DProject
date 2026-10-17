// 拼块生成 v1.0：2~5 格，3×3 九宫格内随机连通形状 + 随机旋转（策划案 §5）
import { TERRAIN, EFFECT } from './model.js';

const prand = (n) => Math.floor(Math.random() * n);

// 从 3×3 中心随机游走，长出 n 格连通块
function randomShape() {
  const n = 2 + prand(4); // 2~5 格
  const cells = [[1, 1]];
  const has = new Set(['1,1']);
  while (cells.length < n) {
    const [cx, cy] = cells[prand(cells.length)];
    const dirs = [[1, 0], [-1, 0], [0, 1], [0, -1]];
    const [dx, dy] = dirs[prand(4)];
    const nx = cx + dx, ny = cy + dy;
    if (nx < 0 || nx > 2 || ny < 0 || ny > 2 || has.has(nx + ',' + ny)) continue;
    cells.push([nx, ny]);
    has.add(nx + ',' + ny);
  }
  return cells;
}

function normalize(cells) {
  const minX = Math.min(...cells.map(c => c[0]));
  const minY = Math.min(...cells.map(c => c[1]));
  return cells.map(([x, y]) => [x - minX, y - minY]);
}

function rollTerrain() {
  const r = Math.random();
  if (r < 0.5) return TERRAIN.LAND;
  if (r < 0.75) return TERRAIN.ICE;
  return TERRAIN.FIRE;
}

// 特效池：传送门/强制位移/滑行（属性转化不进拼块池）
function rollEffect(hasPortal) {
  const r = Math.random();
  if (r < 0.85) return EFFECT.NONE;
  if (r < 0.89) return hasPortal ? EFFECT.NONE : EFFECT.PORTAL;
  if (r < 0.92) return EFFECT.FORCE_UP;
  if (r < 0.95) return EFFECT.FORCE_DOWN;
  if (r < 0.98) return EFFECT.FORCE_DIAG;
  return EFFECT.SLIDE;
}

function build(cells) {
  let hasPortal = false;
  const out = cells.map(([x, y]) => {
    const effect = rollEffect(hasPortal);
    if (effect === EFFECT.PORTAL) hasPortal = true;
    return { x, y, terrain: rollTerrain(), effect };
  });
  return {
    cells: out,
    w: Math.max(...out.map(c => c.x)) + 1,
    h: Math.max(...out.map(c => c.y)) + 1,
  };
}

export function randomPiece() {
  let cells = randomShape();
  const turns = prand(4);
  for (let i = 0; i < turns; i++) {
    cells = normalize(cells.map(([x, y]) => [-y, x]));
  }
  return build(normalize(cells));
}

export function rotated(piece) {
  const cells = normalize(piece.cells.map(c => [-c.y, c.x]));
  const out = piece.cells.map(c => ({ x: -c.y, y: c.x, terrain: c.terrain, effect: c.effect }));
  const minX = Math.min(...out.map(c => c.x));
  const minY = Math.min(...out.map(c => c.y));
  for (const c of out) { c.x -= minX; c.y -= minY; }
  void cells;
  return {
    cells: out,
    w: Math.max(...out.map(c => c.x)) + 1,
    h: Math.max(...out.map(c => c.y)) + 1,
  };
}
