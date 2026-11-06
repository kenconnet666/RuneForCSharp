#!/usr/bin/env node
// 从 vue-tsx-pro 的 token 与 16 套内置主题生成三个库的资源（DESIGN §6"token 的生成"）。
// 用法：node tools/token-gen/token-gen.mjs [--check] [--vue <路径>]
//   --check  只比较不写入：生成结果与仓库里的文件不一致时列出并以非零状态退出（CI 用）
import { parseArgs } from 'node:util';
import { writeOutputs } from '../lib/output.mjs';
import { openVue } from '../lib/vue.mjs';
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
  process.exitCode = writeOutputs(outputs, { check: options.check, commit: vue.commit, command: 'node tools/token-gen/token-gen.mjs' });
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
} finally {
  vue.close();
}
