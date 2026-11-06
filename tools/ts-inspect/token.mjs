// token：主题 token 的说明与在 16 套内置主题里的取值
import { code, table } from '../lib/markdown.mjs';
import { builtinThemes, themeValue, tokenGroups } from '../lib/theme.mjs';

/** 主题名 → 主题（builtin.ts 的顺序） */
const themesByName = (vue) => new Map(builtinThemes(vue).map((theme) => [theme.name, theme]));

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
  const data = matches.map(([group, g]) => {
    const { value, doc } = g.tokens.get(key);
    const themes = builtinThemes(vue).map((theme) => {
      const themed = themeValue(vue, theme, group, key);
      return { theme: theme.name, colorScheme: theme.colorScheme, value: themed, overridden: themed !== value };
    });
    return { group, key, doc, default: value, themes };
  });
  let markdown = `# token ${name}（vue-tsx-pro ${vue.commit}）\n`;
  for (const match of data) {
    markdown += `\n## ${match.group}.${match.key}\n\n${match.doc || '（没有注释）'}\n\n默认（亮色）：${code(match.default)}\n\n`;
    markdown += table(['主题', '亮暗', '取值', '与默认不同'], match.themes.map((t) => [t.theme, t.colorScheme, code(t.value), t.overridden ? '是' : '']));
  }
  return { data: { commit: vue.commit, matches: data }, markdown };
}

function groupTable(vue, groupName, themes) {
  const group = tokenGroups(vue).get(groupName);
  if (!group) throw new Error(`没有 token 组 ${groupName}；有：${[...tokenGroups(vue).keys()].join('、')}`);
  const byName = themesByName(vue);
  const columns = themes ? themes.split(',').map((t) => t.trim()) : [];
  for (const theme of columns) if (!byName.has(theme)) throw new Error(`没有主题 ${theme}；有：${[...byName.keys()].join('、')}`);
  const rows = [...group.tokens].map(([key, { value, doc }]) => ({
    key,
    default: value,
    themes: Object.fromEntries(columns.map((theme) => [theme, themeValue(vue, byName.get(theme), groupName, key)])),
    doc,
  }));
  const markdown = `# token 组 ${groupName}（${group.prefix}，${rows.length} 个；vue-tsx-pro ${vue.commit}）\n\n${group.doc ? `${group.doc}\n\n` : ''}${table(
    ['token', '默认（亮色）', ...columns, '说明'],
    rows.map((r) => [r.key, code(r.default), ...columns.map((theme) => code(r.themes[theme])), r.doc]),
  )}`;
  return { data: { commit: vue.commit, group: groupName, prefix: group.prefix, rows }, markdown };
}
