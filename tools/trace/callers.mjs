// 读 dotnet-trace 转成的 speedscope JSON（evented 格式），找经过某个帧的时间，按它往上若干层的调用链汇总：看是谁调到了热点。
// 用法：node tools/trace/callers.mjs <xxx.speedscope.json> <帧名片段> [层数]（DESIGN §9 有完整的采样步骤）
import { readFileSync } from 'node:fs';

const [file, needle, depthArg] = process.argv.slice(2);
const depth = Number(depthArg ?? 8);
const doc = JSON.parse(readFileSync(file, 'utf8'));
const frames = doc.shared.frames.map((f) => f.name.replace(/^[\w.]+\.il!/, '').replace(/\(.*$/, ''));
const counts = new Map();
let total = 0;
for (const profile of doc.profiles) {
  if (profile.type !== 'evented') continue;
  const stack = [];
  let last = profile.startValue;
  const flush = (at) => {
    const index = stack.findIndex((f) => frames[f].includes(needle));
    if (index >= 0 && at > last) {
      total += at - last;
      const chain = stack.slice(Math.max(0, index - depth), index + 1).map((f) => frames[f]).join('\n    > ');
      counts.set(chain, (counts.get(chain) ?? 0) + (at - last));
    }
    last = at;
  };
  for (const event of profile.events) {
    flush(event.at);
    if (event.type === 'O') stack.push(event.frame);
    else stack.pop();
  }
}
console.log(`total ${total.toFixed(1)} ms`);
for (const [chain, time] of [...counts].sort((a, b) => b[1] - a[1]).slice(0, 6)) console.log(`${(100 * time / total).toFixed(1)}%\n    ${chain}\n`);
