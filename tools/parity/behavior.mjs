#!/usr/bin/env node
// 行为对照：vue-tsx-pro 一个组件的测试用例（packages/ui/src/**/.test/<组件>.test.tsx 里的 it）逐条对上三个平台的测试
// （测试方法上的 [Upstream("组件", "it 名字的开头")]），桌面上不适用的登记在 registry.mjs 的 BEHAVIOR_NOT_APPLICABLE。
//
//   node tools/parity/behavior.mjs [组件,…] [--json]
//
// 不写组件时取三个平台里至少一个已经做了的组件。有没对上的用例时退出码为 1。
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { basename, join, relative } from 'node:path';
import { parseArgs } from 'node:util';
import { table } from '../lib/markdown.mjs';
import { openVue, repoRoot } from '../lib/vue.mjs';
import { BEHAVIOR_NOT_APPLICABLE, TYPE_RENAMES } from './registry.mjs';

const { values: options, positionals } = parseArgs({ allowPositionals: true, options: { json: { type: 'boolean', default: false }, vue: { type: 'string' } } });

const PLATFORMS = {
  avalonia: { src: 'avalonia/src/Rune.Avalonia', tests: 'avalonia/tests/Rune.Avalonia.Tests', library: 'Avalonia' },
  wpf: { src: 'wpf/src/Rune.Wpf', tests: 'wpf/tests/Rune.Wpf.Tests', library: 'WPF' },
  winforms: { src: 'winforms/src/Rune.WinForms', tests: 'winforms/tests/Rune.WinForms.Tests', library: 'WinForms' },
};

function files(dir, pattern) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (name === 'bin' || name === 'obj' || name === 'node_modules') continue;
    if (statSync(path).isDirectory()) out.push(...files(path, pattern));
    else if (pattern.test(name)) out.push(path);
  }
  return out;
}

/**
/**
 * vue-tsx-pro：组件 → it 名字。用例归到外层 describe 写的组件（display.test.tsx 里的 describe('Card')）；
 * describe 里并列几个组件（'Card、Descriptions、Text'）时归到用例名开头的那个；describe 不是组件名、或不在 describe 里时，
 * 用例名以组件名开头的归到它，否则归到文件名（Badge.test.tsx）
 */
function upstreamTests(src) {
  const tests = new Map();
  const add = (component, name) => tests.set(component, [...(tests.get(component) ?? []), name]);
  for (const file of files(src, /\.test\.tsx?$/)) {
    if (!file.includes('.test')) continue;
    const fileComponent = basename(file).replace(/\.test\.tsx?$/, '');
    const describes = [];
    for (const line of readFileSync(file, 'utf8').split(/\r?\n/)) {
      const indent = line.length - line.trimStart().length;
      // 缩进不比 describe 深的非空、非收尾行：那个 describe 结束了
      while (describes.length && describes.at(-1).indent >= indent && /^\s*\S/.test(line) && !/^\s*[)}\]]/.test(line)) describes.pop();
      const describe = /^\s*describe\(\s*(['"`])((?:(?!\1).)*)\1/.exec(line);
      if (describe) {
        describes.push({ indent, names: describe[2].split('、').map((name) => name.trim()).filter((name) => /^[A-Z]\w*$/.test(name)) });
        continue;
      }
      // it('名字', …)，以及 it.each([...])('名字', …) 的最后一行
      const test = /^\s*(?:(?:it|test)\(|\]\)\()\s*(['"`])((?:(?!\1).)*)\1/.exec(line);
      if (!test) continue;
      const names = describes.findLast((d) => d.names.length)?.names ?? [];
      // describe 不是组件名（'受控：…'）时，用例名以组件名开头的归到那个组件（'Radio：整组…'）
      add(names.find((name) => test[2].startsWith(name)) ?? names[0] ?? /^[A-Z]\w*/.exec(test[2])?.[0] ?? fileComponent, test[2]);
    }
  }
  return tests;
}

/** 一个平台：[Upstream("组件", "开头")] → 测试方法名；以及库里有哪些类型 */
function platform({ src, tests }) {
  const marks = [];
  for (const file of files(join(repoRoot, tests), /\.cs$/)) {
    const text = readFileSync(file, 'utf8');
    // 每个标记归到它后面最近的测试方法（一个方法可以标几条）
    for (const m of text.matchAll(/\[Upstream\("(\w+)",\s*"([^"]*)"\)\]/g)) {
      const method = /public\s+(?:async\s+)?\w+\s+(\w+)\s*\(/.exec(text.slice(m.index))?.[1];
      marks.push({ component: m[1], prefix: m[2], method, file: relative(repoRoot, file) });
    }
  }
  const types = new Set();
  for (const file of files(join(repoRoot, src), /\.cs$/))
    for (const m of readFileSync(file, 'utf8').matchAll(/\bclass\s+(\w+)/g)) types.add(m[1]);
  return { marks, types };
}

const vue = await openVue(options.vue);
const upstream = upstreamTests(vue.src);
vue.close?.();
const platforms = Object.fromEntries(Object.entries(PLATFORMS).map(([name, paths]) => [name, { ...platform(paths), library: paths.library }]));
// 做了没有：类型名同组件，或平台上改了名（registry 的 TYPE_RENAMES），WinForms 只有控件的组件叫 XxxControl
const made = (p, component) => {
  const type = TYPE_RENAMES[p.library]?.[component] ?? component;
  return p.types.has(type) || p.types.has(`${type}Control`);
};
const components = positionals[0]?.split(',') ?? [...upstream.keys()].filter((c) => Object.values(platforms).some((p) => made(p, c))).sort();

const results = [];
for (const component of components) {
  const names = upstream.get(component) ?? [];
  const notApplicable = BEHAVIOR_NOT_APPLICABLE[component] ?? {};
  for (const name of names) {
    const na = Object.entries(notApplicable).find(([prefix]) => name.startsWith(prefix));
    const row = { component, test: name, notApplicable: na?.[1] ?? null, platforms: {} };
    for (const [platformName, p] of Object.entries(platforms)) {
      if (!made(p, component)) {
        row.platforms[platformName] = '（组件还没做）';
        continue;
      }
      row.platforms[platformName] = p.marks.filter((m) => m.component === component && name.startsWith(m.prefix)).map((m) => m.method).join('、') || null;
    }
    results.push(row);
  }
  // 标了却对不上任何用例的（上游改了措辞或删了）
  for (const [platformName, p] of Object.entries(platforms))
    for (const m of p.marks.filter((m) => m.component === component && !names.some((n) => n.startsWith(m.prefix))))
      results.push({ component, test: `（对不上）${m.prefix}`, notApplicable: null, platforms: { [platformName]: `${m.method}（${m.file}）` }, stale: true });
}

const missing = results.filter((r) => r.stale || (!r.notApplicable && Object.values(r.platforms).some((v) => v === null)));
if (options.json) process.stdout.write(`${JSON.stringify(results, null, 2)}\n`);
else {
  const lines = ['# 行为对照：vue-tsx-pro 的测试用例 → 三个平台的测试', ''];
  for (const component of components) {
    const rows = results.filter((r) => r.component === component);
    if (!rows.length) continue;
    lines.push(`## ${component}`, '', table(['用例', 'Avalonia', 'WPF', 'WinForms'], rows.map((r) => [
      r.test,
      ...['avalonia', 'wpf', 'winforms'].map((p) => r.notApplicable ? `不适用：${r.notApplicable}` : r.platforms[p] === null ? '**缺**' : r.platforms[p] ?? ''),
    ])), '');
  }
  lines.push(`共 ${results.length} 条，${missing.length} 条没对上。`, '');
  process.stdout.write(lines.join('\n'));
}
process.exitCode = missing.length ? 1 : 0;
