// component：移植一个组件要知道的全部信息：说明、props、部件、配方（逐条 token）、样式表、默认值、语言包、依赖
import { code, table } from '../lib/markdown.mjs';
import { compact, docOf, entriesOf, fileDoc, find, findAll, flatten, initializerOf } from '../lib/syntax.mjs';

const label = (file) => file.replace(/\.tsx?$/, '');

/** 定义组件的文件：优先 `export const 名字 = _component(…)` */
export function componentFile(vue, name) {
  const files = vue.findExport(name, 'const|function|class');
  if (!files.length) {
    const lower = name.toLowerCase();
    const similar = vue.files
      .flatMap((file) => [...vue.text(file).matchAll(/^export const (\w+) = _component\(/gm)].map((m) => m[1]))
      .filter((candidate) => candidate.toLowerCase().includes(lower) || lower.includes(candidate.toLowerCase()));
    throw new Error(`找不到导出 ${name} 的文件${similar.length ? `；相近的组件：${[...new Set(similar)].join('、')}` : ''}`);
  }
  const defined = new RegExp(`^export const ${name} = _component\\(`, 'm');
  return files.find((file) => defined.test(vue.text(file))) ?? files.find((file) => file.endsWith('.tsx')) ?? files[0];
}

export function component(vue, name) {
  const { ast } = vue;
  const file = componentFile(vue, name);
  const source = vue.tree(file);
  const { level, usedBy, uses, isInfra } = vue.graph;

  const statement = find(source, (node) => ast.isVariableStatement(node) && node.getText(source).includes(`${name} =`))
    ?? find(source, (node) => (ast.isFunctionDeclaration(node) || ast.isClassDeclaration(node)) && node.name?.getText(source) === name);
  // 工厂生成的几个组件共用一个 props 接口（H1–H6 用 HeadingProps）：没有同名接口时取文件里第一个导出的 …Props
  const propsName = declarations(vue).has(`${name}Props`) ? `${name}Props` : vue.text(file).match(/^export interface (\w+Props)\b/m)?.[1];
  const props = propsName ? collectProps(vue, propsName) : { members: [], inherited: [] };
  // 同一目录里小写开头的文件是这个组件的内核与样式（button-core、badge-styles），组件的配方与样式表常在那里
  const dir = file.slice(0, file.lastIndexOf('/') + 1);
  const helpers = [...vue.imports(file).files].filter((target) => target.startsWith(dir) && /^[a-z]/.test(target.slice(dir.length)));
  const candidates = [file, ...helpers];

  const data = {
    name,
    file,
    level: level.get(file),
    commit: vue.commit,
    fileDoc: fileDoc(vue.text(file)),
    doc: statement ? docOf(statement, source, ast).text : '',
    propsInterface: propsName ?? '',
    props: props.members.filter((member) => !isPart(member.name)),
    parts: props.members.filter((member) => isPart(member.name)),
    inherited: props.inherited,
    recipes: recipes(vue, candidates, name),
    styles: candidates.flatMap((candidate) => styleTables(vue, candidate)),
    defaults: defaults(vue, candidates),
    lang: [...new Set(candidates.flatMap((candidate) => [...vue.text(candidate).matchAll(/\blang\.(\w+)/g)].map((m) => m[1])))].sort(),
    uses: [...uses.get(file)].filter((target) => target !== file).map(label).sort(),
    usedBy: [...usedBy.get(file)].map(label).sort(),
  };
  data.usesComponents = data.uses.filter((target) => !isInfra(target));
  return { data, markdown: render(data) };
}

const isPart = (name) => /^props[A-Z]/.test(name);

// ───────── props ─────────

/** 接口名 → 声明它的文件（含不导出的） */
function declarations(vue) {
  if (!vue.cache.has('declarations'))
    vue.cache.set('declarations', new Map(vue.files.flatMap((file) => [...vue.text(file).matchAll(/^(?:export )?(?:interface|type) (\w+)/gm)].map((m) => [m[1], file]))));
  return vue.cache.get('declarations');
}

/**
 * 收集 props：接口自己的成员，加上沿 extends 展开的本库接口（Omit / Pick / Partial 照着处理）。
 * 平台与工具类型（PartProps 等）不展开，列在 inherited 里
 */
function collectProps(vue, interfaceName, seen = new Set()) {
  const { ast } = vue;
  const result = { members: [], inherited: [] };
  const file = declarations(vue).get(interfaceName);
  if (!file || seen.has(interfaceName)) return result;
  seen.add(interfaceName);
  const source = vue.tree(file);
  const declaration = find(source, (node) => (ast.isInterfaceDeclaration(node) || ast.isTypeAliasDeclaration(node)) && node.name.getText(source) === interfaceName);
  if (!declaration) return result;

  const merge = (other, { omit, pick, partial } = {}) => {
    for (const member of other.members) {
      if (omit?.includes(member.name) || (pick && !pick.includes(member.name))) continue;
      if (result.members.some((existing) => existing.name === member.name)) continue;
      result.members.push(partial ? { ...member, optional: true } : member);
    }
    result.inherited.push(...other.inherited);
  };
  const expand = (typeNode) => {
    const text = compact(typeNode.getText(source));
    const utility = text.match(/^(Omit|Pick|Partial)<\s*(\w+)/);
    const base = utility ? utility[2] : text.match(/^(\w+)/)?.[1];
    if (!base || base === 'PartProps' || !declarations(vue).has(base)) {
      result.inherited.push(text);
      return;
    }
    const keys = [...text.matchAll(/'([^']+)'/g)].map((m) => m[1]);
    merge(collectProps(vue, base, seen), {
      omit: utility?.[1] === 'Omit' ? keys : undefined,
      pick: utility?.[1] === 'Pick' ? keys : undefined,
      partial: utility?.[1] === 'Partial',
    });
    if (utility?.[1] !== 'Partial' && base !== text.match(/^(\w+)/)?.[1]) result.inherited.push(text);
  };

  if (ast.isTypeAliasDeclaration(declaration)) {
    const typeNode = declaration.type;
    if (ast.isIntersectionTypeNode(typeNode)) for (const part of typeNode.types) expand(part);
    else if (ast.isTypeLiteralNode(typeNode)) members(vue, source, typeNode.members, interfaceName, result);
    else expand(typeNode);
    return result;
  }
  members(vue, source, declaration.members, interfaceName, result);
  for (const clause of declaration.heritageClauses ?? []) for (const type of clause.types) expand(type);
  return result;
}

function members(vue, source, list, from, result) {
  for (const member of list) {
    if (!member.name) continue;
    const name = member.name.getText(source).replace(/^['"]|['"]$/g, '');
    const text = member.getText(source);
    const doc = docOf(member, source, vue.ast);
    result.members.push({
      name,
      optional: /^(?:readonly\s+)?['"]?[\w-]+['"]?\?/.test(text),
      type: member.type ? compact(member.type.getText(source)) : compact(text.slice(member.name.getText(source).length)),
      doc: doc.text,
      default: doc.tags.default ?? '',
      from,
    });
  }
}

// ───────── 配方与样式表 ─────────

const isCall = (node, source, ast, suffix) => node && ast.isCallExpression(node) && node.expression.getText(source).endsWith(suffix);

/** 组件用到（useRecipe）或以组件名定义（name: '组件名'）的配方 */
function recipes(vue, candidates, name) {
  const { ast } = vue;
  const used = new Map();
  for (const file of candidates) {
    const source = vue.tree(file);
    for (const call of findAll(source, (node) => ast.isCallExpression(node) && node.expression.getText(source) === 'useRecipe')) {
      const id = call.arguments[0]?.getText(source);
      if (id) used.set(id, vue.imports(file).names.get(id) ?? file);
    }
    for (const declaration of findAll(source, (node) => ast.isVariableDeclaration(node) && isCall(node.initializer, source, ast, 'css.recipe'))) {
      const config = declaration.initializer.arguments[0];
      const recipeName = entriesOf(config, source, ast).find(([key]) => key === 'name')?.[1];
      if (recipeName && flatten(recipeName, source, ast) === name) used.set(declaration.name.getText(source), file);
    }
  }
  return [...used].map(([id, file]) => {
    const source = vue.tree(file);
    const init = initializerOf(source, id, ast);
    if (!isCall(init, source, ast, 'css.recipe')) return { id, file, error: '不是 css.recipe 的结果' };
    const config = Object.fromEntries(entriesOf(init.arguments[0], source, ast));
    const parts = (node) => entriesOf(node, source, ast).map(([part, styles]) => ({ part, styles: flatten(styles, source, ast) }));
    return {
      id,
      file,
      doc: docOf(find(source, (node) => ast.isVariableStatement(node) && node.getText(source).includes(`${id} =`)) ?? init, source, ast).text,
      name: config.name ? flatten(config.name, source, ast) : '',
      parts: config.parts ? flatten(config.parts, source, ast) : '',
      defaults: config.defaults ? flatten(config.defaults, source, ast) : '',
      base: parts(config.base),
      variants: entriesOf(config.variants, source, ast).flatMap(([dimension, values]) =>
        entriesOf(values, source, ast).map(([value, styles]) => ({ dimension, value, parts: parts(styles) })),
      ),
      compound: config.compound && ast.isArrayLiteralExpression(config.compound)
        ? config.compound.elements.map((element) => {
            const entries = entriesOf(element, source, ast);
            const when = entries.find(([key]) => key === 'when')?.[1];
            return {
              when: when ? entriesOf(when, source, ast).map(([key, value]) => `${key}=${flatten(value, source, ast)}`).join(', ') : flatten(element, source, ast),
              parts: entries.filter(([key]) => key !== 'when').map(([part, styles]) => ({ part, styles: flatten(styles, source, ast) })),
            };
          })
        : config.compound ? [{ when: '（不是数组字面量）', parts: [{ part: '', styles: flatten(config.compound, source, ast) }] }] : [],
    };
  });
}

/** 文件里的 `const 名字 = css.create({…})`：表名 → 键 → 样式 */
function styleTables(vue, file) {
  const { ast } = vue;
  const source = vue.tree(file);
  return findAll(source, (node) => ast.isVariableDeclaration(node) && isCall(node.initializer, source, ast, 'css.create')).map((declaration) => ({
    file,
    name: declaration.name.getText(source),
    entries: entriesOf(declaration.initializer.arguments[0], source, ast).map(([key, styles]) => ({ key, styles: flatten(styles, source, ast) })),
  }));
}

// ───────── 默认值 ─────────

/** `useDefaults('名字', () => ({ … }), 回退)` 与 config/defaults.ts 里 ComponentDefaults 的同名项 */
function defaults(vue, candidates) {
  const { ast } = vue;
  for (const file of candidates) {
    const source = vue.tree(file);
    const call = find(source, (node) => ast.isCallExpression(node) && node.expression.getText(source) === 'useDefaults');
    if (!call) continue;
    const [key, read, fallback] = call.arguments;
    const name = flatten(key, source, ast);
    const object = read && find(read, (node) => ast.isObjectLiteralExpression(node));
    const registry = vue.tree('config/defaults.ts');
    const entry = find(registry, (node) => ast.isPropertySignatureDeclaration(node) && node.name.getText(registry) === name && node.parent?.name?.getText(registry) === 'ComponentDefaults');
    return {
      file,
      key: name,
      reads: object ? object.properties.map((property) => property.name?.getText(source) ?? compact(property.getText(source))) : [],
      fallback: fallback ? flatten(fallback, source, ast) : '',
      registry: entry ? { type: compact(entry.type.getText(registry)), doc: docOf(entry, registry, ast).text } : undefined,
    };
  }
  return undefined;
}

// ───────── 输出 ─────────

function render(data) {
  let out = `# ${data.name}（${data.file}，L${data.level}，被 ${data.usedBy.length} 个文件直接用到；vue-tsx-pro ${data.commit}）\n\n`;
  if (data.fileDoc) out += `${data.fileDoc.split('\n').map((line) => `> ${line}`).join('\n')}\n\n`;
  if (data.doc && data.doc !== data.fileDoc) out += `导出的说明：${data.doc}\n\n`;

  const shared = data.propsInterface && data.propsInterface !== `${data.name}Props` ? `，取自 ${data.propsInterface}` : '';
  out += `## Props（${data.props.length}${shared}）\n\n`;
  out += table(['名字', '类型', '默认', '说明', '来自'], data.props.map((p) => [`${p.name}${p.optional ? '?' : ''}`, code(p.type), p.default, p.doc, p.from]));
  if (data.inherited.length) out += `\n不展开的继承：${[...new Set(data.inherited)].map((text) => code(text.length > 120 ? `${text.slice(0, 117)}…` : text)).join('、')}\n`;

  out += `\n## 部件属性（${data.parts.length}）\n\n`;
  out += table(['名字', '类型', '说明', '来自'], data.parts.map((p) => [p.name, code(p.type), p.doc, p.from]));

  for (const recipe of data.recipes) {
    out += `\n## 配方 ${recipe.id}（${recipe.file}）\n\n`;
    if (recipe.error) {
      out += `${recipe.error}\n`;
      continue;
    }
    if (recipe.doc) out += `${recipe.doc}\n\n`;
    out += `name=${recipe.name || '—'}；parts=${recipe.parts || '—'}；defaults=${recipe.defaults || '—'}\n\n`;
    out += `### base\n\n${table(['部件', '样式'], recipe.base.map((p) => [p.part, p.styles]))}`;
    const styled = recipe.variants.filter((v) => v.parts.length);
    const plain = Map.groupBy(recipe.variants.filter((v) => !v.parts.length), (v) => v.dimension);
    out += `\n### variants\n\n`;
    if (plain.size) out += `没有自己样式的取值：${[...plain].map(([dimension, values]) => `${dimension}=${values.map((v) => v.value).join('/')}`).join('；')}\n\n`;
    out += table(['档位', '取值', '部件', '样式'], styled.flatMap((v) => v.parts.map((p) => [v.dimension, v.value, p.part, p.styles])));
    out += `\n### compound（${recipe.compound.length}）\n\n`;
    out += table(['条件', '部件', '样式'], recipe.compound.flatMap((c) => c.parts.map((p) => [c.when, p.part, p.styles])));
  }

  if (data.styles.length) {
    out += `\n## 样式表（css.create）\n\n`;
    out += table(['表', '键', '样式'], data.styles.flatMap((t) => t.entries.map((e) => [`${t.name}（${label(t.file)}）`, e.key, e.styles])));
  }

  out += `\n## 默认值\n\n`;
  if (data.defaults) {
    const d = data.defaults;
    out += `\`useDefaults('${d.key}')\`（${label(d.file)}）：读 ${d.reads.map(code).join('、') || '—'}；回退 ${code(d.fallback)}\n\n`;
    out += d.registry ? `ComponentDefaults.${d.key}：${code(d.registry.type)}${d.registry.doc ? `（${d.registry.doc}）` : ''}\n` : `ComponentDefaults 里没有 ${d.key}\n`;
  } else out += '没有 useDefaults（没有外观档位的默认值）\n';

  out += `\n## 语言包\n\n${data.lang.length ? data.lang.map((key) => code(`lang.${key}`)).join('、') : '没有用到'}\n`;
  out += `\n## 依赖\n\n用到的组件文件：${data.usesComponents.map(code).join('、') || '—'}\n\n`;
  out += `用到的基础设施：${data.uses.filter((u) => !data.usesComponents.includes(u)).map(code).join('、') || '—'}\n\n`;
  out += `直接被用到：${data.usedBy.map(code).join('、') || '—'}\n`;
  return out;
}
