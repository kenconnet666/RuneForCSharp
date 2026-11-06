// Claude Code 的 C# 语言服务器入口（.claude/marketplace/rune-csharp-lsp/.lsp.json 调用）。
// 找到装有 global.json 所要 SDK 的 dotnet（有的机器 .NET 10 只在 ~/.dotnet，PATH 上的 dotnet 没有），
// 还原仓库的本地工具（dotnet-tools.json 固定的 csharp-ls），再以 stdio 启动它加载解决方案。
// stdout 是 LSP 的通道：启动前的任何输出都只能写 stderr。
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { delimiter, dirname, join, resolve } from 'node:path';

const repo = resolve(import.meta.dirname, '../..');
const solution = 'RuneForCSharp.slnx';
const wanted = JSON.parse(readFileSync(join(repo, 'global.json'), 'utf8')).sdk.version;

/** global.json 的 rollForward 是 latestFeature：主次版本相同、功能带不低于要求的 SDK 都行 */
function satisfies(version) {
  const [major, minor, patch] = version.split('.').map((part) => Number.parseInt(part, 10));
  const [wantMajor, wantMinor, wantPatch] = wanted.split('.').map(Number);
  return major === wantMajor && minor === wantMinor && Math.floor(patch / 100) >= Math.floor(wantPatch / 100);
}

function findDotnet() {
  const exe = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const dirs = [process.env.DOTNET_ROOT, join(homedir(), '.dotnet'), ...(process.env.PATH ?? '').split(delimiter)];
  for (const dir of dirs) {
    if (!dir) continue;
    const dotnet = join(dir, exe);
    if (!existsSync(dotnet)) continue;
    const sdks = spawnSync(dotnet, ['--list-sdks'], { encoding: 'utf8' });
    if (sdks.status === 0 && sdks.stdout.split(/\r?\n/).some((line) => line && satisfies(line.split(' ')[0]))) return dotnet;
  }
  throw new Error(`找不到装有 .NET SDK ${wanted}（或同一功能带更新的版本）的 dotnet`);
}

const dotnet = findDotnet();
const root = dirname(dotnet);
const env = { ...process.env, DOTNET_ROOT: root, PATH: `${root}${delimiter}${process.env.PATH ?? ''}` };

const restore = spawnSync(dotnet, ['tool', 'restore'], { cwd: repo, env, stdio: ['ignore', process.stderr, process.stderr] });
if (restore.status !== 0) process.exit(restore.status ?? 1);

const server = spawn(dotnet, ['tool', 'run', 'csharp-ls', '--', '--solution', solution], { cwd: repo, env, stdio: 'inherit' });
server.on('exit', (code, signal) => process.exit(code ?? (signal ? 1 : 0)));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.kill(signal));
