// Three.js 渲染层 v1.0：棋盘、特效图标、标记物、元素英雄、幽灵预览、拾取
import * as THREE from 'three';
import { W, H, TERRAIN, TERRAIN_INFO, EFFECT, EFFECT_INFO } from './model.js';

const HALF = (W - 1) / 2;

export function cellToWorld(x, y) {
  return { wx: x - HALF, wz: y - HALF };
}
export function worldToCell(wx, wz) {
  return { x: Math.round(wx + HALF), y: Math.round(wz + HALF) };
}

function makeIconTexture(glyph, color) {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d');
  g.font = 'bold 72px "PingFang SC", "Microsoft YaHei", sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.shadowColor = color;
  g.shadowBlur = 16;
  g.lineWidth = 6;
  g.strokeStyle = 'rgba(5,8,18,0.9)';
  g.strokeText(glyph, 64, 68);
  g.fillStyle = color;
  g.fillText(glyph, 64, 68);
  const t = new THREE.CanvasTexture(c);
  t.anisotropy = 4;
  return t;
}

const EFFECT_STYLE = {
  [EFFECT.PORTAL]:     '#c95cff',
  [EFFECT.FORCE_UP]:   '#ffd84a',
  [EFFECT.FORCE_DOWN]: '#ffd84a',
  [EFFECT.FORCE_DIAG]: '#ffd84a',
  [EFFECT.SLIDE]:      '#4affd8',
  [EFFECT.CONVERT]:    '#ff9df3',
};

export const HERO_COLORS = {
  ice:  { body: 0xbfeaff, ring: 0x62c8f2 },
  fire: { body: 0xffd0a8, ring: 0xe0682e },
};

export class Renderer {
  constructor(container) {
    this.container = container;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x0a0e1a);

    this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
    this.camera.position.set(0, 26, 15);
    this.camera.lookAt(0, 0, 0.5);

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(this.renderer.domElement);

    this.scene.add(new THREE.HemisphereLight(0x9fc0ff, 0x1a1024, 1.0));
    const dir = new THREE.DirectionalLight(0xffffff, 1.4);
    dir.position.set(8, 20, 6);
    this.scene.add(dir);

    const base = new THREE.Mesh(
      new THREE.BoxGeometry(W + 0.8, 0.1, H + 0.8),
      new THREE.MeshLambertMaterial({ color: 0x0d1226 })
    );
    base.position.y = -0.06;
    this.scene.add(base);
    const grid = new THREE.GridHelper(W, W, 0x2a3560, 0x1a2140);
    grid.position.y = 0.005;
    this.scene.add(grid);

    this.pickPlane = new THREE.Mesh(
      new THREE.PlaneGeometry(W, H),
      new THREE.MeshBasicMaterial({ visible: false })
    );
    this.pickPlane.rotation.x = -Math.PI / 2;
    this.pickPlane.position.y = 0.2;
    this.scene.add(this.pickPlane);
    this.raycaster = new THREE.Raycaster();

    this.cellGeo = new THREE.BoxGeometry(0.92, 0.18, 0.92);
    this.cellMats = {};
    for (const t of Object.values(TERRAIN)) {
      this.cellMats[t] = new THREE.MeshLambertMaterial({
        color: TERRAIN_INFO[t].color,
        emissive: t === TERRAIN.ICE ? 0x0d3346
          : t === TERRAIN.FIRE ? 0x3a1505
          : t === TERRAIN.FORBIDDEN ? 0x1a0208 : 0x000000,
      });
    }
    this.cellMeshes = [];
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const m = new THREE.Mesh(this.cellGeo, this.cellMats[TERRAIN.EMPTY]);
        const { wx, wz } = cellToWorld(x, y);
        m.position.set(wx, 0.09, wz);
        this.scene.add(m);
        this.cellMeshes.push(m);
      }
    }

    this.effectMeshes = new Map();
    this.markerGroup = new THREE.Group();
    this.scene.add(this.markerGroup);
    this.coinMeshes = new Map();

    this.ghostGroup = new THREE.Group();
    this.scene.add(this.ghostGroup);
    this.ghostMats = {};
    for (const t of [TERRAIN.LAND, TERRAIN.ICE, TERRAIN.FIRE]) {
      this.ghostMats[t] = new THREE.MeshBasicMaterial({
        color: TERRAIN_INFO[t].color, transparent: true, opacity: 0.55, depthWrite: false,
      });
    }
    this.ghostInvalidMat = new THREE.MeshBasicMaterial({ color: 0xff3b4e, transparent: true, opacity: 0.45, depthWrite: false });

    this.hero = this.buildHero();
    this.hero.visible = false;
    this.scene.add(this.hero);

    this.frameCbs = new Set();
    this.clock = new THREE.Clock();

    window.addEventListener('resize', () => this.resize());
    this.resize();
    this.renderer.setAnimationLoop(() => this.tick());
  }

  resize() {
    const w = this.container.clientWidth, h = this.container.clientHeight;
    this.renderer.setSize(w, h);
    const aspect = w / h;
    const halfH = 12.6;
    this.camera.top = halfH;
    this.camera.bottom = -halfH;
    this.camera.left = -halfH * aspect;
    this.camera.right = halfH * aspect;
    this.camera.updateProjectionMatrix();
  }

  tick() {
    const dt = this.clock.getDelta();
    const t = this.clock.elapsedTime;
    for (const cb of this.frameCbs) cb(dt, t);
    this.renderer.render(this.scene, this.camera);
  }

  addFrameCallback(cb) { this.frameCbs.add(cb); return () => this.frameCbs.delete(cb); }

  pick(ndcX, ndcY) {
    this.raycaster.setFromCamera({ x: ndcX, y: ndcY }, this.camera);
    const hit = this.raycaster.intersectObject(this.pickPlane)[0];
    if (!hit) return null;
    const { x, y } = worldToCell(hit.point.x, hit.point.z);
    if (x < 0 || x >= W || y < 0 || y >= H) return null;
    return { x, y };
  }

  setHeroCell(x, y) {
    const { wx, wz } = cellToWorld(x, y);
    this.hero.position.set(wx, 0.2, wz);
  }

  setHeroElement(el) {
    const c = HERO_COLORS[el];
    this.hero.userData.body.material.color.setHex(c.body);
    this.hero.userData.body.material.emissive.setHex(el === 'ice' ? 0x123a4a : 0x4a2410);
    this.hero.userData.ring.material.color.setHex(c.ring);
  }

  buildHero() {
    const g = new THREE.Group();
    const body = new THREE.Mesh(
      new THREE.CapsuleGeometry(0.2, 0.32, 4, 12),
      new THREE.MeshLambertMaterial({ color: 0xbfeaff, emissive: 0x123a4a })
    );
    body.position.y = 0.35;
    g.add(body);
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(0.3, 0.035, 8, 32),
      new THREE.MeshBasicMaterial({ color: 0x62c8f2 })
    );
    ring.rotation.x = Math.PI / 2;
    ring.position.y = 0.08;
    g.add(ring);
    g.userData.body = body;
    g.userData.ring = ring;
    return g;
  }

  refreshCells(board) {
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = board.idx(x, y);
        const m = this.cellMeshes[i];
        const t = board.terrain[i];
        m.material = this.cellMats[t];
        m.scale.y = t === TERRAIN.EMPTY ? 0.35 : t === TERRAIN.FORBIDDEN ? 2.2 : 1;
        m.position.y = t === TERRAIN.EMPTY ? 0.03 : t === TERRAIN.FORBIDDEN ? 0.2 : 0.09;

        const eff = board.effect[i];
        const existing = this.effectMeshes.get(i);
        if (eff === EFFECT.NONE && existing) {
          this.scene.remove(existing);
          this.effectMeshes.delete(i);
        } else if (eff !== EFFECT.NONE) {
          if (existing) { this.scene.remove(existing); this.effectMeshes.delete(i); }
          const tex = makeIconTexture(EFFECT_INFO[eff].icon, EFFECT_STYLE[eff]);
          const icon = new THREE.Mesh(
            new THREE.PlaneGeometry(0.72, 0.72),
            new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false })
          );
          icon.rotation.x = -Math.PI / 2;
          const { wx, wz } = cellToWorld(x, y);
          icon.position.set(wx, 0.2, wz);
          this.scene.add(icon);
          this.effectMeshes.set(i, icon);
        }
      }
    }
  }

  showGhost(piece, ox, oy, valid) {
    this.ghostGroup.clear();
    if (!piece) return;
    for (const c of piece.cells) {
      const box = new THREE.Mesh(this.cellGeo, valid ? this.ghostMats[c.terrain] : this.ghostInvalidMat);
      const { wx, wz } = cellToWorld(ox + c.x, oy + c.y);
      box.position.set(wx, 0.22, wz);
      this.ghostGroup.add(box);
    }
  }

  hideGhost() { this.ghostGroup.clear(); }

  // 每关重建标记物：起点 / 终点 / 金币（转化格由特效图标层负责）
  buildMarkers(board) {
    this.scene.remove(this.markerGroup);
    this.markerGroup = new THREE.Group();
    this.scene.add(this.markerGroup);
    this.coinMeshes.clear();
    if (this.markerAnimOff) this.markerAnimOff();

    const put = (obj, x, y, dy = 0.2) => {
      const { wx, wz } = cellToWorld(x, y);
      obj.position.set(wx, dy, wz);
      this.markerGroup.add(obj);
    };

    const start = new THREE.Mesh(
      new THREE.CylinderGeometry(0.28, 0.34, 0.9, 20),
      new THREE.MeshLambertMaterial({ color: 0x35e88c, emissive: 0x0c3a20 })
    );
    put(start, board.start.x, board.start.y, 0.6);

    const endG = new THREE.Group();
    const pole = new THREE.Mesh(
      new THREE.CylinderGeometry(0.1, 0.16, 1.5, 12),
      new THREE.MeshLambertMaterial({ color: 0xffd54a, emissive: 0x3a2a05 })
    );
    pole.position.y = 0.55;
    endG.add(pole);
    const flag = new THREE.Mesh(
      new THREE.BoxGeometry(0.5, 0.3, 0.04),
      new THREE.MeshLambertMaterial({ color: 0xffb300, emissive: 0x3a2400 })
    );
    flag.position.set(0.28, 1.2, 0);
    endG.add(flag);
    put(endG, board.end.x, board.end.y);

    for (const ci of board.coins) {
      const coin = new THREE.Mesh(
        new THREE.TorusGeometry(0.24, 0.08, 10, 24),
        new THREE.MeshLambertMaterial({ color: 0xffd84a, emissive: 0x3a2a00 })
      );
      put(coin, ci % W, Math.floor(ci / W), 0.45);
      this.coinMeshes.set(ci, coin);
    }

    this.markerAnimOff = this.addFrameCallback((dt) => {
      for (const c of this.coinMeshes.values()) c.rotation.y += dt * 2.5;
    });
  }

  removeCoin(idx) {
    const c = this.coinMeshes.get(idx);
    if (!c) return;
    this.markerGroup.remove(c);
    this.coinMeshes.delete(idx);
  }

  flashAt(x, y, color = 0xffffff) {
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(0.3, 0.05, 8, 32),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 })
    );
    ring.rotation.x = Math.PI / 2;
    const { wx, wz } = cellToWorld(x, y);
    ring.position.set(wx, 0.25, wz);
    this.scene.add(ring);
    const t0 = performance.now();
    const off = this.addFrameCallback(() => {
      const k = (performance.now() - t0) / 450;
      ring.scale.setScalar(1 + k * 2.2);
      ring.material.opacity = Math.max(0, 0.9 * (1 - k));
      if (k >= 1) { this.scene.remove(ring); ring.geometry.dispose(); off(); }
    });
  }
}