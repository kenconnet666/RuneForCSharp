// 调用三个平台的 inspect（Release 构建）：取值比对（parity/styles.mjs）、像素比对（parity/pixels.mjs）共用
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { repoRoot } from './vue.mjs';

/** 各平台 inspect 的可执行文件（相对仓库根目录） */
export const INSPECT = {
  avalonia: 'avalonia/tools/Rune.Avalonia.Inspect/bin/Release/net10.0/Rune.Avalonia.Inspect.exe',
  wpf: 'wpf/tools/Rune.Wpf.Inspect/bin/Release/net10.0-windows/Rune.Wpf.Inspect.exe',
  winforms: 'winforms/tools/Rune.WinForms.Inspect/bin/Release/net10.0-windows/Rune.WinForms.Inspect.exe',
};

/** 文档站的页面名 → 示例的组件名：icon-button → IconButton（inspect 的示例键是"组件/示例名"） */
export const componentOf = (pageName) => pageName.replace(/(^|-)(\w)/g, (_, __, c) => c.toUpperCase());

/** 某个平台的 inspect；没有 Release 构建时报错并说怎么建 */
export function inspector(platform) {
  const path = INSPECT[platform];
  const exe = path && join(repoRoot, path);
  if (!exe || !existsSync(exe)) throw new Error(`没有 ${platform} 的 inspect：先 Release 构建 ${path ?? `（不认识的平台，可选 ${Object.keys(INSPECT).join('、')}）`}`);
  // 这台机器的 .NET 10 在 ~/.dotnet（HANDOFF §3）
  const env = { ...process.env, DOTNET_ROOT: process.env.DOTNET_ROOT ?? join(homedir(), '.dotnet') };
  /** 跑一个命令，返回标准输出；失败时抛出（带上 inspect 的错误输出） */
  return (args) => execFileSync(exe, args, { env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}
