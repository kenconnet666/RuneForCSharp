// 色调调色板（DESIGN §4 决定 11）：六种状态色调各一份，组件的样式档位只引用调色板的字段，组合档位不再逐条写。
// 字段与取值照 vue-tsx-pro 的 button-core 配方归纳：非中性色调按 {色调}、{色调}Hover…… 的规律取 token，
// 中性没有实色，用反色表面、填充等另一组（与 Badge 的 StatusColors 同一张表）

export const TONES = ['neutral', 'accent', 'info', 'success', 'warning', 'danger'];

/** 调色板的字段：名字 → 说明 */
export const FIELDS = {
  solid: '实底的底色',
  solidHover: '实底悬停的底色',
  contrast: '实底上的文字与图标',
  soft: '浅色底',
  softHover: '浅色底悬停',
  surface: '浅色底加描边（surface 样式）的底色',
  subtleHover: '透明底（outline、ghost）悬停的底色',
  text: '浅色底、描边、透明底上的文字与图标',
  border: '描边',
};

const cap = (name) => name[0].toUpperCase() + name.slice(1);

/** 一种色调的调色板：字段 → colors 组里的 token 名 */
export function paletteOf(tone) {
  if (tone === 'neutral') {
    return {
      solid: 'inverseSurface',
      solidHover: 'inverseSurfaceHover',
      contrast: 'inverseText',
      soft: 'fill',
      softHover: 'fillHover',
      surface: 'surface',
      subtleHover: 'fillHover',
      text: 'text',
      border: 'border',
    };
  }
  return {
    solid: tone,
    solidHover: `${tone}Hover`,
    contrast: `${tone}Contrast`,
    soft: `${tone}Soft`,
    softHover: `${tone}SoftHover`,
    surface: `${tone}Soft`,
    subtleHover: `${tone}Soft`,
    text: `${tone}Text`,
    border: `${tone}Border`,
  };
}

/** 检查调色板引用的 token 在主题里都有：vue-tsx-pro 改了 token 名时生成直接报错 */
export function checkPalettes(theme) {
  const colors = new Set(theme.entries.filter((e) => e.group === 'colors').map((e) => e.key));
  for (const tone of TONES)
    for (const [field, token] of Object.entries(paletteOf(tone)))
      if (!colors.has(token)) throw new Error(`调色板 ${tone}.${field} 引用的 colors.${token} 在主题 ${theme.name} 里没有`);
}

export { cap };
