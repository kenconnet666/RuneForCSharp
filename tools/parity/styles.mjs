#!/usr/bin/env node
// 取值对照：vue-tsx-pro 文档站的计算样式（tools/ts-styles 写的 artifacts/ts-styles/<页面>.json）与一个平台的
// `inspect styles <组件>/<示例> --json` 逐个部件、逐个属性比对。部件按 scope/part#序号 配对，示例名两边相同。
// 文字的宽高随字体变，不比；控件根的高度、内边距、描边、圆角、颜色、字号、字重要一致（长度容差 0.5px）。
//
//   node tools/parity/styles.mjs <页面> [示例,…] [--platform avalonia|wpf|winforms] [--json]
//
// 先跑 ts-styles 取文档站这一页；三个平台的 inspect 要先 Release 构建。有差异时退出码为 1。
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { componentOf, inspector } from '../lib/inspect.mjs';
import { table } from '../lib/markdown.mjs';
import { repoRoot } from '../lib/vue.mjs';

const { values: options, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    platform: { type: 'string', default: 'avalonia' },
    json: { type: 'boolean', default: false },
  },
});
const [pageName, only] = positionals;
if (!pageName) {
  process.stderr.write('用法：node tools/parity/styles.mjs <页面> [示例,…] [--platform avalonia|wpf|winforms] [--json]\n');
  process.exit(2);
}

/** 比较的属性：文字决定的宽高不比（字体不同），其余都比 */
const COMPARED = [
  'MinHeight', 'PaddingTop', 'PaddingRight', 'PaddingBottom', 'PaddingLeft',
  'BorderTop', 'BorderRight', 'BorderBottom', 'BorderLeft', 'BorderColor',
  'RadiusTopLeft', 'RadiusTopRight', 'RadiusBottomRight', 'RadiusBottomLeft',
  'Background', 'Foreground', 'FontSize', 'FontWeight', 'LineHeight', 'LetterSpacing', 'Opacity',
];
/** 部件的宽高不取决于文字时才比：控件根的高度、图标的边长、勾选类的方框与轨道、滑块 */
const SIZED = (key) => /\/(root|icon|iconEnd|spinner|dot|input|indicator)#|^@svg#/.test(key);
const TOLERANCE = 0.5;

const capture = join(repoRoot, 'artifacts/ts-styles', `${pageName}.json`);
if (!existsSync(capture)) throw new Error(`没有 ${capture}：先跑 node tools/ts-styles/ts-styles.mjs ${pageName}`);
const upstream = JSON.parse(readFileSync(capture, 'utf8'));
const inspect = inspector(options.platform);
const component = componentOf(pageName);

const results = [];
for (const [id, example] of Object.entries(upstream.examples)) {
  if (only && !only.split(',').includes(id)) continue;
  for (const [theme, theirs] of Object.entries(example.themes)) {
    let ours;
    try {
      ours = JSON.parse(inspect(['styles', `${component}/${id}`, '--theme', theme, '--width', String(upstream.width ?? 800), '--json'])).parts;
    } catch {
      results.push({ example: id, theme, missing: true, diffs: [], only: { theirs: [], ours: [] } });
      continue;
    }
    results.push({ example: id, theme, ...compare(theirs, ours) });
  }
}

function compare(theirs, ours) {
  const byKey = new Map(ours.map((part) => [part.key, part]));
  const diffs = [];
  for (const part of theirs) {
    const mine = byKey.get(part.key);
    if (!mine) continue;
    // 图标只看大小与颜色：svg 上的字号、字重不影响画出来的样子
    const props = part.key.startsWith('@svg#') ? ['Width', 'Height', 'Foreground', 'Opacity']
      // 原生 input（勾选类的方框、轨道）里没有文字，前景色画不出来
      : [...COMPARED, ...(SIZED(part.key) ? ['Height'] : [])].filter((prop) => !(part.tag === 'input' && prop === 'Foreground'));
    for (const prop of props) {
      const [a, b] = [part.props[prop], mine.props[prop]];
      if (a === undefined || b === undefined || same(prop, a, b, part.props, mine.props)) continue;
      diffs.push({ key: part.key, text: part.text, prop, theirs: a, ours: b });
    }
  }
  const keys = new Set(ours.map((part) => part.key));
  return {
    diffs,
    only: { theirs: theirs.map((p) => p.key).filter((k) => !keys.has(k)), ours: ours.map((p) => p.key).filter((k) => !theirs.some((p) => p.key === k)) },
  };
}

function same(prop, a, b, props, mine) {
  // 看不出来的描边（透明，或与底色相同）：CSS 的底色铺到描边下面，画出来就是同样大的一块底色，平台上不画描边是一样的
  if (prop.startsWith('Border') && invisibleBorder(props) && !mine.BorderTop && !mine.BorderRight && !mine.BorderBottom && !mine.BorderLeft) return true;
  // 行内元素没有高度（auto）：不比
  if (prop === 'Height' && a === 'auto') return true;
  // CSS 的 min-width / min-height 没写时是 auto、letter-spacing 是 normal，平台上是 0
  if (a === 'auto' || (prop === 'LetterSpacing' && a === 'normal')) a = 0;
  // 自绘的组件在测量里保证最小尺寸，没有 MinHeight 属性：实际高度够了就算一样
  if (prop === 'MinHeight' && b === 0 && typeof a === 'number' && mine.Height >= a - TOLERANCE) return true;
  // 胶囊形：CSS 写 9999px；平台按 em 系数算出更大的数，或已经夹到高度的一半，画出来一样
  if (prop.startsWith('Radius') && a >= 999 && (b >= 999 || b >= mine.Height / 2 - TOLERANCE)) return true;
  // TS 用 visibility: hidden 藏起的部件，平台上用透明度 0
  if (prop === 'Opacity' && props.Visibility === 'hidden' && b === 0) return true;
  if (typeof a === 'number' && typeof b === 'number') return Math.abs(a - b) <= TOLERANCE;
  // 没有描边时描边色不用比；完全透明的颜色都算一样
  if (prop === 'BorderColor' && !props.BorderTop) return true;
  if (typeof a === 'string' && typeof b === 'string' && a.startsWith('#00') && b.startsWith('#00')) return true;
  return String(a).toUpperCase() === String(b).toUpperCase();
}

function invisibleBorder(props) {
  const color = String(props.BorderColor ?? '').toUpperCase();
  return color.startsWith('#00') || color === String(props.Background ?? '').toUpperCase();
}

if (options.json) process.stdout.write(`${JSON.stringify(results, null, 2)}\n`);
else {
  const lines = [`# ${pageName}：${options.platform} 对 vue-tsx-pro 的取值`, ''];
  for (const r of results) {
    lines.push(`## ${r.example}（${r.theme}）`, '');
    if (r.missing) {
      lines.push(`${options.platform} 没有示例 ${component}/${r.example}`, '');
      continue;
    }
    lines.push(r.diffs.length ? table(['部件', '文字', '属性', 'vue-tsx-pro', options.platform], r.diffs.map((d) => [d.key, d.text, d.prop, d.theirs, d.ours])) : '取值一致。', '');
    if (r.only.theirs.length) lines.push(`只有 vue-tsx-pro 有的部件：${r.only.theirs.join('、')}`, '');
  }
  const total = results.reduce((n, r) => n + r.diffs.length, 0);
  const missing = results.filter((r) => r.missing).map((r) => r.example);
  lines.push(`共 ${total} 处差异${missing.length ? `；没有的示例：${[...new Set(missing)].join('、')}` : ''}。`, '');
  process.stdout.write(lines.join('\n'));
}
process.exitCode = results.some((r) => r.diffs.length) ? 1 : 0;
