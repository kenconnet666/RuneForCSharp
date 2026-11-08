#!/usr/bin/env node
// 名字对照：vue-tsx-pro 的组件（按依赖分层）在三个库里做了没有；已做的组件比对 props 与部件属性；语言包的键对不对得上。
// C# 一侧读源码（公开的类与属性），不需要先构建。改名与不做的组件登记在 registry.mjs。
// 用法：node tools/parity/parity.mjs [--component <名字>] [--json] [--vue <路径>]
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { SKIPPED as LANG_SKIPPED } from '../lang-gen/skipped.mjs';
import { langMembers } from '../lib/lang.mjs';
import { code, table } from '../lib/markdown.mjs';
import { openVue, repoRoot } from '../lib/vue.mjs';
import { component as inspectComponent } from '../ts-inspect/component.mjs';
import { COMPONENT_PROP_RENAMES, EXCLUDED, PLATFORM_PROPS, PROP_RENAMES, PLATFORM_COMPONENT_PROP_RENAMES, PLATFORM_PROP_RENAMES, PLATFORM_PROPS_NOT_PORTED, PROPS_NOT_PORTED, TYPE_RENAMES } from './registry.mjs';

const LIBRARIES = [
  { name: 'Avalonia', dir: 'avalonia/src/Rune.Avalonia' },
  { name: 'WPF', dir: 'wpf/src/Rune.Wpf' },
  { name: 'WinForms', dir: 'winforms/src/Rune.WinForms' },
];

const pascal = (name) => name.replace(/^_/, '').replace(/^\w/, (c) => c.toUpperCase());
// 回调 onCopied 是 C# 的事件 Copied
const csName = (component, prop) => COMPONENT_PROP_RENAMES[component]?.[prop] ?? PROP_RENAMES[prop] ?? pascal(prop.replace(/^on(?=[A-Z])/, ''));

/** vue-tsx-pro 的组件：`export const 名字 = _component(…)` 或工厂（createText、createFlex……，createContext 不算） */
function vueComponents(vue) {
  const { level, isInfra } = vue.graph;
  return vue.files
    .filter((file) => !isInfra(file))
    .flatMap((file) =>
      [...vue.text(file).matchAll(/^export const ([A-Z]\w*) = (_component|create(?!Context)[A-Z]\w*)\(/gm)].map(([, name]) => ({
        name,
        file: file.replace(/\.tsx?$/, ''),
        level: level.get(file),
      })),
    )
    .sort((a, b) => a.level - b.level || a.file.localeCompare(b.file) || a.name.localeCompare(b.name));
}

/**
 * 一个库的 C# 源码：公开的类 → 它的公开属性与事件（按文件读，一个文件里的属性算给文件里声明的类），
 * 加上库里基类的（IconButton 的 Variant 在 ButtonCore 里）。限定名的基类（global::Avalonia.Controls.Button）是平台的，不展开
 */
function csharpTypes(dir) {
  const types = new Map();
  const bases = new Map();
  const walk = (path) => {
    for (const name of readdirSync(path)) {
      const full = join(path, name);
      if (name === 'bin' || name === 'obj') continue;
      if (statSync(full).isDirectory()) walk(full);
      else if (name.endsWith('.cs')) {
        const text = readFileSync(full, 'utf8');
        const declarations = [...text.matchAll(/public\s+(?:(?:sealed|partial|abstract|static)\s+)*(?:class|record)\s+(\w+)(?:<[^>{]*>)?(?:\([^)]*\))?(?:\s*:\s*([\w.:]+))?/g)];
        // 泛型基类只取到名字（ButtonControl : ButtonControl<Button> 取到的就是自己），自己不算基类
        for (const [, type, base] of declarations) if (base && base !== type && !/[.:]/.test(base)) bases.set(type, base);
        const classes = declarations.map((m) => m[1]);
        // 类型可以带空格（Func<string, string>、值元组），按行匹配：修饰符、类型、名字，后面是 { 或 =>
        const props = [...text.matchAll(/public\s+(?:(?:static|required|override|virtual|new)\s+)*[^\n=;]*?\S\s+(\w+)\s*(?:\{|=>)/g)].map((m) => m[1]);
        // 部件名常量（Avalonia 的 `SpinnerParts.Icon = "PART_Icon"`、WPF 的部件样式键）：按字段收进所在的类
        const fields = [...text.matchAll(/public\s+(?:const|static\s+readonly)\s+[\w.<>?]+\s+(\w+)\s*=/g)].map((m) => m[1]);
        // 附加属性（rune:Tooltip.Placement）：字段名去掉 Property
        const attached = fields.filter((name) => name.endsWith('Property')).map((name) => name.slice(0, -'Property'.length));
        // 扩展属性（WinForms 的 TooltipProvider.GetTooltip(control)）：一个参数的 GetXxx
        const extended = [...text.matchAll(/public\s+[\w.<>?]+\s+Get(\w+)\(\s*Control\s+\w+\s*\)/g)].map((m) => m[1]);
        // 事件（TS 的 onCopied 回调）
        const events = [...text.matchAll(/public\s+event\s+[^\n;]*?\s(\w+)\s*;/g)].map((m) => m[1]);
        for (const type of classes) types.set(type, new Set([...(types.get(type) ?? []), ...props, ...fields, ...attached, ...extended, ...events]));
      }
    }
  };
  walk(join(repoRoot, dir));
  const inherited = (type, seen = new Set()) =>
    seen.has(type) ? [] : (seen.add(type), [...(types.get(type) ?? []), ...(bases.has(type) ? inherited(bases.get(type), seen) : [])]);
  const merged = new Map([...types.keys()].map((type) => [type, new Set(inherited(type))]));
  // WinForms 里只有控件、没有元素的组件（容器类：BlockquoteControl）按控件名对上
  for (const [type, props] of [...merged]) if (type.endsWith('Control') && !merged.has(type.slice(0, -'Control'.length))) merged.set(type.slice(0, -'Control'.length), props);
  // 基类的部件也算（CloseButton 沿用 IconButton 的模板，Checkbox 的文字、说明在 ChoiceBaseParts）：自己的部件名类并上各级基类的
  for (const type of types.keys()) {
    const seen = new Set([type]);
    for (let base = bases.get(type); base && !seen.has(base); base = bases.get(base)) {
      seen.add(base);
      if (merged.has(`${base}Parts`)) merged.set(`${type}Parts`, new Set([...(merged.get(`${type}Parts`) ?? []), ...merged.get(`${base}Parts`)]));
    }
  }
  return merged;
}

/**
 * props 与部件。部件（`propsIcon`）有两种写法都算有：WinForms 的部件属性记录（`PropsIcon`），
 * Avalonia、WPF 的部件名常量（`SpinnerParts.Icon`，DESIGN §7）
 */
function compareProps(vue, name, members, parts, library) {
  const data = inspectComponent(vue, name).data;
  const expected = [...data.props, ...data.parts]
    .filter((prop) => !PROPS_NOT_PORTED[name]?.[prop.name] && !PLATFORM_PROPS_NOT_PORTED[library]?.[name]?.[prop.name])
    .map((prop) => ({ prop: prop.name, cs: csName(name, prop.name), alias: PLATFORM_COMPONENT_PROP_RENAMES[library]?.[name]?.[prop.name] ?? PLATFORM_PROP_RENAMES[library]?.[prop.name] }));
  const isPart = (cs) => cs.startsWith('Props') && parts?.has(cs.slice('Props'.length));
  const missing = expected.filter(({ cs, alias }) => !members.has(cs) && !members.has(alias) && !PLATFORM_PROPS.has(cs) && !PLATFORM_PROPS.has(alias) && !isPart(cs));
  const known = new Set(expected.flatMap(({ cs, alias }) => (alias ? [cs, alias] : [cs])));
  return { missing, expected: expected.length, extra: [...members].filter((member) => !known.has(member)) };
}

const { values: options } = parseArgs({
  options: {
    component: { type: 'string' },
    json: { type: 'boolean', default: false },
    vue: { type: 'string' },
  },
});

const vue = await openVue(options.vue);
try {
  const components = vueComponents(vue).filter((c) => !options.component || c.name === options.component);
  const libraries = LIBRARIES.map((library) => ({ ...library, types: csharpTypes(library.dir) }));
  const rows = components.map((c) => ({
    ...c,
    excluded: EXCLUDED[c.name],
    platforms: Object.fromEntries(
      libraries.map((library) => {
        const type = TYPE_RENAMES[library.name]?.[c.name] ?? c.name;
        const members = library.types.get(type);
        return [library.name, members ? compareProps(vue, c.name, members, library.types.get(`${type}Parts`), library.name) : null];
      }),
    ),
  }));

  // 语言包：Lang 的键（去掉不移植的）在三个库的 Lang 里都要有
  const langKeys = langMembers(vue).filter((member) => !LANG_SKIPPED[member.name]).map((member) => pascal(member.name));
  const langGaps = Object.fromEntries(
    libraries.map((library) => {
      const members = library.types.get('Lang') ?? new Set();
      return [library.name, langKeys.filter((key) => !members.has(key))];
    }),
  );

  const data = { commit: vue.commit, rows, langGaps };
  if (options.json) process.stdout.write(`${JSON.stringify(data, null, 2)}\n`);
  else process.stdout.write(render(data));
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
} finally {
  vue.close();
}

function render({ commit, rows, langGaps }) {
  const mark = (row, platform) => {
    if (row.excluded) return '不做';
    const result = row.platforms[platform];
    if (!result) return '—';
    return result.missing.length ? `缺 ${result.missing.length}/${result.expected}` : '✓';
  };
  const names = LIBRARIES.map((library) => library.name);
  const active = rows.filter((row) => !row.excluded);
  let out = `# 对照（vue-tsx-pro ${commit}）\n\n`;
  out += '✓ 是 props 与部件属性都对得上；"缺 n/m" 是有这个组件但缺 n 个（共 m 个）；— 是还没做。\n\n## 进度\n\n';
  out += table(
    ['层', '组件数', ...names],
    [...Map.groupBy(active, (row) => row.level)].map(([level, group]) => [
      `L${level}`,
      group.length,
      ...names.map((platform) => group.filter((row) => row.platforms[platform]).length),
    ]),
  );
  out += `\n## 组件\n\n${table(['层', '组件', '文件', ...names], rows.map((row) => [`L${row.level}`, row.name, row.file, ...names.map((platform) => mark(row, platform))]))}`;
  const gaps = rows.flatMap((row) => names.filter((platform) => row.platforms[platform]?.missing.length).map((platform) => [row.name, platform, row.platforms[platform].missing.map(({ prop, cs }) => `${prop} → ${cs}`).join('、')]));
  if (gaps.length) out += `\n## 已做的组件里缺的 props\n\n${table(['组件', '平台', '缺的（TS → C#）'], gaps)}`;
  out += '\n## 语言包\n\n';
  const langLines = names.map((platform) => `- ${platform}：${langGaps[platform].length ? `缺 ${langGaps[platform].map(code).join('、')}` : '键都对得上'}`);
  out += `${langLines.join('\n')}\n`;
  if (Object.keys(EXCLUDED).length) out += `\n不做的组件：${Object.entries(EXCLUDED).map(([name, why]) => `${name}（${why}）`).join('；')}\n`;
  return out;
}
