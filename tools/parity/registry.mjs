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
  required: 'IsRequired',
  invalid: 'IsInvalid',
  indeterminate: 'IsIndeterminate',
  labelHidden: 'IsLabelHidden',
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
  // Measure 是布局的方法
  Prose: { measure: 'LimitLineLength' },
  // Name 是控件名；图片给平台的图片对象
  Avatar: { name: 'DisplayName', src: 'Source' },
  // 平台的 ItemsControl 自己有 Items（直接写的项）；数据给 ItemsSource
  CheckboxGroup: { items: 'ItemsSource' },
  RadioGroup: { items: 'ItemsSource' },
};

/** 某个平台上统一改了名的 prop：WinForms 的控件自己有 Size（大小），字号档位叫 TextSize */
export const PLATFORM_PROP_RENAMES = {
  WinForms: { size: 'TextSize' },
};

/** 某个平台上某个组件改了名的 prop（盖过 PLATFORM_PROP_RENAMES）：WinForms 的控件自己有 Size、Padding */
export const PLATFORM_COMPONENT_PROP_RENAMES = {
  WinForms: { Container: { size: 'ContainerSize', padding: 'InlinePadding' }, Card: { padding: 'CardPadding' } },
};

/** 某个平台上改了名的组件（平台自带同名类型时）：库 → { 组件: 类型名 } */
export const TYPE_RENAMES = {
  // 三个平台都自带 Label：表单标签叫 FieldLabel
  Avalonia: { Label: 'FieldLabel' },
  WPF: { Label: 'FieldLabel' },
  // WinForms 自己有 ListControl（ListBox、ComboBox 的基类）
  WinForms: { List: 'UnorderedList', Container: 'ContainerPanel', Label: 'FieldLabel' },
};

const FORM_NAME = '表单提交的字段名是浏览器的概念；值经 Value 绑定，单选组的互斥由组自己管';

const BUTTON_TYPE = '表单提交是浏览器的概念；对话框的默认、取消按钮用平台的 IsDefault、IsCancel';

/** 某个组件不移植的 prop 与原因（平台上没有对应的概念）：parity 不报缺 */
export const PROPS_NOT_PORTED = {
  Button: { type: BUTTON_TYPE },
  IconButton: { type: BUTTON_TYPE },
  ButtonBase: { type: BUTTON_TYPE },
  CloseButton: { type: BUTTON_TYPE },
  ClearButton: { type: BUTTON_TYPE },
  Toolbar: { items: '桌面上没有 CSS 选择器：参与方向键移动的固定是按钮与链接' },
  Skeleton: { variant: '上游已废弃（改名为 shape），新库不带' },
  Progress: { propsRoot: '外层就是 Progress 自己（读屏的进度条也是它）：样式直接写在 Progress 上' },
  Switch: { propsRoot: '外层（TS 的 label）就是 Switch 自己：样式直接写在 Switch 上' },
  Checkbox: { propsRoot: '外层（TS 的 label）就是 Checkbox 自己：样式直接写在 Checkbox 上' },
  Radio: { propsRoot: '外层（TS 的 label）就是 Radio 自己：样式直接写在 Radio 上' },
  CheckboxGroup: { name: FORM_NAME },
  RadioGroup: { name: FORM_NAME },
};

/** 某个平台上不移植的 prop 与原因（盖在 PROPS_NOT_PORTED 上）：库 → { 组件: { prop: 原因 } } */
export const PLATFORM_PROPS_NOT_PORTED = {
  WinForms: {
    Collapse: { lazy: '收起时子控件隐藏：隐藏的控件不建句柄、不参与布局，本来就是第一次展开才真正创建' },
  },
};

/** 平台控件自带、不用组件自己声明的属性（基类里已经有；ItemsSource 是 Avalonia、WPF 的 ItemsControl 的） */
export const PLATFORM_PROPS = new Set(['IsEnabled', 'Content', 'Tag', 'AutomationProperties.Name', 'ItemsSource']);

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
  Flex: { createFlex: '桌面没有语义标签：别的语义容器从 Flex 派生、改读屏角色（FlexList、FlexOrderedList 就是这样，测试覆盖）' },
  Container: { createContainer: '桌面没有语义标签：要别的语义时从 Container 派生' },
  Grid: { createGrid: '桌面没有语义标签：别的语义容器从 Grid 派生、改读屏角色（GridList 就是这样，测试覆盖）' },
  Collapse: { '页内搜索命中': '页内搜索命中收起的内容时展开（hidden="until-found"、beforematch）是浏览器的能力，桌面没有页内搜索' },
};
