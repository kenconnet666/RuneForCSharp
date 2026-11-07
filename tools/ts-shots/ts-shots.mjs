#!/usr/bin/env node
// 截 vue-tsx-pro 文档站上每个示例的效果区（[data-demo]，不含标题、说明、源码），给 tools/parity/pixels.mjs 与三个平台的截图逐像素比（docs/DOCSITE.md）。
// 每个示例、每个主题、每档缩放一张 PNG，另存一份效果区里文字的盒子（CSS 像素，相对效果区）：文字的栅格化各平台不同，比对时盖掉文字、只比盒子。
// 截图前停住：减少动态效果、指针移开、不留焦点、等网页字体。Playwright 用 vue-tsx-pro 的 apps/host-demo 装好的那份（同 ts-styles）。
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';
import { parseArgs } from 'node:util';
import { repoRoot } from '../lib/vue.mjs';

const USAGE = `用法：node tools/ts-shots/ts-shots.mjs <页面> [示例,…] [选项]

  <页面>          文档站的组件页：checkbox、icon-button（/components/<页面>）
  [示例,…]        只截这几个示例（小节标题的 id，同 Gallery 示例名）；不写时截整页的示例

选项：
  --origin <地址>  文档站（默认 http://localhost:5274）
  --themes <列表>  主题（默认 light,dark）
  --scales <列表>  缩放（默认 1,2）
  --width <像素>   视口宽度（默认 1280：示例区内容宽 756）
  --vue <路径>     vue-tsx-pro 的副本（找 Playwright；默认 ~/.rune/vtp-docs）

输出：artifacts/ts-shots/<页面>/<示例>-<主题>@<缩放>x.png 与 <示例>-<主题>.json（效果区的宽高、文字盒子）`;

const { values: options, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    origin: { type: 'string', default: 'http://localhost:5274' },
    themes: { type: 'string', default: 'light,dark' },
    scales: { type: 'string', default: '1,2' },
    width: { type: 'string', default: '1280' },
    vue: { type: 'string', default: join(process.env.USERPROFILE ?? process.env.HOME ?? '', '.rune/vtp-docs') },
    help: { type: 'boolean', default: false },
  },
});
const [pageName, only] = positionals;
if (options.help || !pageName) {
  console.log(USAGE);
  process.exit(options.help ? 0 : 2);
}

const vue = resolve(options.vue);
const { chromium } = createRequire(join(vue, 'apps/host-demo/package.json'))('playwright');
const out = join(repoRoot, 'artifacts/ts-shots', pageName);
mkdirSync(out, { recursive: true });
const wanted = only ? new Set(only.split(',')) : null;

const browser = await chromium.launch();
let count = 0;
for (const theme of options.themes.split(',')) {
  for (const scale of options.scales.split(',').map(Number)) {
    const context = await browser.newContext({
      viewport: { width: Number(options.width), height: 900 },
      deviceScaleFactor: scale,
      reducedMotion: 'reduce',
      locale: 'zh-CN',
    });
    if (theme !== 'light' && theme !== 'dark') await context.addInitScript((name) => localStorage.setItem('vue-tsx-pro-theme', name), theme);
    const page = await context.newPage();
    await page.emulateMedia({ colorScheme: theme === 'dark' ? 'dark' : 'light', reducedMotion: 'reduce' });
    await page.goto(`${options.origin}/components/${pageName}#top`, { waitUntil: 'networkidle' });
    await page.waitForFunction(() => document.querySelector('main h1'), null, { timeout: 120_000 });
    // 推迟渲染的示例卡片：滚一遍让它们都渲染出来
    await page.evaluate(async () => {
      for (let y = 0; y < document.body.scrollHeight; y += 600) {
        window.scrollTo(0, y);
        await new Promise((done) => setTimeout(done, 60));
      }
      window.scrollTo(0, 0);
      await document.fonts.ready;
    });
    const ids = await page.evaluate(() =>
      [...document.querySelectorAll('h2[id]')].filter((heading) => heading.parentElement.querySelector('[data-demo]')).map((heading) => heading.id));
    for (const id of ids.filter((id) => !wanted || wanted.has(id))) {
      const canvas = page.locator(`[id="${id}"]`).locator('xpath=..').locator('[data-demo]').first();
      await canvas.scrollIntoViewIfNeeded();
      // 停住：指针移开、不留焦点，等过渡结束
      await page.mouse.move(0, 0);
      await page.evaluate(() => document.activeElement instanceof HTMLElement && document.activeElement.blur());
      await page.waitForTimeout(150);
      await canvas.evaluate(alignToPixels);
      await canvas.screenshot({ path: join(out, `${id}-${theme}@${scale}x.png`), animations: 'disabled' });
      if (scale === 1) writeFileSync(join(out, `${id}-${theme}.json`), `${JSON.stringify(await canvas.evaluate(textBoxes), null, 1)}\n`);
      count++;
    }
    await context.close();
  }
}
await browser.close();
console.log(`${count} 张截图在 ${out}`);

/**
 * 把效果区对到整 CSS 像素上。它可能落在半像素上（上面说明文字的行高带小数），这时 Chrome 把里面的边线、底色吸附到
 * 四舍五入的像素，与原生平台在整像素原点上的渲染差半个像素的相位（行线总差一行）；元素截图还会多截进上面一行的边线。
 * 用 margin 挪（另一边补回去，后面的布局不变）：不改包含块、不建合成层，文字的抗锯齿不变。
 * 高度带小数时底部补到整像素（同原生平台的画布），不然截图的最后一行是下面元素的边线。
 */
function alignToPixels(canvas) {
  const { x, y, height } = canvas.getBoundingClientRect();
  const [dx, dy, extra] = [Math.round(x) - x, Math.round(y) - y, Math.ceil(height) - height];
  const style = getComputedStyle(canvas);
  if (dx || dy) {
    canvas.style.margin = [
      parseFloat(style.marginTop) + dy, parseFloat(style.marginRight) - dx, parseFloat(style.marginBottom) - dy, parseFloat(style.marginLeft) + dx,
    ].map((value) => `${value}px`).join(' ');
  }
  if (extra) canvas.style.paddingBottom = `${parseFloat(style.paddingBottom) + extra}px`;
}

/** 效果区的宽高与里面每段可见文字的盒子（逐行；CSS 像素，相对效果区的左上角） */
function textBoxes(canvas) {
  const origin = canvas.getBoundingClientRect();
  const texts = [];
  const walker = document.createTreeWalker(canvas, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (!node.textContent.trim()) continue;
    const style = getComputedStyle(node.parentElement);
    if (style.visibility === 'hidden' || style.opacity === '0') continue;
    const range = document.createRange();
    range.selectNodeContents(node);
    for (const rect of range.getClientRects()) {
      if (rect.width < 1 || rect.height < 1) continue;
      texts.push([rect.x - origin.x, rect.y - origin.y, rect.width, rect.height].map((value) => Math.round(value * 100) / 100));
    }
  }
  // 输入框里的文字（值、占位文字）不是文本节点：取有文字的输入框的内容区（去掉内边距）
  for (const input of canvas.querySelectorAll('input:not([type=checkbox]):not([type=radio]):not([type=range]), textarea')) {
    if (!(input.value || input.placeholder)) continue;
    const style = getComputedStyle(input);
    if (style.visibility === 'hidden' || style.display === 'none') continue;
    const rect = input.getBoundingClientRect();
    const [left, top] = [parseFloat(style.paddingLeft) + parseFloat(style.borderLeftWidth), parseFloat(style.paddingTop) + parseFloat(style.borderTopWidth)];
    const [right, bottom] = [parseFloat(style.paddingRight) + parseFloat(style.borderRightWidth), parseFloat(style.paddingBottom) + parseFloat(style.borderBottomWidth)];
    if (rect.width - left - right < 1 || rect.height - top - bottom < 1) continue;
    texts.push([rect.x + left - origin.x, rect.y + top - origin.y, rect.width - left - right, rect.height - top - bottom].map((value) => Math.round(value * 100) / 100));
  }
  return { width: origin.width, height: origin.height, texts };
}
