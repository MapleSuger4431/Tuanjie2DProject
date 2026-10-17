// AI 走法模拟器 v1.0：按方向偏好贪心走 + 死路回溯（策划案 §4）
// 同一套模拟既用于「可达性预检」，也用于执行阶段驱动动画，保证预言=实况。
import { W, EFFECT, FORCE_VECTORS } from './model.js';

export const MAX_CHAIN = 20;   // 特效连锁步数上限
export const MAX_STEPS = 800;  // 全局步数保险丝

const BASE_ORDER = ['right', 'down', 'left', 'up'];
const VEC = {
  right: { dx: 1, dy: 0 },
  down:  { dx: 0, dy: 1 },
  left:  { dx: -1, dy: 0 },
  up:    { dx: 0, dy: -1 },
};

// 偏好序：以选中方向为首，其余按默认 右>下>左>上 轮转
export function prefOrder(first) {
  const i = BASE_ORDER.indexOf(first);
  const seq = [...BASE_ORDER.slice(i), ...BASE_ORDER.slice(0, i)];
  return seq.map(k => ({ name: k, ...VEC[k] }));
}

export function simulate(board, start, end, element, pref) {
  const events = [];
  const dead = new Set();
  const stack = [{ ...start }];
  let pos = { ...start };
  let el = element;
  let coinGot = false;
  let chain = 0;
  let lastDir = pref[0];
  let guard = 0;

  const key = (x, y) => y * W + x;
  const inStack = (x, y) => stack.some(c => c.x === x && c.y === y);

  // 位移：沿 (dx,dy) 走 dist 格，停在前路不可走之前；一格都走不了就原地
  function displace(dx, dy, dist) {
    let nx = pos.x, ny = pos.y;
    for (let s = 0; s < dist; s++) {
      if (!board.walkable(nx + dx, ny + dy, el)) break;
      nx += dx; ny += dy;
    }
    return { x: nx, y: ny };
  }

  // 结算当前落点：金币 + 特效链。返回 { fail? }
  function settle() {
    for (;;) {
      if (board.hasCoin(pos.x, pos.y) && !coinGot) {
        coinGot = true;
        events.push({ type: 'coin', x: pos.x, y: pos.y, idx: key(pos.x, pos.y) });
      }
      const eff = board.effectAt(pos.x, pos.y);
      if (eff === EFFECT.NONE) return {};

      if (eff === EFFECT.CONVERT) {
        if (++chain > MAX_CHAIN) return { fail: '特效连锁超限' };
        el = el === 'ice' ? 'fire' : 'ice';
        events.push({ type: 'convert', x: pos.x, y: pos.y, element: el });
        return {};
      }
      if (eff === EFFECT.PORTAL) {
        const t = board.nearestOtherPortal(pos.x, pos.y);
        if (!t) return {}; // 只有一个传送门：不触发
        if (++chain > MAX_CHAIN) return { fail: '特效连锁超限' };
        // 落点必须当前属性可走；否则试 4 邻（按偏好序）；都不行则不传送
        let dest = null;
        const cands = [t, ...pref.map(d => ({ x: t.x + d.dx, y: t.y + d.dy }))];
        for (const c of cands) {
          if (board.walkable(c.x, c.y, el)) { dest = c; break; }
        }
        if (!dest) return {};
        events.push({ type: 'portal', from: { ...pos }, to: { ...dest } });
        pos = { ...dest };
        // 落点必是另一个传送门：不再连锁触发，防止两个门之间乒乓死循环
        if (board.effectAt(pos.x, pos.y) === EFFECT.PORTAL) return {};
        continue;
      }
      if (eff === EFFECT.FORCE_UP || eff === EFFECT.FORCE_DOWN || eff === EFFECT.FORCE_DIAG) {
        if (++chain > MAX_CHAIN) return { fail: '特效连锁超限' };
        const v = FORCE_VECTORS[eff];
        const dest = displace(v.dx, v.dy, v.dist);
        lastDir = { name: 'force', dx: Math.sign(v.dx), dy: Math.sign(v.dy) };
        events.push({ type: 'force', effect: eff, from: { ...pos }, to: dest });
        pos = dest;
        continue;
      }
      if (eff === EFFECT.SLIDE) {
        if (++chain > MAX_CHAIN) return { fail: '特效连锁超限' };
        const dest = displace(lastDir.dx, lastDir.dy, Math.max(W, W));
        events.push({ type: 'slide', from: { ...pos }, to: dest });
        pos = dest;
        continue;
      }
      return {};
    }
  }

  // 起点若带特效先结算（关卡生成不会放，但防御一次）
  const first = settle();
  if (first.fail) return { success: false, reason: first.fail, events, coin: coinGot };

  while (guard++ < MAX_STEPS) {
    if (pos.x === end.x && pos.y === end.y) {
      return { success: true, events, coin: coinGot };
    }
    let moved = false;
    for (const d of pref) {
      const nx = pos.x + d.dx, ny = pos.y + d.dy;
      if (!board.walkable(nx, ny, el)) continue;
      if (dead.has(key(nx, ny)) || inStack(nx, ny)) continue;
      const from = { ...pos };
      pos = { x: nx, y: ny };
      lastDir = d;
      stack.push({ ...pos });
      events.push({ type: 'move', from, to: { ...pos }, element: el });
      const r = settle();
      if (r.fail) return { success: false, reason: r.fail, events, coin: coinGot };
      if (!inStack(pos.x, pos.y)) stack.push({ ...pos });
      moved = true;
      break;
    }
    if (!moved) {
      dead.add(key(pos.x, pos.y));
      stack.pop();
      if (!stack.length) return { success: false, reason: '无路可走', events, coin: coinGot };
      const back = stack[stack.length - 1];
      events.push({ type: 'backtrack', from: { ...pos }, to: { ...back } });
      pos = { ...back };
    }
  }
  return { success: false, reason: '步数超限', events, coin: coinGot };
}
