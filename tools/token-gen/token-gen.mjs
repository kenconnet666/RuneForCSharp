#!/usr/bin/env node
// 从 vue-tsx-pro 的 token 与 16 套内置主题生成三个库的资源（DESIGN §6"token 的生成"）。
// 用法：node tools/token-gen/token-gen.mjs [--check] [--vue <路径>]
//   --check  只比较不写入：生成结果与仓库里的文件不一致时列出并以非零状态退出（CI 用）
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { parseArgs } from 'node:util';
import { openVue, repoRoot } from '../lib/vue.mjs';
import { avalonia } from './avalonia.mjs';
import { buildModel } from './model.mjs';
import { winforms } from './winforms.mjs';
import { wpf } from './wpf.mjs';

const { values: options } = parseArgs({
  options: {
    check: { type: 'boolean', default: false },
    vue: { type: 'string' },
  },
});

const vue = await openVue(options.vue);
try {
  const model = buildModel(vue);
  const outputs = [...avalonia(model), ...wpf(model), ...winforms(model)];
  const stale = [];
  for (const { path, content } of outputs) {
    const file = join(repoRoot, path);
    const current = existsSync(file) ? readFileSync(file, 'utf8') : undefined;
    if (current === content) continue;
    stale.push(path);
    if (options.check) continue;
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, content);
  }
  if (options.check && stale.length) {
    process.stderr.write(`生成的文件与 vue-tsx-pro（${vue.commit}）不一致，运行 node tools/token-gen/token-gen.mjs 重新生成：\n${stale.map((path) => `  ${path}`).join('\n')}\n`);
    process.exitCode = 1;
  } else {
    process.stdout.write(`${options.check ? '已核对' : stale.length ? '已生成' : '没有变化'}（vue-tsx-pro ${vue.commit}）：${(options.check ? outputs.map((o) => o.path) : stale).join('、') || '—'}\n`);
  }
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
} finally {
  vue.close();
}
