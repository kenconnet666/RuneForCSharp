#!/usr/bin/env node
// 从 vue-tsx-pro 文档站的组件页（apps/docs/src/components/<分类>/<组件>/docs.tsx）抽出页面与示例清单，
// 三个平台的文档应用共用（DOCSITE.md 第二节"内容从上游抽，不手抄"）。
// 用法：node tools/docs-gen/docs-gen.mjs [--check] [--vue <路径>]
import { readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { writeOutputs } from '../lib/output.mjs';
import { openVue } from '../lib/vue.mjs';

const OUT = 'docs/site/pages.g.json';
const SITE = 'apps/docs/src';

const { values: options } = parseArgs({
  options: {
    check: { type: 'boolean', default: false },
    vue: { type: 'string' },
  },
});

const vue = await openVue(options.vue);
try {
  const site = buildSite(vue);
  const content = `${JSON.stringify({ commit: vue.commit, ...site }, null, 2)}\n`;
  process.exitCode = writeOutputs([{ path: OUT, content }], {
    check: options.check,
    commit: vue.commit,
    command: 'node tools/docs-gen/docs-gen.mjs',
  });
} catch (error) {
  process.stderr.write(`${error.stack ?? error.message}\n`);
  process.exitCode = 1;
} finally {
  vue.close();
}

/** 分类（侧栏的组，顺序与组名取上游 site/nav.ts 的 CATEGORIES）与每类里的页面 */
function buildSite(vue) {
  const nav = vue.siteFile(`${SITE}/site/nav.ts`).text;
  const block = nav.match(/const CATEGORIES[^=]*=\s*\[([\s\S]*?)\n\];/)?.[1];
  if (!block) throw new Error('site/nav.ts 里找不到 CATEGORIES');
  const categories = [...block.matchAll(/\['([\w-]+)',\s*'([^']+)'\]/g)].map(([, id, label]) => ({ id, label }));
  const root = join(vue.root, SITE, 'components');
  return {
    categories: categories
      .map(({ id, label }) => {
        const dir = join(root, id);
        if (!existsSync(dir)) return { id, label, pages: [] };
        const pages = readdirSync(dir)
          .filter((name) => existsSync(join(dir, name, 'docs.tsx')))
          .map((name) => page(vue, id, name))
          // 同上游 nav.ts：按 order，没写的按名字排在后面
          .sort((a, b) => (a.order ?? Infinity) - (b.order ?? Infinity) || a.name.localeCompare(b.name));
        return { id, label, pages };
      })
      .filter((category) => category.pages.length),
  };
}

/** 一页：meta、示例（按列）、键盘表、API 表里列的组件 */
function page(vue, category, name) {
  const path = `${SITE}/components/${category}/${name}/docs.tsx`;
  const { tree } = vue.siteFile(path);
  const ast = vue.ast;
  const imports = importMap(tree, ast);
  const meta = metaOf(tree, ast, path);
  const result = {
    name,
    slug: kebab(name),
    title: meta.title,
    description: meta.description,
    ...(meta.order !== undefined ? { order: meta.order } : {}),
    wide: false,
    columns: [],
    keyboard: [],
    api: [],
  };
  const visit = (node, column) => {
    if (ast.isJsxElement(node) || ast.isJsxSelfClosingElement(node)) {
      const tag = tagName(node, ast);
      const attrs = attributes(node, ast);
      if (tag === 'DocPage') result.wide = attrs.has('wide');
      if (tag === 'ExampleColumn') {
        const next = [];
        result.columns.push(next);
        forEach(node, ast, (child) => visit(child, next));
        return;
      }
      if (tag === 'Example') {
        if (!column) {
          column = [];
          result.columns.push(column);
        }
        const demo = attrs.get('demo');
        const source = demo && ast.isIdentifier(demo) ? imports.get(demo.text) : undefined;
        column.push({
          id: stringOf(attrs.get('id'), ast, path),
          title: stringOf(attrs.get('title'), ast, path),
          demo: source?.match(/(?:demo|frames)\/(.+?)\.tsx\?demo$/)?.[1] ?? null,
          // 整页示例在 iframe 里，frame 是它的高度（40rem）
          ...(attrs.has('frame') ? { frame: attrs.get('frame') === true ? '' : stringOf(attrs.get('frame'), ast, path) } : {}),
          description: inline(ast.isJsxElement(node) ? node.children : [], ast),
        });
        return;
      }
      if (tag === 'KeyboardReference') {
        const keys = attrs.get('keys');
        if (keys && ast.isArrayLiteralExpression(keys))
          for (const row of keys.elements) {
            if (!ast.isArrayLiteralExpression(row)) continue;
            const [combo, action] = row.elements;
            result.keyboard.push({ keys: stringOf(combo, ast, path), action: action && (isString(ast, action) ? [{ text: action.text }] : inline([action], ast)) });
          }
        return;
      }
      if (tag === 'ApiReference') {
        const api = attrs.get('api');
        const names = api && ast.isArrayLiteralExpression(api) ? api.elements : api ? [api] : [];
        for (const item of names) {
          const from = ast.isIdentifier(item) ? imports.get(item.text) : undefined;
          const props = from?.match(/^virtual:api\/(\w+)$/)?.[1];
          if (props) result.api.push(props.replace(/Props$/, ''));
        }
        return;
      }
    }
    forEach(node, ast, (child) => visit(child, column));
  };
  visit(tree, undefined);
  return result;
}

/** 默认导入：名字 → 模块说明符 */
function importMap(tree, ast) {
  const map = new Map();
  for (const statement of tree.statements) {
    if (!ast.isImportDeclaration(statement) || !statement.importClause?.name) continue;
    map.set(statement.importClause.name.text, statement.moduleSpecifier.text);
  }
  return map;
}

/** `export const meta = { title, description, order }` */
function metaOf(tree, ast, path) {
  for (const statement of tree.statements) {
    if (!ast.isVariableStatement(statement)) continue;
    for (const declaration of statement.declarationList.declarations) {
      if (declaration.name.text !== 'meta' || !declaration.initializer || !ast.isObjectLiteralExpression(declaration.initializer)) continue;
      const meta = {};
      for (const property of declaration.initializer.properties) {
        if (!ast.isPropertyAssignment(property)) continue;
        const key = property.name.text;
        const value = property.initializer;
        if (ast.isNumericLiteral(value)) meta[key] = Number(value.text);
        else meta[key] = stringOf(value, ast, path);
      }
      return meta;
    }
  }
  throw new Error(`${path}：没有 export const meta`);
}

/** 字符串字面量、`{'…'}`、模板字面量（无插值）、相加的字面量 */
function stringOf(node, ast, path) {
  if (!node) return '';
  if (ast.isJsxExpression(node)) return stringOf(node.expression, ast, path);
  if (isString(ast, node)) return node.text;
  if (ast.isParenthesizedExpression(node)) return stringOf(node.expression, ast, path);
  if (ast.isBinaryExpression(node) && node.operatorToken.kind === ast.SyntaxKind.PlusToken)
    return stringOf(node.left, ast, path) + stringOf(node.right, ast, path);
  throw new Error(`${path}：认不出的字符串写法（${ast.SyntaxKind[node.kind]}）`);
}

function tagName(node, ast) {
  const tag = ast.isJsxElement(node) ? node.openingElement.tagName : node.tagName;
  return tag.text;
}

/** JSX 属性：名字 → 初始化表达式（布尔属性的值是 true） */
function attributes(node, ast) {
  const opening = ast.isJsxElement(node) ? node.openingElement : node;
  const map = new Map();
  for (const attribute of opening.attributes.properties) {
    if (!ast.isJsxAttribute(attribute)) continue;
    const init = attribute.initializer;
    map.set(attribute.name.text, init ? (ast.isJsxExpression(init) ? init.expression : init) : true);
  }
  return map;
}

function forEach(node, ast, callback) {
  node.forEachChild(callback);
}

function isString(ast, node) {
  return ast.isStringLiteral(node) || ast.isNoSubstitutionTemplateLiteral(node);
}

/**
 * 示例说明这类行内内容 → 片段：`{ text }`、`{ code }`、`{ kbd }`、`{ link, href }`。
 * 文字的空白按 JSX 的规则处理（同 Babel 的 cleanJSXElementLiteralChild）：行首行尾的空白与空行去掉，跨行处并成一个空格
 */
function inline(children, ast) {
  const runs = [];
  const push = (run) => {
    const last = runs.at(-1);
    if (run.text !== undefined && last?.text !== undefined) last.text += run.text;
    else if (run.text !== '') runs.push(run);
  };
  for (const child of children) {
    if (ast.isJsxText(child)) {
      const text = cleanJsxText(child.text);
      if (text) push({ text });
    } else if (ast.isJsxExpression(child)) {
      if (!child.expression) continue;
      if (isString(ast, child.expression)) push({ text: child.expression.text });
      else throw new Error('说明里认不出的表达式：' + ast.SyntaxKind[child.expression.kind]);
    } else if (ast.isJsxElement(child) || ast.isJsxSelfClosingElement(child)) {
      const tag = tagName(child, ast);
      const text = ast.isJsxElement(child) ? inline(child.children, ast).map((run) => run.text ?? run.code ?? run.kbd ?? run.link ?? '').join('') : '';
      if (tag === 'Code') push({ code: text });
      else if (tag === 'Kbd') push({ kbd: text });
      else if (tag === 'Link') {
        const href = attributes(child, ast).get('href');
        push({ link: text, href: href && isString(ast, href) ? href.text : '' });
      } else push({ text });
    }
  }
  return runs;
}

function cleanJsxText(value) {
  const lines = value.split(/\r\n|\n|\r/);
  let last = 0;
  lines.forEach((line, index) => {
    if (/[^ \t]/.test(line)) last = index;
  });
  let out = '';
  lines.forEach((line, index) => {
    let trimmed = line.replace(/\t/g, ' ');
    if (index !== 0) trimmed = trimmed.replace(/^ +/, '');
    if (index !== lines.length - 1) trimmed = trimmed.replace(/ +$/, '');
    if (trimmed) {
      if (index !== last) trimmed += ' ';
      out += trimmed;
    }
  });
  return out;
}

/** Button → button，IconButton → icon-button（同上游 nav.ts 的 kebab） */
function kebab(name) {
  return name.replace(/(?<=[a-z0-9])([A-Z])/g, '-$1').toLowerCase();
}
