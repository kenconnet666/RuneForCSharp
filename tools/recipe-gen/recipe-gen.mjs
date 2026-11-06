#!/usr/bin/env node
// 组件配方的生成：样式档位 × 色调的组合（vue-tsx-pro 的 css.recipe compound），按调色板表（token-gen/palette.mjs）展开。
// Avalonia 用调色板对象，样式里只写 6 条档位（手写在组件的 axaml 里）；WPF 用主题样式的 MultiTrigger（DESIGN §4），
// 36 条组合加悬停逐条写容易错，这里生成。用法：node tools/recipe-gen/recipe-gen.mjs [--check]
import { parseArgs } from 'node:util';
import { writeOutputs } from '../lib/output.mjs';
import { cap, paletteOf, TONES } from '../token-gen/palette.mjs';

/**
 * 配方：样式档位 → 各属性取调色板的哪个字段；hover 是悬停时（只在可用时）覆盖的属性。
 * 取值照 vue-tsx-pro 的 button-core 的 buttonRecipe：中性与其他色调的差别已经收在调色板里
 */
export const RECIPES = {
  Button: {
    file: 'wpf/src/Rune.Wpf/Base/Button/ButtonRecipe.g.xaml',
    variants: {
      solid: { Background: 'solid', Foreground: 'contrast', hover: { Background: 'solidHover' } },
      soft: { Background: 'soft', Foreground: 'text', hover: { Background: 'softHover' } },
      surface: { Background: 'surface', Foreground: 'text', BorderBrush: 'border', hover: { Background: 'softHover' } },
      outline: { Foreground: 'text', BorderBrush: 'border', hover: { Background: 'subtleHover' } },
      ghost: { Foreground: 'text', hover: { Background: 'subtleHover' } },
      link: { Foreground: 'text' },
    },
  },
};

const brush = (tone, field) => `{DynamicResource Rune.Brush.${cap(paletteOf(tone)[field])}}`;

function trigger(component, variant, tone, setters, hover) {
  const conditions = [
    `<Condition Property="Variant" Value="${variant}" />`,
    `<Condition Property="Tone" Value="${tone}" />`,
    ...(hover ? ['<Condition Property="IsMouseOver" Value="True" />'] : []),
  ];
  const values = Object.entries(setters).map(([property, field]) => `<Setter Property="${property}" Value="${brush(tone, field)}" />`);
  return `      <MultiTrigger><MultiTrigger.Conditions>${conditions.join('')}</MultiTrigger.Conditions>${values.join('')}</MultiTrigger>`;
}

function wpf(component, recipe) {
  const base = [];
  const hovers = [];
  for (const [variant, { hover, ...setters }] of Object.entries(recipe.variants))
    for (const tone of TONES) {
      base.push(trigger(component, variant, tone, setters, false));
      if (hover) hovers.push(trigger(component, variant, tone, hover, true));
    }
  return [
    '<!-- 由 tools/recipe-gen 按调色板表（tools/token-gen/palette.mjs）生成，不要手改；运行 node tools/recipe-gen/recipe-gen.mjs -->',
    `<!-- ${component} 的样式 × 色调（vue-tsx-pro 的配方 compound）：先 ${base.length} 条组合，再 ${hovers.length} 条悬停（排在后面的触发器优先）。组件的主题样式 BasedOn 它 -->`,
    '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
    '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
    '                    xmlns:rune="clr-namespace:Rune.Wpf">',
    `  <Style x:Key="Rune.Recipe.${component}" TargetType="rune:${component}">`,
    '    <Style.Triggers>',
    ...base,
    ...hovers,
    '    </Style.Triggers>',
    '  </Style>',
    '</ResourceDictionary>',
    '',
  ].join('\n');
}

const { values } = parseArgs({ options: { check: { type: 'boolean', default: false } } });
const outputs = Object.entries(RECIPES).map(([component, recipe]) => ({ path: recipe.file, content: wpf(component, recipe) }));
process.exitCode = writeOutputs(outputs, { check: values.check, commit: '调色板表', command: 'node tools/recipe-gen/recipe-gen.mjs' });
