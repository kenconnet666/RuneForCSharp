#!/usr/bin/env node
// 查 vue-tsx-pro（TS 版）的组件、token、语言包与依赖分层，不用通读源码就能拿到移植要的信息。
// 解析用 vue-tsx-pro 自带的 TypeScript 7（typescript/sync、typescript/ast），本仓库不装 node 包。
import { parseArgs } from 'node:util';
import { openVue } from '../lib/vue.mjs';
import { component } from './component.mjs';
import { deps } from './deps.mjs';
import { lang } from './lang.mjs';
import { token } from './token.mjs';

const USAGE = `用法：node tools/ts-inspect/ts-inspect.mjs <命令> [参数] [选项]

命令：
  component <组件名>        props、部件、配方（逐条 token）、样式表、默认值、语言包、依赖
  token [名字]              token 在 16 套主题里的取值；名字可以是 accent、_accent、colors.accent；不带名字列出所有组
  token --group <组>        一组 token 的默认值与说明；--themes dark,paper 加几列主题
  lang [前缀]               语言包的键、说明与中英文案（组件文案以组件名开头：select、datePicker）
  deps [组件名]             依赖分层表；带组件名时列出它往下用到、往上被用到的组件（--all 含扩展与 patterns）

选项：
  --json                    输出 JSON（默认 Markdown）
  --vue <路径>              vue-tsx-pro 仓库（默认取环境变量 VUE_TSX_PRO，再默认与本仓库同级的 IdeaProjects/vue-tsx-pro）
`;

const { values: options, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    json: { type: 'boolean', default: false },
    vue: { type: 'string' },
    group: { type: 'string' },
    themes: { type: 'string' },
    all: { type: 'boolean', default: false },
    help: { type: 'boolean', short: 'h', default: false },
  },
});

const COMMANDS = {
  component: (vue, [name]) => {
    if (!name) throw new Error('component 要给组件名，如 component Button');
    return component(vue, name);
  },
  token: (vue, [name]) => token(vue, name, options),
  lang: (vue, [prefix]) => lang(vue, prefix),
  deps: (vue, [name]) => deps(vue, name, options),
};

const [command, ...args] = positionals;
if (options.help || !command) {
  process.stdout.write(USAGE);
  process.exit(command || options.help ? 0 : 1);
}
if (!COMMANDS[command]) {
  process.stderr.write(`没有命令 ${command}\n\n${USAGE}`);
  process.exit(1);
}

const vue = await openVue(options.vue);
try {
  const { data, markdown } = COMMANDS[command](vue, args);
  process.stdout.write(options.json ? `${JSON.stringify(data, null, 2)}\n` : markdown);
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
} finally {
  vue.close();
}
