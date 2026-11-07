#!/usr/bin/env node
// 像素对照：vue-tsx-pro 文档站示例效果区的截图（tools/ts-shots）与一个平台 `inspect snapshot` 的截图逐像素比（docs/DOCSITE.md 第一节）。
// 两边按同样的条件出图：宽 756（文档站示例区）、四周留白 21（示例区的 1.5em 内边距）、同一档缩放、同一主题、动画停住、布局不按像素取整（--exact）。
// 文字的栅格化各平台不同：上游文字的盒子（外扩 2 像素）盖掉不比，文字的位置与大小由取值对照（styles.mjs）管；
// 其余像素任一通道差超过 24、且在对方图里周围 1 个 CSS 像素内也找不到相同颜色时算不同（通道阈值同上游 scripts/docs/shots-compare.mjs；
// 位移容差同 pixelmatch 的抗锯齿处理：上游按小数像素定位、平台按整像素对齐，边缘差半个到一个像素，那是取值对照管的）。差异像素占未盖掉部分的比例超过允许值时报出，退出码为 1；
// 允许值与原因登记在 registry.mjs 的 PIXEL_ALLOWANCE。报告：artifacts/parity/pixels/<平台>/<页面>.html（上游 | 我们 | 差异）。
//
//   node tools/parity/pixels.mjs <页面> [示例,…] [--platform avalonia|wpf|winforms] [--themes light,dark] [--scale 1]
//   node tools/parity/pixels.mjs all [--platform …]     截过的全部页面（artifacts/ts-shots 下的），另出一张每页一行的总表
//
// 先跑 ts-shots 截文档站这一页；inspect 要先 Release 构建。WinForms 按显示器的 DPI 出图，缩放取它的。
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, relative, resolve } from 'node:path';
import { parseArgs } from 'node:util';
import { componentOf, inspector } from '../lib/inspect.mjs';
import { table } from '../lib/markdown.mjs';
import { repoRoot } from '../lib/vue.mjs';
import { DEFAULT_PIXEL_ALLOWANCE, PIXEL_ALLOWANCE } from './registry.mjs';

const { values: options, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    platform: { type: 'string', default: 'avalonia' },
    themes: { type: 'string', default: 'light,dark' },
    scale: { type: 'string', default: '1' },
    vue: { type: 'string', default: join(process.env.USERPROFILE ?? process.env.HOME ?? '', '.rune/vtp-docs') },
  },
});
const [pageName, only] = positionals;
if (!pageName) {
  process.stderr.write('用法：node tools/parity/pixels.mjs <页面|all> [示例,…] [--platform avalonia|wpf|winforms] [--themes light,dark] [--scale 1]\n');
  process.exit(2);
}

/** 文档站示例区的宽度与内边距（CSS 像素）：两边同样大小出图 */
const CANVAS = { width: 756, padding: 21 };
/** 每个通道差超过这个值才算不同（抗锯齿的细微差别不算） */
const THRESHOLD = 24;
/** 文字盒子往外扩几个 CSS 像素再盖掉：盖住抗锯齿的边与两边几个像素的位置差 */
const TEXT_MARGIN = 2;
/** 位移容差（CSS 像素）：对方图里这么近的范围内有相同颜色就不算不同 */
const SHIFT = 1;

const { PNG } = createRequire(join(resolve(options.vue), 'package.json'))('pngjs');
const inspect = inspector(options.platform);
const reports = join(repoRoot, 'artifacts/parity/pixels', options.platform);
const percent = (ratio) => `${(ratio * 100).toFixed(2)}%`;

if (pageName === 'all') {
  const pages = readdirSync(join(repoRoot, 'artifacts/ts-shots'));
  const summary = pages.map((page) => {
    const results = comparePage(page);
    const compared = results.filter((r) => !r.missing);
    const worst = compared.reduce((max, r) => Math.max(max, r.ratio), 0);
    return [page, compared.length, results.length - compared.length, compared.filter((r) => r.over).length, percent(worst)];
  });
  process.stdout.write(`# ${options.platform} 对 vue-tsx-pro 的像素：总表\n\n${table(['页面', '比了', '没有示例', '超过允许', '最大差异'], summary)}\n`);
  process.exit(summary.some((row) => row[3] > 0) ? 1 : 0);
}
process.exit(comparePage(pageName, only).some((r) => r.over) ? 1 : 0);

/** 比一页：每个示例、每个主题出图、比对、写进这一页的 HTML 报告，返回结果 */
function comparePage(pageName, only) {
  const shots = join(repoRoot, 'artifacts/ts-shots', pageName);
  if (!existsSync(shots)) throw new Error(`没有 ${shots}：先跑 node tools/ts-shots/ts-shots.mjs ${pageName}`);
  const component = componentOf(pageName);
  const out = join(reports, pageName);
  mkdirSync(out, { recursive: true });

  // 示例 id 可以带连字符（in-context），按第一个主题的文件名取
  const suffix = `-${options.themes.split(',')[0]}.json`;
  const ids = readdirSync(shots).filter((name) => name.endsWith(suffix)).map((name) => name.slice(0, -suffix.length))
    .filter((id) => !only || only.split(',').includes(id));
  const results = [];
  for (const id of ids) {
    for (const theme of options.themes.split(',')) {
      const boxes = JSON.parse(readFileSync(join(shots, `${id}-${theme}.json`), 'utf8'));
      const ours = join(out, `${id}-${theme}.png`);
      try {
        const args = ['snapshot', `${component}/${id}`, '--theme', theme, '--width', String(CANVAS.width), '--padding', String(CANVAS.padding), '--exact', '--out', ours];
        inspect(options.platform === 'winforms' ? args : [...args, '--scale', options.scale]);
      } catch {
        results.push({ id, theme, missing: true });
        continue;
      }
      const mine = PNG.sync.read(readFileSync(ours));
      // WinForms 按显示器的 DPI 出图：缩放按它的宽度算
      const scale = Math.round((mine.width / CANVAS.width) * 4) / 4;
      const theirsPath = join(shots, `${id}-${theme}@${scale}x.png`);
      if (!existsSync(theirsPath)) {
        results.push({ id, theme, missing: true, note: `文档站没有 ${scale}x 的截图（ts-shots --scales）` });
        continue;
      }
      const theirs = PNG.sync.read(readFileSync(theirsPath));
      results.push({ id, theme, scale, theirsPath, ours, ...compare(theirs, mine, boxes.texts, scale, join(out, `${id}-${theme}.diff.png`)) });
    }
  }

  const allowance = (id) => PIXEL_ALLOWANCE[options.platform]?.[pageName]?.[id] ?? PIXEL_ALLOWANCE.all?.[pageName]?.[id] ?? { ratio: DEFAULT_PIXEL_ALLOWANCE };
  const rows = results.map((r) => {
    if (r.missing) return [r.id, r.theme, '—', '—', r.note ?? `${options.platform} 没有示例 ${component}/${r.id}`];
    const allowed = allowance(r.id);
    const sizes = r.size[0] === r.size[2] && r.size[1] === r.size[3] ? `${r.size[0]}×${r.size[1]}` : `上游 ${r.size[0]}×${r.size[1]}，我们 ${r.size[2]}×${r.size[3]}`;
    r.over = r.ratio > allowed.ratio;
    return [r.id, r.theme, sizes, percent(r.ratio), r.over ? `**超过 ${percent(allowed.ratio)}**` : allowed.reason ?? '在允许范围内'];
  });
  process.stdout.write(`# ${pageName}：${options.platform} 对 vue-tsx-pro 的像素\n\n${table(['示例', '主题', '尺寸', '不同的像素', '结论'], rows)}\n`);

  // HTML 报告：每行 上游 | 我们 | 差异
  const report = join(reports, `${pageName}.html`);
  const link = (path) => relative(join(report, '..'), path).replaceAll('\\', '/');
  writeFileSync(report, `<!doctype html><meta charset="utf-8"><title>${pageName} · ${options.platform} 像素对照</title>
  <style>body{font:14px system-ui,sans-serif;margin:24px;background:#f9f9fb}h2{font-size:15px;margin:24px 0 8px}.row{display:flex;gap:12px;align-items:flex-start}
  figure{margin:0}figcaption{color:#60646c;font-size:12px;margin-bottom:4px}img{border:1px solid #d9d9e0;max-width:100%}.over{color:#ce2c31}</style>
  <h1>${pageName}：${options.platform} 对 vue-tsx-pro 的像素</h1>
  ${results.filter((r) => !r.missing).map((r) => `<h2 class="${r.over ? 'over' : ''}">${r.id} · ${r.theme} · ${percent(r.ratio)}</h2>
  <div class="row"><figure><figcaption>vue-tsx-pro</figcaption><img src="${link(r.theirsPath)}"></figure><figure><figcaption>${options.platform}</figcaption><img src="${link(r.ours)}"></figure><figure><figcaption>差异（红：不同；蓝：文字盖掉）</figcaption><img src="${link(r.diffPath)}"></figure></div>`).join('\n')}
  `);
  process.stdout.write(`报告：${report}\n\n`);
  return results;
}

/** 逐像素比：文字盒子盖掉；尺寸不同时多出来的部分全算不同。差异图：上游变淡做底，不同的标红，盖掉的标浅蓝 */
function compare(a, b, texts, scale, diffPath) {
  const width = Math.max(a.width, b.width);
  const height = Math.max(a.height, b.height);
  const mask = new Uint8Array(width * height);
  for (const [x, y, w, h] of texts) {
    const [left, top] = [Math.floor((x - TEXT_MARGIN) * scale), Math.floor((y - TEXT_MARGIN) * scale)];
    const [right, bottom] = [Math.ceil((x + w + TEXT_MARGIN) * scale), Math.ceil((y + h + TEXT_MARGIN) * scale)];
    for (let row = Math.max(0, top); row < Math.min(height, bottom); row++) mask.fill(1, row * width + Math.max(0, left), row * width + Math.min(width, right));
  }
  const reach = Math.max(1, Math.round(SHIFT * scale));
  const diff = new PNG({ width, height });
  let compared = 0;
  let different = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const at = y * width + x;
      const pa = pixel(a, x, y);
      const pb = pixel(b, x, y);
      const base = pa ?? pb ?? [255, 255, 255, 255];
      const gray = Math.round(255 - (255 - (base[0] + base[1] + base[2]) / 3) * 0.3);
      let color = [gray, gray, gray];
      if (mask[at]) color = [gray * 0.8, gray * 0.9, 255];
      else {
        compared++;
        if (!pa || !pb || (!near(b, x, y, pa, reach) || !near(a, x, y, pb, reach))) {
          different++;
          color = [230, 30, 40];
        }
      }
      diff.data.set([...color, 255], at * 4);
    }
  }
  writeFileSync(diffPath, PNG.sync.write(diff));
  return { size: [a.width, a.height, b.width, b.height], ratio: compared ? different / compared : 0, different, diffPath };
}

/** 图里 (x, y) 周围 reach 个像素内有没有和 color 相同（每个通道差不超过阈值）的像素 */
function near(png, x, y, color, reach) {
  for (let dy = -reach; dy <= reach; dy++) {
    for (let dx = -reach; dx <= reach; dx++) {
      const other = pixel(png, x + dx, y + dy);
      if (other && Math.max(Math.abs(other[0] - color[0]), Math.abs(other[1] - color[1]), Math.abs(other[2] - color[2]), Math.abs(other[3] - color[3])) <= THRESHOLD) return true;
    }
  }
  return false;
}

function pixel(png, x, y) {
  if (x < 0 || y < 0) return null;
  if (x >= png.width || y >= png.height) return null;
  const at = (y * png.width + x) * 4;
  return [png.data[at], png.data[at + 1], png.data[at + 2], png.data[at + 3]];
}

