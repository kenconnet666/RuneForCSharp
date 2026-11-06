// 与平台无关的 token 模型：每个 token 解析成颜色、字体族、长度、阴影、时长、缓动等结构，三个平台的输出都从这里取
import { defaultOf, mixOklab, parseColor, parseCubicBezier, parseDuration, parseFonts, parseLength, parseShadows } from '../lib/css.mjs';
import { builtinThemes, themedGroups, themeValue, tokenGroups } from '../lib/theme.mjs';

/**
 * 每组 token 在平台上的名字（单数，资源键是 `Rune.<名字>.<token>`）与取值的种类。
 * vue-tsx-pro 加了组时这里要登记，否则生成时报错，免得悄悄漏掉
 */
export const GROUPS = {
  colors: { name: 'Color', kind: 'color' },
  fonts: { name: 'Font', kind: 'font' },
  fontSizes: { name: 'FontSize', kind: 'length' },
  fontWeights: { name: 'FontWeight', kind: 'number' },
  lineHeights: { name: 'LineHeight', kind: 'number' },
  letterSpacings: { name: 'LetterSpacing', kind: 'length' },
  space: { name: 'Space', kind: 'length' },
  sizes: { name: 'Size', kind: 'length' },
  radii: { name: 'Radius', kind: 'length' },
  borderWidths: { name: 'BorderWidth', kind: 'length' },
  shadows: { name: 'Shadow', kind: 'shadow' },
  durations: { name: 'Duration', kind: 'duration' },
  easings: { name: 'Easing', kind: 'easing' },
  opacities: { name: 'Opacity', kind: 'number' },
  strokeWidths: { name: 'StrokeWidth', kind: 'number' },
};

/** 平台上用不了的 token：生成时跳过，原因写进生成的文件 */
export const SKIPPED = {
  'sizes.full': '100%：平台上用拉伸（Stretch）',
};

/**
 * 派生的颜色：vue-tsx-pro 在组件样式里用 color-mix 现算、没有对应 token 的颜色，按主题算好补进 colors 组，
 * 三个平台当普通 token 用。key 是生成的 token 名，mix 给出（颜色甲, 颜色乙, 甲的比例）
 */
export const DERIVED_COLORS = {
  inverseSurfaceHover: {
    doc: "中性实底按钮的悬停底色（button-core 的 color-mix(in oklab, inverseSurface 85%, background)）",
    mix: ["inverseSurface", "background", 0.85],
  },
};

/** rem 按浏览器默认的根字号换算成设备无关像素；整体缩放由运行时负责（DESIGN §6） */
const REM = 16;

/** token 名 → PascalCase：accentSoftHover → AccentSoftHover，high-contrast → HighContrast */
export const pascal = (name) => name.replace(/[-_](\w)/g, (_, c) => c.toUpperCase()).replace(/^\w/, (c) => c.toUpperCase());

/** 解析一个取值。长度：em 留作系数（unit=em），px、rem 换成设备无关像素（unit=px） */
function convert(kind, raw) {
  const value = defaultOf(raw);
  switch (kind) {
    case 'color':
      return parseColor(value);
    case 'font':
      return parseFonts(value);
    case 'number': {
      const number = Number(value);
      if (!Number.isFinite(number)) throw new Error(`不是数字：${raw}`);
      return number;
    }
    case 'length': {
      const length = parseLength(value);
      if (length.unit === '%') throw new Error(`百分比没法换算：${raw}（在 SKIPPED 里登记）`);
      if (length.unit === 'rem') return { value: length.value * REM, unit: 'px' };
      return { value: length.value, unit: length.unit === 'em' ? 'em' : 'px' };
    }
    case 'shadow':
      return parseShadows(value);
    case 'duration':
      return parseDuration(value);
    case 'easing':
      return parseCubicBezier(value);
    default:
      throw new Error(`没有这种取值：${kind}`);
  }
}

/** 主题的中文名：语言包里的 theme<名字>（'high-contrast' → themeHighContrast） */
function themeLabels(vue) {
  return new Map([...vue.text('config/lang/zh-CN.ts').matchAll(/^\s*theme(\w+): '([^']+)'/gm)].map(([, key, label]) => [key, label]));
}

/**
 * 模型：`base` 是不随主题变的组（字号、间距、动效……），`themes` 是 16 套内置主题各自的完整一组
 * （被主题覆盖的组：颜色、阴影、字体、圆角）。每项 `{ group, key, name, kind, raw, value, doc }`
 */
export function buildModel(vue) {
  const groups = tokenGroups(vue);
  for (const group of groups.keys()) {
    if (!GROUPS[group]) throw new Error(`vue-tsx-pro 新加了 token 组 ${group}：在 tools/token-gen/model.mjs 的 GROUPS 里登记`);
  }
  const themed = themedGroups(vue);
  const entry = (group, key, raw, doc) => {
    const { name, kind } = GROUPS[group];
    try {
      return { group, key, name: `${name}.${pascal(key)}`, kind, raw, value: convert(kind, raw), doc };
    } catch (error) {
      throw new Error(`${group}.${key}：${error.message}`);
    }
  };
  const tokens = (group) => [...groups.get(group).tokens].filter(([key]) => !SKIPPED[`${group}.${key}`]);

  const base = [...groups.keys()]
    .filter((group) => !themed.has(group))
    .flatMap((group) => tokens(group).map(([key, { value, doc }]) => entry(group, key, value, doc)));
  const labels = themeLabels(vue);
  const themes = builtinThemes(vue).map((theme) => ({
    name: theme.name,
    id: pascal(theme.name),
    colorScheme: theme.colorScheme,
    label: labels.get(pascal(theme.name)) ?? theme.name,
    entries: derive([...themed].flatMap((group) => tokens(group).map(([key, { doc }]) => entry(group, key, themeValue(vue, theme, group, key), doc)))),
  }));
  return { groups: [...groups.keys()], themed: [...themed], base, themes, skipped: SKIPPED };

  /** 在 colors 组的最后补上派生的颜色（DERIVED_COLORS） */
  function derive(entries) {
    const colors = new Map(entries.filter((e) => e.group === "colors").map((e) => [e.key, e.value]));
    const derived = Object.entries(DERIVED_COLORS).map(([key, { doc, mix: [a, b, weight] }]) => ({
      group: "colors",
      key,
      name: `Color.${pascal(key)}`,
      kind: "color",
      raw: `color-mix(in oklab, ${a} ${weight * 100}%, ${b})`,
      value: mixOklab(colors.get(a), colors.get(b), weight),
      doc: `${doc}（token-gen 算出）`,
    }));
    const last = entries.findLastIndex((e) => e.group === "colors");
    return [...entries.slice(0, last + 1), ...derived, ...entries.slice(last + 1)];
  }
}

