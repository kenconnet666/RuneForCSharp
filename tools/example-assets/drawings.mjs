#!/usr/bin/env node
// 示例用的“零件图纸”（Image、Lightbox 的示例）：上游在浏览器里画 SVG（apps/docs/.../Image/demo/drawings.ts），三个桌面库解不了 SVG，
// 这里用 Playwright 把同一份 SVG 截成 PNG（1200 × 800），放进 docs/site/drawings，三个示例项目复制到输出目录。
// 用法：node tools/example-assets/drawings.mjs [vue-tsx-pro 的副本，默认 ~/.rune/vtp-docs]
import { mkdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { homedir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { repoRoot } from '../lib/vue.mjs';

const vue = resolve(process.argv[2] ?? join(homedir(), '.rune/vtp-docs'));
const { chromium } = createRequire(join(vue, 'apps/host-demo/package.json'))('playwright');
// drawings.ts 只有类型标注，Node 直接剥掉类型运行
const { drawings } = await import(pathToFileURL(join(vue, 'apps/docs/src/components/display/Image/demo/drawings.ts')).href);

const out = join(repoRoot, 'docs/site/drawings');
mkdirSync(out, { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
for (const drawing of drawings) {
  await page.setContent(`<body style="margin:0"><img src="${drawing.src}" width="1200" height="800" style="display:block"></body>`);
  await page.locator('img').screenshot({ path: join(out, `${drawing.code}.png`) });
  console.log(`${drawing.code}.png  ${drawing.title}`);
}
await browser.close();

