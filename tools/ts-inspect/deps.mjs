// deps：组件的依赖分层（ROADMAP 阶段 3 的顺序以它为准），或一个组件往下用到、往上被用到的组件
import { EXTENSIONS } from '../lib/vue.mjs';
import { code, table } from '../lib/markdown.mjs';
import { componentFile } from './component.mjs';

const label = (file) => file.replace(/\.tsx?$/, '');

export function deps(vue, name, options) {
  return name ? one(vue, name) : layers(vue, options);
}

function layers(vue, { all }) {
  const { level, usedBy, uses, isInfra } = vue.graph;
  const rows = vue.files
    .filter((file) => !isInfra(file) && (all || !EXTENSIONS.has(file.split('/')[0])))
    .map((file) => ({
      file: label(file),
      level: level.get(file),
      usedBy: usedBy.get(file).size,
      uses: [...uses.get(file)].filter((target) => !isInfra(target) && target !== file).map(label).sort(),
    }))
    .sort((a, b) => a.level - b.level || a.file.localeCompare(b.file));
  const data = { commit: vue.commit, rows };
  const groups = Map.groupBy(rows, (row) => row.level);
  let markdown = `# 组件的依赖分层（vue-tsx-pro ${vue.commit}${all ? '，含扩展与 patterns' : ''}）\n\n`;
  markdown += '层号：只用基础设施（utils、config）的是 L0，否则是用到的组件文件的最大层号 + 1。"被用到"是直接用到它的组件文件数。\n';
  for (const [l, group] of groups) {
    markdown += `\n## L${l}（${group.length}）\n\n`;
    markdown += table(['文件', '被用到', '用到的组件文件'], group.map((row) => [row.file, row.usedBy, row.uses.join('、') || '—']));
  }
  return { data, markdown };
}

function one(vue, name) {
  const file = componentFile(vue, name);
  const { level, usedBy, uses, isInfra } = vue.graph;
  const closure = new Set();
  const walk = (current) => {
    for (const target of uses.get(current)) {
      if (target === current || closure.has(target)) continue;
      closure.add(target);
      walk(target);
    }
  };
  walk(file);
  const components = [...closure].filter((target) => !isInfra(target)).sort((a, b) => level.get(a) - level.get(b) || a.localeCompare(b));
  const infra = [...closure].filter(isInfra).sort();
  const data = {
    name,
    file,
    level: level.get(file),
    uses: [...uses.get(file)].filter((target) => target !== file).sort(),
    transitive: components,
    infrastructure: infra,
    usedBy: [...usedBy.get(file)].sort(),
  };
  let markdown = `# ${name} 的依赖（${file}，L${data.level}，vue-tsx-pro ${vue.commit}）\n\n`;
  markdown += `**直接用到**：${data.uses.map((target) => code(label(target))).join('、') || '—'}\n\n`;
  markdown += `**直接被用到**（${data.usedBy.length}）：${data.usedBy.map((target) => code(label(target))).join('、') || '—'}\n\n`;
  markdown += `## 往下用到的组件文件（含间接，先做层号小的）\n\n`;
  markdown += table(['层', '文件'], components.map((target) => [`L${level.get(target)}`, label(target)]));
  markdown += `\n## 往下用到的基础设施（含间接）\n\n${infra.map((target) => code(label(target))).join('、') || '—'}\n`;
  return { data, markdown };
}
