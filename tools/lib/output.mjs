// 生成脚本共用的输出：写入有变化的文件，或（--check）只比较、列出不一致的文件
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { repoRoot } from './vue.mjs';

/**
 * 输出 `[{ path（相对仓库根）, content }]`。返回退出码：--check 时有不一致为 1。
 * `command` 是重新生成的命令，写进提示里
 */
export function writeOutputs(outputs, { check, commit, command }) {
  const stale = [];
  for (const { path, content } of outputs) {
    const file = join(repoRoot, path);
    const current = existsSync(file) ? readFileSync(file, 'utf8') : undefined;
    if (current === content) continue;
    stale.push(path);
    if (check) continue;
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, content);
  }
  if (check && stale.length) {
    process.stderr.write(`生成的文件与 vue-tsx-pro（${commit}）不一致，运行 ${command} 重新生成：\n${stale.map((path) => `  ${path}`).join('\n')}\n`);
    return 1;
  }
  const done = check ? `已核对 ${outputs.length} 个文件` : stale.length ? `已生成：${stale.join('、')}` : '没有变化';
  process.stdout.write(`${done}（vue-tsx-pro ${commit}）\n`);
  return 0;
}
