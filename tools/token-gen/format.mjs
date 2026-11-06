// 各平台输出共用的写法：数字、XML 注释与转义、时长、生成文件的说明
/** 生成文件开头的说明 */
export const HEADER = '由 tools/token-gen 从 vue-tsx-pro 的 packages/ui/src/config/theme 生成，不要手改；改了那边的 token 后运行 node tools/token-gen/token-gen.mjs';

/** 数字：最多 4 位小数，去掉多余的 0 */
export const number = (n) => String(Number(n.toFixed(4)));

/** C# 的 float 字面量 */
export const float = (n) => `${number(n)}f`;

/** XML 属性与文本里的转义 */
export const escape = (text) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');

/** XML 注释（里面不能出现 `--`） */
export const comment = (text) => `<!-- ${text.replace(/\s+/g, ' ').replace(/--/g, '- -').trim()} -->`;

/** C# 的单行文档注释（XML 转义，合并空白） */
export const summary = (text) => `/// <summary>${escape(text.replace(/\s+/g, ' ').trim())}</summary>`;

/** XAML 里 TimeSpan 的写法：00:00:00.2 */
export function timeSpan(ms) {
  const seconds = ms / 1000;
  return `00:00:${seconds < 10 ? '0' : ''}${number(seconds)}`;
}

/** 一个 token 的说明：取值、单位与注释 */
export function describe(entry) {
  const unit = entry.kind === 'length' ? (entry.value.unit === 'em' ? '（em 系数）' : '（设备无关像素）') : '';
  return `${entry.key} = ${entry.raw}${unit}${entry.doc ? `：${entry.doc}` : ''}`;
}

/** 内置主题里不是亮色、暗色的那些（各平台要给它们单独的名字） */
export const customThemes = (model) => model.themes.filter((theme) => theme.name !== 'light' && theme.name !== 'dark');

/** 字重 → 平台的名字（Avalonia、WPF 同名） */
export const FONT_WEIGHTS = { 300: 'Light', 400: 'Normal', 500: 'Medium', 600: 'SemiBold', 700: 'Bold' };

/** 主题的称呼：中文名（名字，亮暗）；亮色、暗色两套不重复写亮暗 */
export function themeTitle(theme) {
  const scheme = theme.colorScheme === 'dark' ? '暗色' : '亮色';
  return `${theme.label}（${theme.name}${theme.label === scheme ? '' : `，${scheme}`}）`;
}
