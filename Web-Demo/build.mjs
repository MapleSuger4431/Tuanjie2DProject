// 打包器：把模块化源码 + 本地 three.js 合成单个可双击运行的 edge-grid.html
// 用法：node build.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = dirname(fileURLToPath(import.meta.url));
const read = (p) => readFileSync(join(root, p), 'utf8');

// 1) three.module.js：移除末尾 export 块，改建成 THREE 命名空间
let three = read('vendor/three.module.js');
const m = three.match(/export\s*\{([\s\S]*?)\};?\s*$/);
if (!m) throw new Error('three.module.js 里没找到 export 块');
const names = m[1].split(',').map(s => s.trim()).filter(Boolean).map(s => {
  const as = s.match(/^(\S+)\s+as\s+(\S+)$/);
  return as ? `${as[2]}: ${as[1]}` : s;
});
three = three.slice(0, m.index) + `\nconst THREE = { ${names.join(',\n  ')} };\n`;

// 2) 游戏模块：去 import/export 后按依赖序拼接
const strip = (src) => src
  .split('\n')
  .filter(l => !/^\s*import\s/.test(l))
  .join('\n')
  .replace(/^export\s+(const|class|function|let|var)/gm, '$1')
  .replace(/^export\s*\{[^}]*\};?\s*$/gm, '');

const modules = ['js/model.js', 'js/pieces.js', 'js/walker.js', 'js/render.js', 'js/main.js'];
let gameCode = modules.map(f => `\n// ===== ${f} =====\n` + strip(read(f))).join('\n');

// 2.5) 与 three.js 顶层标识符冲突的游戏代码标识符统一加前缀
const declRe = /^(?:const|let|var|function|class)\s+([A-Za-z_$][\w$]*)/gm;
const threeNames = new Set();
let mm;
while ((mm = declRe.exec(three))) threeNames.add(mm[1]);
const gameNames = new Set();
while ((mm = declRe.exec(gameCode))) gameNames.add(mm[1]);
const collisions = [...gameNames].filter(n => threeNames.has(n) && n !== 'THREE');
for (const c of collisions) {
  gameCode = gameCode.replace(new RegExp(`\\b${c}\\b`, 'g'), 'G_' + c);
}
if (collisions.length) console.log('renamed:', collisions.map(c => c + '→G_' + c).join(', '));

// 3) 组装 HTML：内联 CSS + 内联脚本（模块作用域共享 THREE）
let html = read('index.html');
const css = read('css/style.css');
html = html.replace(/<link rel="stylesheet"[^>]*>/, () => '<style>\n' + css + '\n</style>');
html = html.replace(/<script type="importmap">[\s\S]*?<\/script>/, '');
html = html.replace(
  /<script type="module" src="\.\/js\/main\.js"><\/script>/,
  () => '<script type="module">\n' + three + '\n' + gameCode + '\n</script>'
);

writeFileSync(join(root, 'edge-grid.html'), html);
console.log('OK edge-grid.html', (html.length / 1024 / 1024).toFixed(2) + ' MB');
