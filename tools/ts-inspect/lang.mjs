// lang：语言包的键、说明与中英文案（config/lang：Lang 接口的注释，zh-CN.ts、en-US.ts 的取值）
import { LANG_PACKS, langMembers, langValues } from '../lib/lang.mjs';
import { code, table } from '../lib/markdown.mjs';
import { entriesOf, flatten } from '../lib/syntax.mjs';

/** 一个语言包压成一行一条：嵌套的表（errorCodes……）展开成 `errorCodes.http.network`，函数照原文 */
function flatValues(vue, pack) {
  const { ast } = vue;
  const values = new Map();
  for (const [key, { node, source }] of langValues(vue, pack)) {
    if (ast.isObjectLiteralExpression(node)) for (const [inner, value] of entriesOf(node, source, ast)) values.set(`${key}.${inner}`, flatten(value, source, ast));
    else values.set(key, flatten(node, source, ast));
  }
  return values;
}

export function lang(vue, prefix) {
  const members = new Map(langMembers(vue).map((member) => [member.name, member]));
  const packs = Object.fromEntries(LANG_PACKS.map((pack) => [pack.code, flatValues(vue, pack)]));
  const all = new Set([...members.keys(), ...Object.values(packs).flatMap((values) => [...values.keys()])]);
  const rows = [...all]
    .filter((key) => !prefix || key.startsWith(prefix))
    .map((key) => {
      const [top] = key.split('.');
      return {
        key,
        type: members.get(key)?.typeText ?? '',
        doc: members.get(key)?.doc ?? (members.has(top) && key.includes('.') ? `（${top} 的一项）` : ''),
        values: Object.fromEntries(Object.entries(packs).map(([name, values]) => [name, values.get(key) ?? ''])),
      };
    });
  if (!rows.length) throw new Error(`语言包里没有以 ${prefix} 开头的键（组件文案以组件名开头，如 select、datePicker）`);
  const codes = LANG_PACKS.map((pack) => pack.code);
  const markdown = `# 语言包${prefix ? `：${prefix}…` : ''}（${rows.length} 个；vue-tsx-pro ${vue.commit}）\n\n${table(
    ['键', '类型', '说明', ...codes],
    rows.map((r) => [r.key, code(r.type), r.doc, ...codes.map((name) => r.values[name])]),
  )}`;
  return { data: { commit: vue.commit, rows }, markdown };
}
