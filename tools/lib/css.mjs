// CSS 取值的解析：颜色、长度、阴影、字体族、时长、三次贝塞尔。
// 只认 vue-tsx-pro 的 token 里出现的写法，认不出就报错：vue-tsx-pro 加了新写法时生成脚本停下来，而不是悄悄写错

/** 条件取值 `{ default=75ms; [reduce]=0ms }`（ts-inspect 压成一行的写法）取 default，普通值原样返回 */
export function defaultOf(value) {
  const match = value.match(/^\{.*?\bdefault=([^;}]+)/);
  return (match ? match[1] : value).trim();
}

/** 颜色 → `{ r, g, b, a }`（a 为 0–1）：#rgb、#rgba、#rrggbb、#rrggbbaa（CSS 的 alpha 在最后）、rgb()、rgba() */
export function parseColor(text) {
  const value = text.trim().toLowerCase();
  const hex = value.match(/^#([0-9a-f]{3,8})$/);
  if (hex && [3, 4, 6, 8].includes(hex[1].length)) {
    const digits = hex[1].length <= 4 ? [...hex[1]].map((c) => c + c).join('') : hex[1];
    const [r, g, b, a = 255] = digits.match(/../g).map((pair) => Number.parseInt(pair, 16));
    return { r, g, b, a: a / 255 };
  }
  const fn = value.match(/^rgba?\(([^)]*)\)$/);
  if (fn) {
    const parts = fn[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    if (parts.length >= 3 && parts.every(Number.isFinite)) return { r: parts[0], g: parts[1], b: parts[2], a: parts[3] ?? 1 };
  }
  throw new Error(`认不出的颜色：${text}`);
}

const hex2 = (n) => Math.round(n).toString(16).padStart(2, '0').toUpperCase();

/** XAML 与 .NET 的写法：`#AARRGGBB`（alpha 在最前，和 CSS 相反） */
export const argbHex = ({ r, g, b, a }) => `#${hex2(a * 255)}${hex2(r)}${hex2(g)}${hex2(b)}`;

/** 长度 → `{ value, unit }`：px、em、rem、%，或不带单位的数字（0 也是） */
export function parseLength(text) {
  const match = text.trim().match(/^(-?\d*\.?\d+)(px|em|rem|%)?$/);
  if (!match) throw new Error(`认不出的长度：${text}`);
  return { value: Number(match[1]), unit: match[2] ?? '' };
}

/** 按顶层逗号切开（括号里的逗号不算） */
function splitTopLevel(text) {
  const parts = [];
  let depth = 0;
  let current = '';
  for (const char of text) {
    if (char === '(') depth++;
    if (char === ')') depth--;
    if (char === ',' && depth === 0) {
      parts.push(current.trim());
      current = '';
    } else current += char;
  }
  if (current.trim()) parts.push(current.trim());
  return parts;
}

/** box-shadow → 各层 `{ inset, x, y, blur, spread, color }`（长度都是 px）；none 是空数组 */
export function parseShadows(text) {
  if (text.trim() === 'none') return [];
  return splitTopLevel(text).map((layer) => {
    const tokens = layer.match(/rgba?\([^)]*\)|\S+/g);
    const inset = tokens[0] === 'inset';
    if (inset) tokens.shift();
    const color = parseColor(tokens.pop());
    const lengths = tokens.map((token) => {
      const { value, unit } = parseLength(token);
      if (unit && unit !== 'px') throw new Error(`阴影里只认 px：${layer}`);
      return value;
    });
    if (lengths.length < 2 || lengths.length > 4) throw new Error(`认不出的阴影：${layer}`);
    const [x, y, blur = 0, spread = 0] = lengths;
    return { inset, x, y, blur, spread, color };
  });
}

/** 浏览器专用的通用字体名：桌面平台没有对应，去掉后由平台的默认字体兜底 */
const GENERIC_FONTS = new Set(['system-ui', '-apple-system', 'blinkmacsystemfont', 'ui-sans-serif', 'ui-serif', 'ui-monospace', 'sans-serif', 'serif', 'monospace', 'cursive', 'fantasy']);

/** font-family → 字体名列表（去掉引号与通用字体名，顺序不变） */
export function parseFonts(text) {
  return splitTopLevel(text)
    .map((name) => name.replace(/^['"]|['"]$/g, '').trim())
    .filter((name) => name && !GENERIC_FONTS.has(name.toLowerCase()));
}

/** 时长 → 毫秒：75ms、0.2s */
export function parseDuration(text) {
  const match = text.trim().match(/^(\d*\.?\d+)(ms|s)$/);
  if (!match) throw new Error(`认不出的时长：${text}`);
  return Number(match[1]) * (match[2] === 's' ? 1000 : 1);
}

/** cubic-bezier(x1, y1, x2, y2) → [x1, y1, x2, y2] */
export function parseCubicBezier(text) {
  const match = text.trim().match(/^cubic-bezier\(([^)]*)\)$/);
  const points = match?.[1].split(',').map(Number);
  if (!points || points.length !== 4 || !points.every(Number.isFinite)) throw new Error(`认不出的缓动：${text}`);
  return points;
}
