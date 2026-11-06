// lang：语言包的键、说明与中英文案（config/lang：Lang 接口的注释，zh-CN.ts、en-US.ts 的取值）
import { code, table } from '../lib/markdown.mjs';
import { compact, docOf, entriesOf, find, flatten } from '../lib/syntax.mjs';

const LANGS = { 'zh-CN': ['config/lang/zh-CN.ts', 'zhCN'], 'en-US': ['config/lang/en-US.ts', 'enUS'] };

/** Lang 接口的成员：键 → { type, doc } */
function langKeys(vue) {
  const { ast } = vue;
  const file = 'config/lang/lang.ts';
  const source = vue.tree(file);
  const declaration = find(source, (node) => ast.isInterfaceDeclaration(node) && node.name.getText(source) === 'Lang');
  return new Map(
    declaration.members
      .filter((member) => member.name)
      .map((member) => [member.name.getText(source), { type: member.type ? compact(member.type.getText(source)) : '', doc: docOf(member, source, ast).text }]),
  );
}

/** 一个语言包的取值：键 → 文案（嵌套对象如 errorCodes 展开成 `errorCodes.http.network`；函数照原文） */
function langValues(vue, [file, id]) {
  const { ast } = vue;
  const source = vue.tree(file);
  const declaration = find(source, (node) => ast.isVariableDeclaration(node) && node.name.getText(source) === id);
  const values = new Map();
  const walk = (object, prefix) => {
    for (const [key, value] of entriesOf(object, source, ast)) {
      if (ast.isObjectLiteralExpression(value) && prefix === '') walk(value, `${key}.`);
      else values.set(`${prefix}${key}`, flatten(value, source, ast));
    }
  };
  walk(declaration.initializer, '');
  return values;
}

export function lang(vue, prefix) {
  const keys = langKeys(vue);
  const packs = Object.fromEntries(Object.entries(LANGS).map(([name, entry]) => [name, langValues(vue, entry)]));
  const all = new Set([...keys.keys(), ...Object.values(packs).flatMap((values) => [...values.keys()])]);
  const rows = [...all]
    .filter((key) => !prefix || key.startsWith(prefix))
    .map((key) => {
      const top = keys.get(key.split('.')[0]);
      return {
        key,
        type: keys.get(key)?.type ?? '',
        doc: keys.get(key)?.doc ?? (top && key.includes('.') ? `（${key.split('.')[0]} 的一项）` : ''),
        values: Object.fromEntries(Object.entries(packs).map(([name, values]) => [name, values.get(key) ?? ''])),
      };
    });
  if (!rows.length) throw new Error(`语言包里没有以 ${prefix} 开头的键（组件文案以组件名开头，如 select、datePicker）`);
  const markdown = `# 语言包${prefix ? `：${prefix}…` : ''}（${rows.length} 个；vue-tsx-pro ${vue.commit}）\n\n${table(
    ['键', '类型', '说明', ...Object.keys(LANGS)],
    rows.map((r) => [r.key, code(r.type), r.doc, ...Object.keys(LANGS).map((name) => r.values[name])]),
  )}`;
  return { data: { commit: vue.commit, rows }, markdown };
}
