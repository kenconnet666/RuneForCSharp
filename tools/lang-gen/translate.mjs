// TS → C#：语言包的类型映射成 C# 类型，取值（字符串、表、插值函数）按语法树翻译成 C# 表达式。
// 只认语言包里出现的写法：模板字符串、三元、=== / == null / 真值判断、join、toLocaleString、对象字面量按键取值；
// 认不出的报错并指出键，扩展这里或把键放进 lang-gen.mjs 的 SKIPPED（写明原因）

/** PascalCase：paginationTotal → PaginationTotal */
export const pascal = (name) => name.replace(/^\w/, (c) => c.toUpperCase());

/** C# 普通字符串字面量 */
export const csString = (text) => `"${text.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\r?\n/g, '\\n')}"`;

/** C# 的关键字：JS 的参数名撞上时加 @（`checked` → `@checked`） */
const KEYWORDS = new Set(['abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class', 'const', 'continue', 'decimal', 'default', 'delegate', 'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null', 'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref', 'return', 'sbyte', 'sealed', 'short', 'sizeof', 'stackalloc', 'static', 'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while']);
const ident = (name) => (KEYWORDS.has(name) ? `@${name}` : name);

/** 插值字符串里的文字部分：另外要把花括号写双 */
const interpolatedText = (text) => text.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\r?\n/g, '\\n').replace(/\{/g, '{{').replace(/\}/g, '}}');

export class Translator {
  constructor(ast) {
    this.ast = ast;
    this.K = ast.SyntaxKind;
  }

  // ───────── 类型 ─────────

  /**
   * TS 类型节点 → `{ kind, cs, nullable, element?, fields?, params? }`。
   * kind：string、number、bool、list、dict、tuple、func
   */
  type(node, source) {
    const { ast, K } = this;
    if (ast.isParenthesizedTypeNode(node)) return this.type(node.type, source);
    if (node.kind === K.StringKeyword) return { kind: 'string', cs: 'string' };
    if (node.kind === K.NumberKeyword) return { kind: 'number', cs: 'double' };
    if (node.kind === K.BooleanKeyword) return { kind: 'bool', cs: 'bool' };
    if (ast.isLiteralTypeNode(node)) return { kind: 'string', cs: 'string' };
    if (ast.isUnionTypeNode(node)) {
      const isNullish = (t) => t.kind === K.UndefinedKeyword || (ast.isLiteralTypeNode(t) && t.literal.kind === K.NullKeyword);
      const rest = node.types.filter((t) => !isNullish(t));
      const nullable = rest.length < node.types.length;
      const inner = rest.every((t) => ast.isLiteralTypeNode(t)) ? { kind: 'string', cs: 'string' } : rest.length === 1 ? this.type(rest[0], source) : this.fail(`联合类型 ${node.getText(source)}`);
      return nullable ? this.nullable(inner) : inner;
    }
    if (ast.isTypeOperatorNode(node)) {
      if (node.operator === K.KeyOfKeyword) return { kind: 'string', cs: 'string' };
      if (node.operator === K.ReadonlyKeyword) return this.type(node.type, source);
    }
    if (ast.isArrayTypeNode(node)) return this.list(this.type(node.elementType, source));
    if (ast.isTupleTypeNode(node)) {
      const elements = node.elements.map((element) => this.type(element, source));
      if (elements.some((element) => element.cs !== elements[0].cs)) this.fail(`元素类型不同的元组 ${node.getText(source)}`);
      return this.list(elements[0]);
    }
    if (ast.isTypeLiteralNode(node)) {
      const fields = node.members.map((member) => ({ name: member.name.getText(source), type: this.type(member.type, source) }));
      return { kind: 'tuple', fields, cs: `(${fields.map((f) => `${f.type.cs} ${pascal(f.name)}`).join(', ')})` };
    }
    if (ast.isFunctionTypeNode(node)) {
      const params = node.parameters.map((p) => {
        const type = this.type(p.type, source);
        const optional = /^\w+\s*\?/.test(p.getText(source));
        return { name: p.name.getText(source), type: optional ? this.nullable(type) : type };
      });
      if (this.type(node.type, source).kind !== 'string') this.fail(`返回值不是字符串的函数 ${node.getText(source)}`);
      return { kind: 'func', params, cs: `Func<${[...params.map((p) => p.type.cs), 'string'].join(', ')}>` };
    }
    if (ast.isTypeReferenceNode(node)) {
      const name = node.typeName.getText(source);
      const args = node.typeArguments ?? [];
      if (name === 'Readonly' || name === 'Partial') return this.type(args[0], source);
      if (name === 'Record') {
        const value = this.type(args[1], source);
        return { kind: 'dict', value, cs: `IReadOnlyDictionary<string, ${value.cs}>` };
      }
    }
    return this.fail(`类型 ${node.getText(source)}`);
  }

  nullable(type) {
    return type.nullable ? type : { ...type, nullable: true, cs: `${type.cs}?` };
  }

  list(element) {
    return { kind: 'list', element, cs: `IReadOnlyList<${element.cs}>` };
  }

  // ───────── 取值 ─────────

  /** 按类型翻译一个取值；indent 是多行输出（表）的缩进 */
  value(type, node, source, indent) {
    const { ast, K } = this;
    switch (type.kind) {
      case 'string':
        if (node.kind === K.NullKeyword && type.nullable) return 'null';
        if (ast.isStringLiteral(node) || ast.isNoSubstitutionTemplateLiteral(node)) return csString(node.text);
        break;
      case 'number':
        if (ast.isNumericLiteral(node)) return node.getText(source);
        break;
      case 'bool':
        if (node.kind === K.TrueKeyword || node.kind === K.FalseKeyword) return node.getText(source);
        break;
      case 'list':
        if (ast.isArrayLiteralExpression(node)) return `[${node.elements.map((element) => this.value(type.element, element, source, indent)).join(', ')}]`;
        break;
      case 'dict':
        if (ast.isObjectLiteralExpression(node)) {
          const inner = `${indent}    `;
          const entries = node.properties.map((p) => `${inner}[${csString(this.key(p, source))}] = ${this.value(type.value, p.initializer, source, inner)},`);
          return `new Dictionary<string, ${type.value.cs}>\n${indent}{\n${entries.join('\n')}\n${indent}}`;
        }
        break;
      case 'tuple':
        if (ast.isObjectLiteralExpression(node)) {
          const props = new Map(node.properties.map((p) => [this.key(p, source), p.initializer]));
          return `(${type.fields.map((f) => this.value(f.type, props.get(f.name) ?? this.fail(`缺少 ${f.name}`), source, indent)).join(', ')})`;
        }
        break;
      case 'func':
        if (ast.isArrowFunction(node)) return this.arrow(type, node, source);
        break;
    }
    return this.fail(`${type.cs} 的取值 ${node.getText(source)}`);
  }

  key(property, source) {
    const { ast } = this;
    if (!ast.isPropertyAssignment(property)) this.fail(`属性 ${property.getText(source)}`);
    const name = property.name;
    return ast.isStringLiteral(name) || ast.isNumericLiteral(name) ? name.text : name.getText(source);
  }

  // ───────── 插值函数 ─────────

  arrow(type, node, source) {
    if (!node.body || this.ast.isBlock?.(node.body) || node.body.statements) this.fail('函数体是代码块的箭头函数');
    const names = node.parameters.map((p) => p.name.getText(source));
    const vars = new Map(names.map((name, i) => [name, type.params[i]?.type ?? this.fail(`多出的参数 ${name}`)]));
    const body = this.expression(node.body, source, vars);
    const params = names.map(ident);
    return `${params.length === 1 ? params[0] : `(${params.join(', ')})`} => ${body}`;
  }

  /** 表达式 → C#；结果都是字符串，除了条件与数字 */
  expression(node, source, vars) {
    const { ast, K } = this;
    if (ast.isParenthesizedExpression(node)) return `(${this.expression(node.expression, source, vars)})`;
    if (ast.isStringLiteral(node) || ast.isNoSubstitutionTemplateLiteral(node)) return csString(node.text);
    if (ast.isNumericLiteral(node)) return node.getText(source);
    if (ast.isIdentifier(node)) {
      if (!vars.has(node.text)) this.fail(`不是参数的名字 ${node.text}`);
      return ident(node.text);
    }
    if (ast.isTemplateExpression(node)) {
      let numeric = false;
      let text = interpolatedText(node.head.text);
      for (const span of node.templateSpans) {
        if (this.isNumber(span.expression, vars)) numeric = true;
        text += `{${this.interpolation(span.expression, source, vars)}}${interpolatedText(span.literal.text)}`;
      }
      // 数字按不变文化输出，同 JS 模板字符串里的数字（不受当前文化的小数点影响）
      return numeric ? `string.Create(CultureInfo.InvariantCulture, $"${text}")` : `$"${text}"`;
    }
    if (ast.isConditionalExpression(node)) {
      return `${this.condition(node.condition, source, vars)} ? ${this.expression(node.whenTrue, source, vars)} : ${this.expression(node.whenFalse, source, vars)}`;
    }
    if (ast.isCallExpression(node) && ast.isIdentifier(node.expression) && node.expression.text === 'String' && node.arguments.length === 1) {
      const [arg] = node.arguments;
      return this.isNumber(arg, vars) ? `${this.number(arg, source, vars)}.ToString(CultureInfo.InvariantCulture)` : this.expression(arg, source, vars);
    }
    if (ast.isCallExpression(node) && ast.isPropertyAccessExpression(node.expression)) {
      const method = node.expression.name.getText(source);
      const target = this.expression(node.expression.expression, source, vars);
      const [arg] = node.arguments;
      if (method === 'toLocaleString' && arg && ast.isStringLiteral(arg)) return `LangFormat.Number(${target}, ${csString(arg.text)})`;
      if (method === 'join' && arg && ast.isStringLiteral(arg)) return `string.Join(${csString(arg.text)}, ${target})`;
    }
    if (ast.isElementAccessExpression(node)) {
      // `'一二三四'[quarter - 1]` → 按下标取一个字
      if (ast.isStringLiteral(node.expression)) return `${csString(node.expression.text)}[(int)(${this.number(node.argumentExpression, source, vars)})].ToString()`;
      // `{ a: '甲', b: '乙' }[key]` → switch 表达式；取不到时原样给出键
      let table = node.expression;
      while (ast.isParenthesizedExpression(table)) table = table.expression;
      if (ast.isObjectLiteralExpression(table) && ast.isIdentifier(node.argumentExpression)) {
        const key = this.expression(node.argumentExpression, source, vars);
        const arms = table.properties.map((p) => `${csString(this.key(p, source))} => ${this.expression(p.initializer, source, vars)}`);
        return `${key} switch { ${[...arms, `_ => ${key}`].join(', ')} }`;
      }
    }
    if (ast.isBinaryExpression(node) && node.operatorToken.kind === K.PlusToken) {
      return `${this.expression(node.left, source, vars)} + ${this.expression(node.right, source, vars)}`;
    }
    return this.fail(`表达式 ${node.getText(source)}`);
  }

  /** 数字表达式：数字参数、数字字面量与它们的加减乘除 */
  number(node, source, vars) {
    const { ast, K } = this;
    if (ast.isParenthesizedExpression(node)) return `(${this.number(node.expression, source, vars)})`;
    if (this.isNumber(node, vars)) return ast.isIdentifier(node) ? ident(node.text) : node.getText(source);
    const operators = { [K.PlusToken]: '+', [K.MinusToken]: '-', [K.AsteriskToken]: '*', [K.SlashToken]: '/' };
    if (ast.isBinaryExpression(node) && operators[node.operatorToken.kind]) {
      return `${this.number(node.left, source, vars)} ${operators[node.operatorToken.kind]} ${this.number(node.right, source, vars)}`;
    }
    return this.fail(`数字表达式 ${node.getText(source)}`);
  }

  /** 插值里的表达式：不是名字的都加括号（三元里的冒号会被当成格式说明） */
  interpolation(node, source, vars) {
    const text = this.expression(node, source, vars);
    return /^@?\w+$/.test(text) ? text : `(${text})`;
  }

  isNumber(node, vars) {
    const { ast } = this;
    if (ast.isNumericLiteral(node)) return true;
    return ast.isIdentifier(node) && vars.get(node.text)?.kind === 'number';
  }

  /** 条件：比较（=== / == null）、真值判断（字符串非空、布尔、数字非 0）、取反 */
  condition(node, source, vars) {
    const { ast, K } = this;
    if (ast.isParenthesizedExpression(node)) return `(${this.condition(node.expression, source, vars)})`;
    if (ast.isPrefixUnaryExpression?.(node) && node.operator === K.ExclamationToken) return `!(${this.condition(node.operand, source, vars)})`;
    if (ast.isBinaryExpression(node)) {
      const op = node.operatorToken.kind;
      if (op === K.AmpersandAmpersandToken || op === K.BarBarToken) {
        return `${this.condition(node.left, source, vars)} ${op === K.AmpersandAmpersandToken ? '&&' : '||'} ${this.condition(node.right, source, vars)}`;
      }
      const equal = op === K.EqualsEqualsEqualsToken || op === K.EqualsEqualsToken;
      const notEqual = op === K.ExclamationEqualsEqualsToken || op === K.ExclamationEqualsToken;
      if (equal || notEqual) {
        const left = this.expression(node.left, source, vars);
        if (node.right.kind === K.NullKeyword || node.right.getText(source) === 'undefined') return `${left} ${equal ? 'is null' : 'is not null'}`;
        return `${left} ${equal ? '==' : '!='} ${this.expression(node.right, source, vars)}`;
      }
      const comparisons = { [K.GreaterThanToken]: '>', [K.LessThanToken]: '<', [K.GreaterThanEqualsToken]: '>=', [K.LessThanEqualsToken]: '<=' };
      if (comparisons[op]) return `${this.expression(node.left, source, vars)} ${comparisons[op]} ${this.expression(node.right, source, vars)}`;
    }
    if (ast.isIdentifier(node) && vars.has(node.text)) {
      const type = vars.get(node.text);
      const name = ident(node.text);
      if (type.kind === 'bool') return type.nullable ? `${name} == true` : name;
      if (type.kind === 'string') return `!string.IsNullOrEmpty(${name})`;
      if (type.kind === 'number') return type.nullable ? `${name} is not null and not 0` : `${name} != 0`;
    }
    return this.fail(`条件 ${node.getText(source)}`);
  }

  fail(what) {
    throw new Error(`认不出的${what}`);
  }
}
