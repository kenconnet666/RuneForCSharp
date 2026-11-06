// vue-tsx-pro 的 token 与内置主题（config/theme），读法同 vue-tsx-pro 的 scripts/themes/check.mjs：
// tokens.ts 的各组默认值即亮色主题；每套主题文件用 css.createTheme(组, {…}) 覆盖其中几组，并写明 colorScheme
import { docOf, entriesOf, findAll, flatten } from './syntax.mjs';

const THEME_DIR = 'config/theme';

const isCallTo = (node, source, ast, suffix) =>
  ast.isCallExpression(node) && node.expression.getText(source).endsWith(suffix);

/** tokens.ts 里每组 `const 组 = css.defineVars('vtp-…', {…})`：组 → { prefix, doc, tokens: 键 → { value, doc } } */
export function tokenGroups(vue) {
  if (vue.cache.has('tokenGroups')) return vue.cache.get('tokenGroups');
  const { ast } = vue;
  const source = vue.tree(`${THEME_DIR}/tokens.ts`);
  const groups = new Map();
  for (const declaration of findAll(source, (node) => ast.isVariableDeclaration(node) && node.initializer && isCallTo(node.initializer, source, ast, 'defineVars'))) {
    const [prefix, object] = declaration.initializer.arguments;
    const statement = declaration.parent?.parent;
    const tokens = new Map();
    for (const property of object.properties) {
      if (!ast.isPropertyAssignment(property)) continue;
      tokens.set(property.name.getText(source), { value: flatten(property.initializer, source, ast), doc: docOf(property, source, ast).text });
    }
    groups.set(declaration.name.getText(source), { prefix: flatten(prefix, source, ast), doc: statement ? docOf(statement, source, ast).text : '', tokens });
  }
  vue.cache.set('tokenGroups', groups);
  return groups;
}

/**
 * 内置主题，按 builtin.ts 的顺序：`{ name, id, file, colorScheme, groups }`，groups 是 组 → 键 → 值（只含这套主题覆盖的）。
 * 亮色主题的文件 light.ts 把默认值再写了一遍，这里直接用 tokens.ts 的默认值，groups 为空
 */
export function builtinThemes(vue) {
  if (vue.cache.has('builtinThemes')) return vue.cache.get('builtinThemes');
  const { ast } = vue;
  const text = vue.text(`${THEME_DIR}/builtin.ts`);
  const files = Object.fromEntries([...text.matchAll(/^import \{ (\w+) \} from '\.\/([\w-]+)';$/gm)].map(([, id, file]) => [id, `${THEME_DIR}/${file}.ts`]));
  const body = text.match(/export const builtinThemes = \{([\s\S]*?)\} as const/)?.[1];
  if (!body) throw new Error('builtin.ts 里找不到 builtinThemes');
  const themes = [...body.matchAll(/^\s*'?([\w-]+)'?: (\w+),$/gm)].map(([, name, id]) => {
    const file = files[id];
    if (!file) throw new Error(`builtin.ts 的 ${name}（${id}）找不到对应的导入`);
    const colorScheme = vue.text(file).match(/colorScheme: '(light|dark)'/)?.[1];
    if (!colorScheme) throw new Error(`${file} 里找不到 colorScheme`);
    const groups = new Map();
    if (name !== 'light') {
      const source = vue.tree(file);
      for (const call of findAll(source, (node) => isCallTo(node, source, ast, 'createTheme'))) {
        const [group, object] = call.arguments;
        groups.set(group.getText(source), new Map(entriesOf(object, source, ast).map(([key, value]) => [key, flatten(value, source, ast)])));
      }
    }
    return { name, id, file, colorScheme, groups };
  });
  vue.cache.set('builtinThemes', themes);
  return themes;
}

/** 一套主题里某个 token 的取值：主题覆盖了就用覆盖的，否则用默认值 */
export function themeValue(vue, theme, group, key) {
  return theme.groups.get(group)?.get(key) ?? tokenGroups(vue).get(group)?.tokens.get(key)?.value;
}

/** 被主题覆盖的组（至少一套主题写了 createTheme） */
export function themedGroups(vue) {
  return new Set(builtinThemes(vue).flatMap((theme) => [...theme.groups.keys()]));
}
