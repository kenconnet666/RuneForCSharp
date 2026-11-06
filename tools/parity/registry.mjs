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
  // 读屏名称：Avalonia、WPF 的附加属性，WinForms 的 AccessibleName
  'aria-label': 'AutomationProperties.Name',
};

/** 只对某个组件的改名（C# 的成员不能与类同名：Icon 的 icon 叫 Data） */
export const COMPONENT_PROP_RENAMES = {
  Icon: { icon: 'Data' },
  // Text 是 TextBlock 自己的属性
  CodeText: { text: 'Source' },
};

/** 某个平台上统一改了名的 prop：WinForms 的控件自己有 Size（大小），字号档位叫 TextSize */
export const PLATFORM_PROP_RENAMES = {
  WinForms: { size: 'TextSize' },
};

/** 某个平台上改了名的组件（平台自带同名类型时）：库 → { 组件: 类型名 } */
export const TYPE_RENAMES = {
  // WinForms 自己有 ListControl（ListBox、ComboBox 的基类）
  WinForms: { List: 'UnorderedList' },
};

const BUTTON_TYPE = '表单提交是浏览器的概念；对话框的默认、取消按钮用平台的 IsDefault、IsCancel';

/** 某个组件不移植的 prop 与原因（平台上没有对应的概念）：parity 不报缺 */
export const PROPS_NOT_PORTED = {
  Button: { type: BUTTON_TYPE },
  IconButton: { type: BUTTON_TYPE },
  ButtonBase: { type: BUTTON_TYPE },
  CloseButton: { type: BUTTON_TYPE },
  ClearButton: { type: BUTTON_TYPE },
};

/** 平台控件自带、不用组件自己声明的属性（基类里已经有） */
export const PLATFORM_PROPS = new Set(['IsEnabled', 'Content', 'Tag', 'AutomationProperties.Name']);

/** 桌面上不做的组件与原因（ROADMAP 阶段 3） */
export const EXCLUDED = {
  VisuallyHidden: '桌面上用 AutomationProperties.Name / HelpText',
  SkipLink: '浏览器专用（跳到主内容的链接）',
  ClientOnly: '浏览器专用（服务端渲染时不输出）',
};

/**
 * 桌面上不适用的 vue-tsx-pro 测试用例（tools/parity/behavior.mjs）：组件 → { it 名字的开头: 原因 }。
 * 其余用例要有测试标了 [Upstream("组件", "名字开头")]
 */
export const BEHAVIOR_NOT_APPLICABLE = {
  LinkButton: { '站内链接经 router 跳转': '桌面没有站内地址：Href 是绝对地址，用系统打开（ROADMAP 平台化的约定）' },
  IconLinkButton: { '站内链接经 router 跳转': '桌面没有站内地址：Href 是绝对地址，用系统打开（ROADMAP 平台化的约定）' },
};
