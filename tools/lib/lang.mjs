// vue-tsx-pro 的语言包（config/lang）：lang.ts 的 Lang 接口给出键的顺序、类型与说明，zh-CN.ts、en-US.ts 给出取值
import { compact, docOf, entriesOf, find } from './syntax.mjs';

/** 内置语言：文件、导出名、BCP 47 代码 */
export const LANG_PACKS = [
  { file: 'config/lang/zh-CN.ts', id: 'zhCN', code: 'zh-CN' },
  { file: 'config/lang/en-US.ts', id: 'enUS', code: 'en-US' },
];

/** Lang 接口的成员（按声明顺序）：`{ name, optional, type（类型节点）, typeText, doc, source }` */
export function langMembers(vue) {
  return interfaceMembers(vue, 'config/lang/lang.ts', 'Lang');
}

/** 一个接口的成员（按声明顺序），格式同 langMembers：语言包与页面组合的文案表（patterns/messages.ts）共用 */
export function interfaceMembers(vue, file, name) {
  const key = `interfaceMembers:${file}:${name}`;
  if (vue.cache.has(key)) return vue.cache.get(key);
  const { ast } = vue;
  const source = vue.tree(file);
  const declaration = find(source, (node) => ast.isInterfaceDeclaration(node) && node.name.getText(source) === name);
  if (!declaration) throw new Error(`${file} 里找不到 interface ${name}`);
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
  vue.cache.set(key, members);
  return members;
}

/** 文案表（`const 表: MessageTable<…> = { 'zh-CN': {…}, 'en-US': {…} }`）里一种语言的取值：键 → `{ node, source }` */
export function messageValues(vue, file, table, code) {
  const key = `messageValues:${file}:${table}:${code}`;
  if (vue.cache.has(key)) return vue.cache.get(key);
  const { ast } = vue;
  const source = vue.tree(file);
  const declaration = find(source, (node) => ast.isVariableDeclaration(node) && node.name.getText(source) === table);
  if (!declaration) throw new Error(`${file} 里找不到 ${table}`);
  const pack = entriesOf(declaration.initializer, source, ast).find(([name]) => name === code)?.[1];
  if (!pack) throw new Error(`${file} 的 ${table} 里没有 ${code}`);
  const values = new Map(entriesOf(pack, source, ast).map(([name, node]) => [name, { node, source }]));
  vue.cache.set(key, values);
  return values;
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
