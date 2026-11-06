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

/**
 * 长度 → `{ value, unit }`：px、em、rem、%，或不带单位的数字（0 也是）。
 * CSS 的 `round(nearest, 2.25em, 4px)`（控件高度按字号算再取整到 4 的倍数）→ 里面的长度加 `round`（步长，也是长度）
 */
export function parseLength(text) {
  const rounded = text.trim().match(/^round\(\s*(?:nearest\s*,\s*)?([^,]+),([^,)]+)\)$/);
  if (rounded) return { ...parseLength(rounded[1]), round: parseLength(rounded[2]) };
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

const toLinear = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const toGamma = (c) => (c <= 0.0031308 ? 12.92 * c : 1.055 * c ** (1 / 2.4) - 0.055);

/** sRGB（0–255）→ oklab（Björn Ottosson 的矩阵，同 CSS Color 4） */
function oklab({ r, g, b }) {
  const [lr, lg, lb] = [r, g, b].map((c) => toLinear(c / 255));
  const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
  const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
  const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
  return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s, 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s, 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
}

function fromOklab([L, A, B]) {
  const l = (L + 0.3963377774 * A + 0.2158037573 * B) ** 3;
  const m = (L - 0.1055613458 * A - 0.0638541728 * B) ** 3;
  const s = (L - 0.0894841775 * A - 1.291485548 * B) ** 3;
  const linear = [4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s];
  const [r, g, b] = linear.map((c) => Math.round(Math.min(1, Math.max(0, toGamma(c))) * 255));
  return { r, g, b };
}

/** CSS 的 `color-mix(in oklab, a weight, b)`：两个不透明的颜色在 oklab 里按比例混合（vue-tsx-pro 里几处悬停色这样算） */
export function mixOklab(a, b, weight) {
  const [x, y] = [oklab(a), oklab(b)];
  return { ...fromOklab(x.map((v, i) => v * weight + y[i] * (1 - weight))), a: a.a * weight + b.a * (1 - weight) };
}
