// 生成脚本共用的输出：写入有变化的文件，或（--check）只比较、列出不一致的文件
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { repoRoot } from './vue.mjs';

/**
 * 输出 `[{ path（相对仓库根）, content }]`。返回退出码：--check 时有不一致为 1。
 * `command` 是重新生成的命令，写进提示里。
 * `owned` 是整个归生成脚本管的目录（相对仓库根）：里面不在这次输出里的 `*.g.*` 文件是多余的
 * （上游删掉的主题），--check 时报出来，否则删掉
 */
export function writeOutputs(outputs, { check, commit, command, owned = [] }) {
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
  const generated = new Set(outputs.map(({ path }) => path));
  const extra = owned.flatMap((dir) =>
    existsSync(join(repoRoot, dir))
      ? readdirSync(join(repoRoot, dir)).filter((name) => /\.g\.\w+$/.test(name)).map((name) => `${dir}/${name}`).filter((path) => !generated.has(path))
      : [],
  );
  if (check && (stale.length || extra.length)) {
    const lines = [...stale.map((path) => `  ${path}`), ...extra.map((path) => `  ${path}（多余，上游已经没有）`)];
    process.stderr.write(`生成的文件与 vue-tsx-pro（${commit}）不一致，运行 ${command} 重新生成：\n${lines.join('\n')}\n`);
    return 1;
  }
  for (const path of extra) rmSync(join(repoRoot, path));
  const changed = [...stale, ...extra.map((path) => `${path}（已删除）`)];
  const done = check ? `已核对 ${outputs.length} 个文件` : changed.length ? `已生成：${changed.join('、')}` : '没有变化';
  process.stdout.write(`${done}（vue-tsx-pro ${commit}）\n`);
  return 0;
}
