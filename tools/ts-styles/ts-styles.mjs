#!/usr/bin/env node
// 取 vue-tsx-pro 文档站上每个示例里、每个部件计算后的样式：parity 拿它与三个平台 `inspect props` 的结果逐项比对（ROADMAP"信息与验证工具"）。
// 部件按（示例, data-scope/data-part, 第几个）编号；亮、暗主题各一遍，可交互的部件另外强制悬停、键盘聚焦、按下各记一次会变的属性。
// 颜色统一成 #AARRGGBB，长度统一成像素数。Playwright 用 vue-tsx-pro 的 apps/host-demo 装好的那份（本仓库不装 node 包）。
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';
import { parseArgs } from 'node:util';
import { table } from '../lib/markdown.mjs';
import { repoRoot } from '../lib/vue.mjs';

const USAGE = `用法：node tools/ts-styles/ts-styles.mjs <页面> [示例,…] [选项]

  <页面>          文档站的组件页：button、icon-button（/components/<页面>）
  [示例,…]        只取这几个示例（小节标题的 id，同 Gallery 示例名）；不写时取整页

选项：
  --origin <地址>  文档站（默认 http://localhost:5274：在 vue-tsx-pro 的副本里 apps/docs 下 vite --port 5274，见 HANDOFF 命令表）
  --themes <列表>  主题（默认 light,dark；其余内置主题按名字，如 paper）
  --states        另外强制悬停、键盘聚焦、按下，记下会变的属性
  --width <像素>   视口宽度（默认 1280）
  --json          输出 JSON（默认 Markdown 摘要）；不论哪种都写 artifacts/ts-styles/<页面>.json
  --vue <路径>     vue-tsx-pro 仓库（取它装好的 Playwright；默认 VUE_TSX_PRO 或同级的 IdeaProjects/vue-tsx-pro）
`;

const { values: options, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    origin: { type: 'string', default: 'http://localhost:5274' },
    themes: { type: 'string', default: 'light,dark' },
    states: { type: 'boolean', default: false },
    width: { type: 'string', default: '1280' },
    json: { type: 'boolean', default: false },
    vue: { type: 'string' },
    help: { type: 'boolean', short: 'h', default: false },
  },
});
const [pageName, only] = positionals;
if (options.help || !pageName) {
  process.stdout.write(USAGE);
  process.exit(options.help ? 0 : 1);
}

/** 取的属性：CSS 名 → 结果里的名字与种类（length 换成像素数，color 换成 #AARRGGBB，其余原样） */
const PROPS = {
  width: ['Width', 'length'],
  height: ['Height', 'length'],
  'min-width': ['MinWidth', 'length'],
  'min-height': ['MinHeight', 'length'],
  'padding-top': ['PaddingTop', 'length'],
  'padding-right': ['PaddingRight', 'length'],
  'padding-bottom': ['PaddingBottom', 'length'],
  'padding-left': ['PaddingLeft', 'length'],
  'border-top-width': ['BorderTop', 'length'],
  'border-right-width': ['BorderRight', 'length'],
  'border-bottom-width': ['BorderBottom', 'length'],
  'border-left-width': ['BorderLeft', 'length'],
  'border-top-color': ['BorderColor', 'color'],
  'border-top-left-radius': ['RadiusTopLeft', 'length'],
  'border-top-right-radius': ['RadiusTopRight', 'length'],
  'border-bottom-right-radius': ['RadiusBottomRight', 'length'],
  'border-bottom-left-radius': ['RadiusBottomLeft', 'length'],
  'background-color': ['Background', 'color'],
  color: ['Foreground', 'color'],
  fill: ['Fill', 'color'],
  stroke: ['Stroke', 'color'],
  'stroke-width': ['StrokeWidth', 'length'],
  'font-size': ['FontSize', 'length'],
  'font-weight': ['FontWeight', 'number'],
  'line-height': ['LineHeight', 'length'],
  'letter-spacing': ['LetterSpacing', 'length'],
  'column-gap': ['ColumnGap', 'length'],
  'row-gap': ['RowGap', 'length'],
  opacity: ['Opacity', 'number'],
  'box-shadow': ['BoxShadow', 'raw'],
  'outline-style': ['OutlineStyle', 'raw'],
  'outline-width': ['OutlineWidth', 'length'],
  'outline-color': ['OutlineColor', 'color'],
  'outline-offset': ['OutlineOffset', 'length'],
  'text-decoration-line': ['TextDecoration', 'raw'],
  cursor: ['Cursor', 'raw'],
  display: ['Display', 'raw'],
  visibility: ['Visibility', 'raw'],
};
/** 状态下比较的属性（会随悬停、聚焦、按下变的） */
const STATE_PROPS = ['color', 'background-color', 'border-top-color', 'box-shadow', 'outline-style', 'outline-width', 'outline-color', 'opacity', 'text-decoration-line'];
const STATES = ['hover', 'focus-visible', 'active'];
/** 没有部件标记、按标签配对的元素（Text 家族、标题、行内代码、图标） */
const UNMARKED = 'svg, p, span, strong, em, small, h1, h2, h3, h4, h5, h6, code, kbd, mark';
const INTERACTIVE = 'a[href], button, input, select, textarea, summary, [tabindex], [role=button], [role=option], [role=tab], [role=menuitem], [role=switch], [role=checkbox], [role=radio]';

const vue = resolve(options.vue ?? process.env.VUE_TSX_PRO ?? join(repoRoot, '../../IdeaProjects/vue-tsx-pro'));
const { chromium } = createRequire(join(vue, 'apps/host-demo/package.json'))('playwright');
const browser = await chromium.launch();
try {
  const result = { page: pageName, origin: options.origin, width: Number(options.width), examples: {} };
  for (const theme of options.themes.split(',')) {
    const context = await browser.newContext({ viewport: { width: Number(options.width), height: 900 }, reducedMotion: 'reduce', locale: 'zh-CN' });
    // light、dark 跟随系统；其余主题写进文档站记主题偏好的 localStorage（vue-tsx-pro 的 DEFAULT_THEME_STORAGE_KEY）
    if (theme !== 'light' && theme !== 'dark') await context.addInitScript((name) => localStorage.setItem('vue-tsx-pro-theme', name), theme);
    const page = await context.newPage();
    await page.emulateMedia({ colorScheme: theme === 'dark' ? 'dark' : 'light', reducedMotion: 'reduce' });
    // 带锚点打开：文档站把整页示例立即渲染（不等滚动）
    await page.goto(`${options.origin}/components/${pageName}#top`, { waitUntil: 'networkidle' });
    await page.waitForSelector('[data-demo]');
    await page.evaluate(defineColor);
    const examples = await page.evaluate(collect, { PROPS, only: only?.split(',') ?? null, UNMARKED });
    if (!Object.keys(examples).length) throw new Error(`${pageName} 页上没有找到示例${only ? `：${only}` : ''}`);
    if (options.states) await addStates(page, examples);
    for (const [id, example] of Object.entries(examples)) {
      result.examples[id] ??= { title: example.title, themes: {} };
      result.examples[id].themes[theme] = example.parts;
    }
    await context.close();
  }
  const dir = join(repoRoot, 'artifacts/ts-styles');
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, `${pageName}.json`), `${JSON.stringify(result, null, 2)}\n`);
  process.stdout.write(options.json ? `${JSON.stringify(result, null, 2)}\n` : summary(result));
} finally {
  await browser.close();
}

/** 在页面里定义颜色换算：任何 CSS 颜色（rgb、oklab、color()）画一个像素再读回来，得到 #AARRGGBB */
function defineColor() {
  const probe = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
  window.tsStylesColor = (value) => {
    probe.clearRect(0, 0, 1, 1);
    probe.fillStyle = '#000';
    probe.fillStyle = value;
    probe.fillRect(0, 0, 1, 1);
    const [r, g, b, a] = probe.getImageData(0, 0, 1, 1).data;
    return `#${[a, r, g, b].map((n) => n.toString(16).padStart(2, '0')).join('').toUpperCase()}`;
  };
}

/** 在页面里跑：每个示例（h2[id] 与 [data-demo] 画布同在一个小节里）里带部件标记的元素，按（scope/part, 第几个）编号 */
function collect({ PROPS, only, UNMARKED }) {
  const length = (value) => (value.endsWith('px') ? Math.round(parseFloat(value) * 100) / 100 : value);
  const convert = (kind, value) => (kind === 'length' ? length(value) : kind === 'color' ? window.tsStylesColor(value) : kind === 'number' ? Number(value) : value);
  const examples = {};
  for (const heading of document.querySelectorAll('h2[id]')) {
    if (only && !only.includes(heading.id)) continue;
    const canvas = heading.parentElement.querySelector('[data-demo]');
    if (!canvas) continue;
    const counts = {};
    const parts = [];
    // 没渲染的元素（自己或祖先 display: none，如收起的弹出层）不记：平台那边看不见的元素也不记
    for (const element of canvas.querySelectorAll('[data-scope][data-part]')) {
      if (!element.checkVisibility()) continue;
      const name = `${element.getAttribute('data-scope')}/${element.getAttribute('data-part')}`;
      const index = (counts[name] = (counts[name] ?? -1) + 1);
      element.setAttribute('data-ts-styles', `${heading.id}|${name}#${index}`);
      const style = getComputedStyle(element);
      const props = {};
      for (const [css, [prop, kind]] of Object.entries(PROPS)) props[prop] = convert(kind, style.getPropertyValue(css));
      const text = element.textContent.trim().replace(/\s+/g, ' ').slice(0, 24);
      parts.push({ key: `${name}#${index}`, tag: element.tagName.toLowerCase(), text, props });
    }
    // 没有部件标记的元素（用 css.create 写的组件：Text 家族、Icon）按标签与出现顺序编号：@svg#0、@p#1……
    // 文字元素只算直接含文字的；svg 只算最外层的
    const tagCounts = {};
    for (const element of canvas.querySelectorAll(UNMARKED)) {
      if (element.hasAttribute('data-part') || element.parentElement.closest('svg') || !element.checkVisibility()) continue;
      const tag = element.tagName.toLowerCase();
      if (tag !== 'svg' && ![...element.childNodes].some((node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim())) continue;
      const name = `@${tag}`;
      const index = (tagCounts[name] = (tagCounts[name] ?? -1) + 1);
      element.setAttribute('data-ts-styles', `${heading.id}|${name}#${index}`);
      const style = getComputedStyle(element);
      const props = {};
      for (const [css, [prop, kind]] of Object.entries(PROPS)) props[prop] = convert(kind, style.getPropertyValue(css));
      parts.push({ key: `${name}#${index}`, tag, text: element.textContent.trim().replace(/\s+/g, ' ').slice(0, 24), props });
    }
    examples[heading.id] = { title: heading.textContent.trim(), parts };
  }
  return examples;
}

/** 可交互的部件强制悬停、键盘聚焦、按下（CDP 的 CSS.forcePseudoState），记下它和里面的部件变了的属性 */
async function addStates(page, examples) {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('DOM.enable');
  await cdp.send('CSS.enable');
  const { root } = await cdp.send('DOM.getDocument', { depth: -1 });
  for (const [id, example] of Object.entries(examples)) {
    for (const part of example.parts) {
      const selector = `[data-ts-styles="${id}|${part.key}"]`;
      if (!(await page.$eval(selector, (element, query) => element.matches(query), INTERACTIVE))) continue;
      const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector });
      const read = () => page.$eval(selector, (element, props) => {
        const own = [element, ...element.querySelectorAll('[data-ts-styles]')];
        const value = (node, [css, kind]) => {
          const raw = getComputedStyle(node).getPropertyValue(css);
          return kind === 'color' ? window.tsStylesColor(raw) : raw;
        };
        return Object.fromEntries(own.map((node) => [node.getAttribute('data-ts-styles').split('|')[1], props.map((prop) => value(node, prop))]));
      }, STATE_PROPS.map((css) => [css, PROPS[css][1]]));
      const base = await read();
      part.states = {};
      for (const state of STATES) {
        await cdp.send('CSS.forcePseudoState', { nodeId, forcedPseudoClasses: [state] });
        const forced = await read();
        await cdp.send('CSS.forcePseudoState', { nodeId, forcedPseudoClasses: [] });
        const changed = {};
        for (const [key, values] of Object.entries(forced))
          values.forEach((value, i) => {
            if (value === base[key][i]) return;
            (changed[key] ??= {})[PROPS[STATE_PROPS[i]][0]] = value;
          });
        if (Object.keys(changed).length) part.states[state] = changed;
      }
    }
  }
}

/** Markdown 摘要：每个示例、每个部件的主要取值（第一套主题） */
function summary(result) {
  const lines = [`# ${result.page}（${result.origin}，宽 ${result.width}）`, '', `完整结果：artifacts/ts-styles/${result.page}.json`, ''];
  for (const [id, example] of Object.entries(result.examples)) {
    const [theme, parts] = Object.entries(example.themes)[0];
    lines.push(`## ${id}：${example.title}（${theme}）`, '');
    lines.push(table(
      ['部件', '文字', '宽 × 高', '内边距', '字号 / 字重', '前景', '底色', '描边', '圆角', '状态'],
      parts.map(({ key, text, props: p, states }) => [
        key,
        text,
        `${p.Width} × ${p.Height}`,
        `${p.PaddingTop} ${p.PaddingRight} ${p.PaddingBottom} ${p.PaddingLeft}`,
        `${p.FontSize} / ${p.FontWeight}`,
        p.Foreground,
        p.Background,
        p.BorderTop ? `${p.BorderTop} ${p.BorderColor}` : '',
        p.RadiusTopLeft,
        states ? Object.keys(states).join('、') : '',
      ]),
    ), '');
  }
  return lines.join('\n');
}
