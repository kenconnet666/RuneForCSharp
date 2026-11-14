// 读 vue-tsx-pro 的工具共用：找到仓库、列出组件库源码、用它自带的 TypeScript 7 解析语法树、算导入关系与依赖分层
import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, relative, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

/** 本仓库根目录 */
export const repoRoot = resolve(import.meta.dirname, '../..');

/**
 * 生成文件对齐的 vue-tsx-pro 提交（tools/vue-tsx-pro.ref，CI 检出它）。上游推送不会让 CI 失败；
 * 跟进上游新版本是一次专门的提交：改这个文件、重新生成、按上游的变化清单改组件
 */
export const pinnedCommit = readFileSync(join(repoRoot, 'tools/vue-tsx-pro.ref'), 'utf8').trim();

/** 基础设施目录：组件只用到它们时是 L0 */
const INFRA = new Set(['utils', 'config']);

/** 子路径的扩展（不在主入口）与页面组合：分层时单独列出 */
export const EXTENSIONS = new Set([
  'charts', 'code-editor', 'diagram', 'docs-kit', 'maps', 'patterns', 'theme-customize', 'three', 'xlsx',
]);

/**
 * 打开 vue-tsx-pro：路径依次取参数、环境变量 VUE_TSX_PRO、与本仓库同级的 IdeaProjects/vue-tsx-pro。
 * 返回的对象缓存源码文本与语法树，用完调 close() 结束 TypeScript 的子进程。
 */
export async function openVue(path) {
  const root = resolve(path ?? process.env.VUE_TSX_PRO ?? join(repoRoot, '../../IdeaProjects/vue-tsx-pro'));
  const src = join(root, 'packages/ui/src');
  if (!existsSync(src)) throw new Error(`找不到 vue-tsx-pro 的组件库源码：${src}（用 --vue 或环境变量 VUE_TSX_PRO 指定仓库）`);
  const require = createRequire(join(root, 'package.json'));
  const load = (specifier) => import(pathToFileURL(require.resolve(specifier)).href);
  const [{ API }, ast] = await Promise.all([load('typescript/sync'), load('typescript/ast')]);
  return new Vue(root, src, API, ast);
}

class Vue {
  #api;
  #texts = new Map();
  #trees = new Map();
  #files;
  #graph;

  /** 各工具按名字缓存的解析结果（token 组、主题、声明表……） */
  cache = new Map();

  constructor(root, src, API, ast) {
    this.root = root;
    this.src = src;
    this.ast = ast;
    this.#api = new API();
    this.commit = execFileSync('git', ['-C', root, 'rev-parse', '--short', 'HEAD'], { encoding: 'utf8' }).trim();
    if (!pinnedCommit.startsWith(this.commit))
      process.stderr.write(`注意：读的是 vue-tsx-pro ${this.commit}，锁定的是 ${pinnedCommit.slice(0, 8)}（tools/vue-tsx-pro.ref）；跟进新版本时改锁定的提交\n`);
  }

  /** 组件库源码（相对 packages/ui/src，斜杠分隔），不含测试、声明文件与生成的入口 */
  get files() {
    if (this.#files) return this.#files;
    const files = [];
    const walk = (dir) => {
      for (const name of readdirSync(dir)) {
        const path = join(dir, name);
        if (name === '.test' || name === 'node_modules') continue;
        if (statSync(path).isDirectory()) walk(path);
        else if (/\.tsx?$/.test(name) && !name.endsWith('.d.ts') && name !== 'index.ts') files.push(this.relative(path));
      }
    };
    walk(this.src);
    return (this.#files = files.sort());
  }

  relative(path) {
    return relative(this.src, path).replaceAll('\\', '/');
  }

  text(file) {
    if (!this.#texts.has(file)) this.#texts.set(file, readFileSync(join(this.src, file), 'utf8'));
    return this.#texts.get(file);
  }

  /** 语法树（节点带父节点）。解析在 TypeScript 7 的 tsc 子进程里做，传回本地后立即释放远端的租约（同 vue-tsx-pro 的 scripts/lib/repo.mjs） */
  tree(file) {
    if (!this.#trees.has(file)) {
      const retained = this.#api.createSourceFile(join(this.src, file), this.text(file));
      retained.dispose();
      this.#trees.set(file, retained.sourceFile);
    }
    return this.#trees.get(file);
  }

  /** 组件库以外的文件（文档站的页面）的原文与语法树：路径相对 vue-tsx-pro 仓库根 */
  siteFile(path) {
    const key = `site:${path}`;
    if (!this.#trees.has(key)) {
      const text = readFileSync(join(this.root, path), 'utf8');
      const retained = this.#api.createSourceFile(join(this.root, path), text);
      retained.dispose();
      this.#trees.set(key, { text, tree: retained.sourceFile });
    }
    return this.#trees.get(key);
  }

  /** 相对导入解析成源码文件；不是组件库里的文件返回 undefined */
  resolveImport(from, specifier) {
    if (!specifier.startsWith('.')) return undefined;
    const base = this.relative(resolve(dirname(join(this.src, from)), specifier));
    return [`${base}.ts`, `${base}.tsx`, `${base}/index.ts`, base].find((file) => this.files.includes(file));
  }

  /**
   * 文件的值导入（`import type` 与花括号里的 `type X` 不算：它们不形成运行时依赖）。
   * `names` 是导入的名字 → 文件，`files` 是用到的文件（含只为副作用的导入）
   */
  imports(file) {
    const names = new Map();
    const files = new Set();
    const text = this.text(file);
    for (const [, clause, specifier] of text.matchAll(/^\s*(?:import|export)\s+(?!type\b)([^;]*?)\s+from\s+['"]([^'"]+)['"]/gms)) {
      const target = this.resolveImport(file, specifier);
      if (!target) continue;
      const braced = clause.match(/\{([^}]*)\}/)?.[1].split(',') ?? [];
      const values = braced.map((part) => part.trim()).filter((part) => part && !part.startsWith('type '));
      if (braced.length && !values.length && !/^\s*\w+\s*,/.test(clause)) continue;
      files.add(target);
      for (const part of values) names.set(part.split(/\s+as\s+/).pop(), target);
    }
    for (const [, specifier] of text.matchAll(/^\s*import\s+['"]([^'"]+)['"]/gm)) {
      const target = this.resolveImport(file, specifier);
      if (target) files.add(target);
    }
    return { names, files };
  }

  /**
   * 依赖图与分层：组件文件只用基础设施时 L0，否则是用到的组件文件的最大层号 + 1；
   * `uses` 是直接用到的文件，`usedBy` 是直接用到它的组件文件
   */
  get graph() {
    if (this.#graph) return this.#graph;
    const isInfra = (file) => INFRA.has(file.split('/')[0]);
    const uses = new Map(this.files.map((file) => [file, this.imports(file).files]));
    const level = new Map();
    const visiting = new Set();
    const levelOf = (file) => {
      if (level.has(file)) return level.get(file);
      if (visiting.has(file)) return 0;
      visiting.add(file);
      let result = 0;
      for (const target of uses.get(file)) if (!isInfra(target) && target !== file) result = Math.max(result, levelOf(target) + 1);
      visiting.delete(file);
      level.set(file, result);
      return result;
    };
    const usedBy = new Map(this.files.map((file) => [file, new Set()]));
    for (const [file, targets] of uses) {
      if (isInfra(file)) continue;
      levelOf(file);
      for (const target of targets) if (target !== file) usedBy.get(target).add(file);
    }
    return (this.#graph = { uses, usedBy, level, isInfra });
  }

  /** 找出导出某个名字的文件（`export const|function|class|interface|type 名字`） */
  findExport(name, kinds = 'const|function|class|interface|type') {
    const pattern = new RegExp(`^export (?:declare )?(?:${kinds}) ${name}\\b`, 'm');
    return this.files.filter((file) => pattern.test(this.text(file)));
  }

  close() {
    this.#api.close?.();
  }
}
