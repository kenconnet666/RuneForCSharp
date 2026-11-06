// token：主题 token 的说明与在 16 套内置主题里的取值（读法同 vue-tsx-pro 的 scripts/themes/check.mjs）
import { code, table } from '../lib/markdown.mjs';
import { docOf, entriesOf, findAll, flatten } from '../lib/syntax.mjs';

const THEME_DIR = 'config/theme';

/** tokens.ts 里每组 `const 组 = css.defineVars('vtp-…', {…})`：组 → { prefix, doc, tokens: 键 → { value, doc } } */
export function tokenGroups(vue) {
  if (vue.tokenGroups) return vue.tokenGroups;
  const { ast } = vue;
  const file = `${THEME_DIR}/tokens.ts`;
  const source = vue.tree(file);
  const groups = new Map();
  for (const declaration of findAll(source, (node) => ast.isVariableDeclaration(node) && node.initializer && ast.isCallExpression(node.initializer) && node.initializer.expression.getText(source).endsWith('defineVars'))) {
    const [prefix, object] = declaration.initializer.arguments;
    const statement = declaration.parent?.parent;
    const tokens = new Map();
    for (const property of object.properties) {
      if (!ast.isPropertyAssignment(property)) continue;
      tokens.set(property.name.getText(source), { value: flatten(property.initializer, source, ast), doc: docOf(property, source, ast).text });
    }
    groups.set(declaration.name.getText(source), { prefix: flatten(prefix, source, ast), doc: statement ? docOf(statement, source, ast).text : '', tokens });
  }
  return (vue.tokenGroups = groups);
}

/** 内置主题名 → 主题文件（builtin.ts 的导入与 builtinThemes；亮色就是 tokens.ts 的默认值） */
function themeFiles(vue) {
  const text = vue.text(`${THEME_DIR}/builtin.ts`);
  const files = Object.fromEntries([...text.matchAll(/^import \{ (\w+) \} from '\.\/([\w-]+)';$/gm)].map(([, id, file]) => [id, `${THEME_DIR}/${file}.ts`]));
  const body = text.match(/export const builtinThemes = \{([\s\S]*?)\} as const/)?.[1];
  if (!body) throw new Error('builtin.ts 里找不到 builtinThemes');
  return [...body.matchAll(/^\s*'?([\w-]+)'?: (\w+),$/gm)].map(([, name, id]) => ({ name, file: name === 'light' ? undefined : files[id] }));
}

/** 每套主题对各组的覆盖：主题名 → 组 → 键 → 值（主题文件里 `css.createTheme(组, {…})`） */
export function themeOverrides(vue) {
  if (vue.themeOverrides) return vue.themeOverrides;
  const { ast } = vue;
  const result = new Map();
  for (const { name, file } of themeFiles(vue)) {
    const groups = new Map();
    if (file) {
      const source = vue.tree(file);
      for (const call of findAll(source, (node) => ast.isCallExpression(node) && node.expression.getText(source).endsWith('createTheme'))) {
        const [group, object] = call.arguments;
        groups.set(group.getText(source), new Map(entriesOf(object, source, ast).map(([key, value]) => [key, flatten(value, source, ast)])));
      }
    }
    result.set(name, groups);
  }
  return (vue.themeOverrides = result);
}

export function token(vue, name, options) {
  const groups = tokenGroups(vue);
  if (options.group) return groupTable(vue, options.group, options.themes);
  if (!name) {
    const rows = [...groups].map(([group, g]) => ({ group, prefix: g.prefix, count: g.tokens.size, doc: g.doc.split('\n')[0] }));
    return {
      data: { commit: vue.commit, groups: rows },
      markdown: `# token 的组（vue-tsx-pro ${vue.commit}，config/theme/tokens.ts）\n\n${table(['组', '变量前缀', '个数', '说明'], rows.map((r) => [r.group, r.prefix, r.count, r.doc]))}`,
    };
  }
  const wanted = name.replace(/^_/, '');
  const [groupName, key] = wanted.includes('.') ? wanted.split('.') : [undefined, wanted];
  const matches = [...groups].filter(([group, g]) => (!groupName || group === groupName) && g.tokens.has(key));
  if (!matches.length) throw new Error(`找不到 token ${name}（短写去掉下划线；带组名写 colors.accent；不带参数列出所有组）`);
  const overrides = themeOverrides(vue);
  const data = matches.map(([group, g]) => {
    const { value, doc } = g.tokens.get(key);
    const themes = [...overrides].map(([theme, byGroup]) => {
      const override = byGroup.get(group)?.get(key);
      return { theme, value: override ?? value, overridden: override !== undefined && override !== value };
    });
    return { group, key, doc, default: value, themes };
  });
  let markdown = `# token ${name}（vue-tsx-pro ${vue.commit}）\n`;
  for (const match of data) {
    markdown += `\n## ${match.group}.${match.key}\n\n${match.doc || '（没有注释）'}\n\n默认（亮色）：${code(match.default)}\n\n`;
    markdown += table(['主题', '取值', '与默认不同'], match.themes.map((t) => [t.theme, code(t.value), t.overridden ? '是' : '']));
  }
  return { data: { commit: vue.commit, matches: data }, markdown };
}

function groupTable(vue, groupName, themes) {
  const group = tokenGroups(vue).get(groupName);
  if (!group) throw new Error(`没有 token 组 ${groupName}；有：${[...tokenGroups(vue).keys()].join('、')}`);
  const overrides = themeOverrides(vue);
  const columns = themes ? themes.split(',').map((t) => t.trim()) : [];
  for (const theme of columns) if (!overrides.has(theme)) throw new Error(`没有主题 ${theme}；有：${[...overrides.keys()].join('、')}`);
  const rows = [...group.tokens].map(([key, { value, doc }]) => ({
    key,
    default: value,
    themes: Object.fromEntries(columns.map((theme) => [theme, overrides.get(theme).get(groupName)?.get(key) ?? value])),
    doc,
  }));
  const markdown = `# token 组 ${groupName}（${group.prefix}，${rows.length} 个；vue-tsx-pro ${vue.commit}）\n\n${group.doc ? `${group.doc}\n\n` : ''}${table(
    ['token', '默认（亮色）', ...columns, '说明'],
    rows.map((r) => [r.key, code(r.default), ...columns.map((theme) => code(r.themes[theme])), r.doc]),
  )}`;
  return { data: { commit: vue.commit, group: groupName, prefix: group.prefix, rows }, markdown };
}
