// 对照的登记表：平台上改了名的 props、不做的组件。新的改名、不做的组件都登记在这里，parity 才不会把它们报成缺失

/**
 * TS 的 prop → 三个库的属性名（不在表里的按 PascalCase：variant → Variant）。
 * 布尔状态按平台的习惯加 Is（ROADMAP"平台化的约定"）；可双向绑定的 `_value` 这类去掉下划线
 */
export const PROP_RENAMES = {
  open: 'IsOpen',
  _open: 'IsOpen',
  checked: 'IsChecked',
  _checked: 'IsChecked',
  expanded: 'IsExpanded',
  _expanded: 'IsExpanded',
  loading: 'IsLoading',
  readonly: 'IsReadOnly',
  disabled: 'IsEnabled',
  children: 'Content',
  _value: 'Value',
};

/** 平台控件自带、不用组件自己声明的属性（基类里已经有） */
export const PLATFORM_PROPS = new Set(['IsEnabled', 'Content', 'Tag']);

/** 桌面上不做的组件与原因（ROADMAP 阶段 3） */
export const EXCLUDED = {
  VisuallyHidden: '桌面上用 AutomationProperties.Name / HelpText',
  SkipLink: '浏览器专用（跳到主内容的链接）',
  ClientOnly: '浏览器专用（服务端渲染时不输出）',
  Can: '浏览器专用（权限由应用层决定）',
};
