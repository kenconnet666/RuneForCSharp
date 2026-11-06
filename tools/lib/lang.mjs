// vue-tsx-pro 的语言包（config/lang）：lang.ts 的 Lang 接口给出键的顺序、类型与说明，zh-CN.ts、en-US.ts 给出取值
import { compact, docOf, entriesOf, find } from './syntax.mjs';

/** 内置语言：文件、导出名、BCP 47 代码 */
export const LANG_PACKS = [
  { file: 'config/lang/zh-CN.ts', id: 'zhCN', code: 'zh-CN' },
  { file: 'config/lang/en-US.ts', id: 'enUS', code: 'en-US' },
];

/** Lang 接口的成员（按声明顺序）：`{ name, optional, type（类型节点）, typeText, doc, source }` */
export function langMembers(vue) {
  if (vue.cache.has('langMembers')) return vue.cache.get('langMembers');
  const { ast } = vue;
  const source = vue.tree('config/lang/lang.ts');
  const declaration = find(source, (node) => ast.isInterfaceDeclaration(node) && node.name.getText(source) === 'Lang');
  if (!declaration) throw new Error('config/lang/lang.ts 里找不到 interface Lang');
  const members = declaration.members
    .filter((member) => member.name)
    .map((member) => ({
      name: member.name.getText(source),
      optional: /^(?:readonly\s+)?\w+\?/.test(member.getText(source)),
      type: member.type,
      typeText: member.type ? compact(member.type.getText(source)) : '',
      doc: docOf(member, source, ast).text,
      source,
    }));
  vue.cache.set('langMembers', members);
  return members;
}

/** 一个语言包的取值：键 → `{ node（值的语法树）, source }` */
export function langValues(vue, pack) {
  const key = `langValues:${pack.id}`;
  if (vue.cache.has(key)) return vue.cache.get(key);
  const { ast } = vue;
  const source = vue.tree(pack.file);
  const declaration = find(source, (node) => ast.isVariableDeclaration(node) && node.name.getText(source) === pack.id);
  if (!declaration) throw new Error(`${pack.file} 里找不到 ${pack.id}`);
  const values = new Map(entriesOf(declaration.initializer, source, ast).map(([name, node]) => [name, { node, source }]));
  vue.cache.set(key, values);
  return values;
}
