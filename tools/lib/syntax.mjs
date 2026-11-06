// 语法树上的小工具：取 JSDoc 的文字与标签、把样式对象压成一行、找声明
/** 节点前面的 JSDoc：`{ text, tags }`，tags 是 `@标签` → 文字（如 `default`）；没有时 text 为空 */
export function docOf(node, source, ast) {
  const docs = ast.getJSDocCommentsAndTags(node);
  if (!docs.length) return { text: '', tags: {} };
  return parseDoc(docs.at(-1).getText(source));
}

/** 解析一段 `/** … *\/` 原文 */
export function parseDoc(raw) {
  const lines = raw
    .replace(/^\/\*\*/, '')
    .replace(/\*\/$/, '')
    .split(/\r?\n/)
    .map((line) => line.replace(/^\s*\* ?/, '').trimEnd());
  const tags = {};
  const text = [];
  let current;
  for (const line of lines) {
    const tag = line.match(/^@(\w+)\s*(.*)$/);
    if (tag) {
      current = tag[1];
      tags[current] = tag[2];
    } else if (current) tags[current] = `${tags[current]} ${line.trim()}`.trim();
    else text.push(line);
  }
  return { text: text.join('\n').trim(), tags };
}

/** 文件开头的说明（第一段 `/** … *\/`，在任何代码之前） */
export function fileDoc(text) {
  const match = text.match(/^\s*(\/\*\*[\s\S]*?\*\/)/);
  return match ? parseDoc(match[1]).text : '';
}

/** 属性名：字符串字面量去掉引号，计算属性名保留方括号 */
export function keyOf(property, source, ast) {
  const name = property.name;
  if (ast.isStringLiteral(name) || ast.isNoSubstitutionTemplateLiteral(name)) return name.text;
  return name.getText(source);
}

/**
 * 把表达式压成一行：对象写成 `{ 键=值; … }`，字符串去掉引号，其余照原文（空白合并）。
 * 样式对象这样读得最快：`{ color=_accentContrast; backgroundColor={ default=_accent; [HOVER]=_accentHover } }`
 */
export function flatten(node, source, ast) {
  if (!node) return '';
  if (ast.isStringLiteral(node) || ast.isNoSubstitutionTemplateLiteral(node)) return node.text;
  if (ast.isObjectLiteralExpression(node)) {
    if (!node.properties.length) return '{}';
    const parts = node.properties.map((property) => {
      if (ast.isPropertyAssignment(property)) return `${keyOf(property, source, ast)}=${flatten(property.initializer, source, ast)}`;
      return compact(property.getText(source));
    });
    return `{ ${parts.join('; ')} }`;
  }
  if (ast.isArrayLiteralExpression(node)) return `[${node.elements.map((element) => flatten(element, source, ast)).join(', ')}]`;
  return compact(node.getText(source));
}

/** 对象字面量的属性：键 → 值节点（展开与简写跳过） */
export function entriesOf(node, source, ast) {
  if (!node || !ast.isObjectLiteralExpression(node)) return [];
  return node.properties.filter((property) => ast.isPropertyAssignment(property)).map((property) => [keyOf(property, source, ast), property.initializer]);
}

/** 合并空白 */
export const compact = (text) => text.replace(/\s+/g, ' ').trim();

/** 深度优先找第一个满足条件的节点 */
export function find(node, predicate) {
  let found;
  const visit = (child) => {
    if (found) return;
    if (predicate(child)) found = child;
    else child.forEachChild(visit);
  };
  node.forEachChild(visit);
  return found;
}

/** 深度优先找全部满足条件的节点 */
export function findAll(node, predicate) {
  const found = [];
  const visit = (child) => {
    if (predicate(child)) found.push(child);
    child.forEachChild(visit);
  };
  node.forEachChild(visit);
  return found;
}

/** 变量声明的初始值：`const 名字 = …` */
export function initializerOf(source, name, ast) {
  return find(source, (node) => ast.isVariableDeclaration(node) && node.name.getText(source) === name)?.initializer;
}
