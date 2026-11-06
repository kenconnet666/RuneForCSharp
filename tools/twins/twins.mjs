#!/usr/bin/env node
// 三个库各有一份、除命名空间外逐字相同的源文件（纯计算，没有平台类型：FlexLayout、GridLayout）。三个库不共享代码（ROADMAP 决定 2），
// 一致性靠这里检查：改了一份，另外两份照抄。用法：node tools/twins/twins.mjs；有不同时退出码为 1
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../lib/vue.mjs';

/** 一组孪生文件：三个库里的路径 */
const TWINS = [
  ['avalonia/src/Rune.Avalonia/Layout/Flex/FlexLayout.cs', 'wpf/src/Rune.Wpf/Layout/Flex/FlexLayout.cs', 'winforms/src/Rune.WinForms/Layout/Flex/FlexLayout.cs'],
  ['avalonia/src/Rune.Avalonia/Layout/Grid/GridLayout.cs', 'wpf/src/Rune.Wpf/Layout/Grid/GridLayout.cs', 'winforms/src/Rune.WinForms/Layout/Grid/GridLayout.cs'],
];

/** 去掉命名空间那一行，统一换行符 */
const normalize = (path) =>
  readFileSync(join(repoRoot, path), 'utf8')
    .replace(/\r\n/g, '\n')
    .replace(/^namespace [\w.]+;$/m, 'namespace ?;');

let failed = false;
for (const [first, ...others] of TWINS) {
  const expected = normalize(first);
  for (const other of others) {
    if (normalize(other) === expected) continue;
    failed = true;
    console.error(`${other} 和 ${first} 不一样（除命名空间外应当逐字相同）`);
  }
}
if (!failed) console.log(`孪生文件一致（${TWINS.length} 组）`);
process.exitCode = failed ? 1 : 0;
