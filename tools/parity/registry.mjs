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
  _selected: 'IsSelected',
  expanded: 'IsExpanded',
  _expanded: 'IsExpanded',
  _fullscreen: 'IsFullscreen',
  _sidebarOpen: 'IsSidebarOpen',
  _sidebarCollapsed: 'IsSidebarCollapsed',
  // 开关变了是事件（参数里带原因）
  onOpenChange: 'OpenChanged',
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
  // 多行文本框的值是平台 TextBox 的 Text（TextField 有 format，值另是 Value：有格式时是去掉分隔的原文）
  Textarea: { _value: 'Text' },
  // 数据是平台的 ItemsSource；点选是事件
  Listbox: { items: 'ItemsSource', onPick: 'Picked' },
  // Text 是 TextBlock 自己的属性
  CodeText: { text: 'Source' },
  // Measure 是布局的方法
  Prose: { measure: 'LimitLineLength' },
  // Name 是控件名；图片给平台的图片对象
  Avatar: { name: 'DisplayName', src: 'Source' },
  UserButton: { name: 'DisplayName', src: 'Source' },
  // 移除由使用方做：事件是"请求移除"（离场结束才发）
  Tag: { onRemove: 'RemoveRequested' },
  // 关闭由使用方做：事件是"请求关闭"（Avalonia、WPF 离场结束才发）
  ...Object.fromEntries(['Alert', 'Banner'].map((name) => [name, { onClose: 'CloseRequested' }])),
  // 关闭、新建由使用方做：事件是"请求"；排序由 TabList 自己挪好标签，事件报告新顺序
  Tab: { onClose: 'CloseRequested' },
  TabList: { onAdd: 'AddRequested', onReorder: 'Reordered' },
  // 点了这一条是事件（订阅了才整条可点；Select 是动词，选中另有 IsSelected）；正在看的一条是 IsSelected（同平台的选中项）
  ItemListItem: { onSelect: 'Invoked', selected: 'IsSelected' },
  // 执行是按钮的 Click（还有 Command）；右键菜单的菜单项同 Menu 的 Items
  MenuItem: { onAction: 'Click' },
  ContextMenu: { menu: 'Items' },
  SelectedTags: { onRemove: 'RemoveRequested' },
  // 数据是平台的 ItemsSource；自定义选项内容是数据模板；新建是事件（Control 自己有 Created）
  Select: { items: 'ItemsSource', children: 'ItemTemplate', onCreate: 'OptionCreated' },
  // 数据是平台的 ItemsSource；执行了一项是事件；输入框的文字同 TextBox 叫 Text；自定义选项内容是数据模板
  CommandPalette: { items: 'ItemsSource', onAction: 'ItemInvoked', _inputValue: 'InputText', renderOption: 'ItemTemplate' },
  // 平台的 ItemsControl 自己有 Items（直接写的项）；数据给 ItemsSource
  CheckboxGroup: { items: 'ItemsSource' },
  RadioGroup: { items: 'ItemsSource' },
  // 平台控件自己有 Focusable（能不能聚焦）：内容超出时才可聚焦的开关另起名
  ScrollArea: { focusable: 'IsViewportFocusable' },
  // 代码的语言：lang 是缩写，C# 写全
  CodeBlock: { lang: 'Language' },
  // 控件自己有 Width
  TableHeaderCell: { width: 'ColumnWidth' },
  // Avalonia、WPF 沿用平台的提示（ToolTip.Tip、ToolTipService），组件库只补位置、距离、箭头
  Tooltip: {
    children: 'ToolTip.Tip',
    _open: 'ToolTip.IsOpen',
    delay: 'ToolTip.ShowDelay',
    disabled: 'ToolTip.ServiceEnabled',
    onOpenChange: 'ToolTip.ToolTipOpening',
  },
};

/** 某个平台上统一改了名的 prop：WinForms 的控件自己有 Size（大小），字号档位叫 TextSize */
export const PLATFORM_PROP_RENAMES = {
  WinForms: { size: 'TextSize' },
};

/** 某个平台上某个组件改了名的 prop（盖过 PLATFORM_PROP_RENAMES）：WinForms 的控件自己有 Size、Padding */
export const PLATFORM_COMPONENT_PROP_RENAMES = {
  // 按数据生成的项的属性（propsItem）：平台的项容器主题，按项不同时绑定到容器的 DataContext（这一项）
  Avalonia: {
    CheckboxGroup: { propsItem: 'ItemContainerTheme' },
    RadioGroup: { propsItem: 'ItemContainerTheme' },
  },
  WPF: {
    // 同上：项容器的样式，按项不同时用 ItemContainerStyleSelector 或绑定
    CheckboxGroup: { propsItem: 'ItemContainerStyle' },
    RadioGroup: { propsItem: 'ItemContainerStyle' },
    // FrameworkElement 自己有 Language（界面文字的语言）
    CodeBlock: { lang: 'CodeLanguage' },
    // 平台的提示：FrameworkElement.ToolTip 与 ToolTipService
    Tooltip: {
      children: 'ToolTip',
      _open: 'ToolTipService.IsOpen',
      delay: 'ToolTipService.InitialShowDelay',
      disabled: 'ToolTipService.IsEnabled',
      onOpenChange: 'ToolTipOpening',
    },
  },
  WinForms: {
    // 组自己生成控件：生成一项时的事件，处理完之后值由组写回
    CheckboxGroup: { propsItem: 'ItemGenerated' },
    RadioGroup: { propsItem: 'ItemGenerated' },
    Container: { size: 'ContainerSize', padding: 'InlinePadding' },
    Card: { padding: 'CardPadding' },
    // 标题是控件的 Text（同按钮、导航项的文字）
    ItemListItem: { title: 'Text' },
    // 同 TextFieldControl
    Select: { placeholder: 'PlaceholderText' },
    // 控件自己有 Size
    Dialog: { size: 'DialogSize' },
    // 选项是画出来的，没有匹配时只能换说明文字
    CommandPalette: { empty: 'EmptyText' },
    // 扩展组件：每个控件多出的属性前面带 Tooltip（设计器里是"Tooltip 在 tooltipProvider1 上"）
    Tooltip: {
      children: 'Tooltip',
      placement: 'TooltipPlacement',
      arrow: 'TooltipArrow',
      disabled: 'Active',
      _open: 'Current',
      onOpenChange: 'Opened',
    },
  },
};

/** 某个平台上改了名的组件（平台自带同名类型时）：库 → { 组件: 类型名 } */
export const TYPE_RENAMES = {
  // 三个平台都自带 Label：表单标签叫 FieldLabel
  Avalonia: { Label: 'FieldLabel' },
  WPF: { Label: 'FieldLabel' },
  // WinForms 自己有 ListControl（ListBox、ComboBox 的基类）
  // WinForms 的提示是扩展组件（同平台的 ToolTip 组件），不是控件；平台自带 TabControl、TabPage：标签叫 TabItemControl
  WinForms: { List: 'UnorderedList', Container: 'ContainerPanel', Label: 'FieldLabel', Tooltip: 'TooltipProvider', Tab: 'TabItem' },
};

const FORM_NAME = '表单提交的字段名是浏览器的概念；值经 Value 绑定，单选组的互斥由组自己管';

const BUTTON_TYPE = '表单提交是浏览器的概念；对话框的默认、取消按钮用平台的 IsDefault、IsCancel';

/** 某个组件不移植的 prop 与原因（平台上没有对应的概念）：parity 不报缺 */
export const PROPS_NOT_PORTED = {
  Listbox: {
    id: '桌面没有元素 id：选项与列表的关系由读屏树的父子关系表达（TS 用 id 拼 aria-activedescendant）',
    scroller: '列表总是自己滚动：Select 弹出层里的列表同样带自己的滚动区域',
    estimateSize: '虚拟化按实际的行高排（Avalonia、WPF 的虚拟化面板；WinForms 量出每行的高度），不用估计值',
  },
  AppShell: {
    skipLink: '跳到正文的链接是网页的（SkipLink 不做）：桌面上 Tab 顺序就从外壳开始',
    pageScrollbar: '整页的浮层滚动条是网页的：桌面上正文那一列本身是 ScrollArea（AppShell.MainScroll）',
  },
  NavList: { propsList: 'nav > ul 是 HTML 的结构：NavList 本身就是那一列' },
  NavItem: { propsItem: '外层的 li 是 HTML 列表的结构：桌面上导航项本身就是列表里的一行' },
  ItemListItem: Object.fromEntries(['propsLink', 'propsButton'].map((prop) => [prop, '标题里伸展的链接、按钮是网页让整条可点的写法：桌面上整条就是一个可点的控件（Href、Invoked），没有里面那一层'])),
  Menu: {
    point: '从某一点弹出只给右键菜单用：ContextMenu 在指针所在处或获得焦点的元素旁打开（内部的 OpenAt），桌面上没有用触发器之外的坐标打开菜单的场景',
    offset: '与触发器的距离同 Popover（6）：菜单不另设',
    lazy: '菜单项是控件，平台按需应用模板；快捷键要求菜单项一直在（上游有快捷键时也不延迟）',
  },
  ContextMenu: { propsMenu: '里面的菜单是 ContextMenu 建的：名称用 Label，其余同 Menu 的默认（位置跟着指针或焦点）' },
  ToggleGroup: { propsIndicator: '滑块是组自己画的（跟着选中项滑动），没有可改样式的部件' },
  CharacterCount: {
    propsValue: '看得见的字数就是控件自己的文字，读屏读的是控件的名称（完整的句子），不另分部件',
    propsLabel: '看得见的字数就是控件自己的文字，读屏读的是控件的名称（完整的句子），不另分部件',
  },
  Button: { type: BUTTON_TYPE },
  IconButton: { type: BUTTON_TYPE },
  ButtonBase: { type: BUTTON_TYPE },
  CloseButton: { type: BUTTON_TYPE },
  ClearButton: { type: BUTTON_TYPE },
  CopyButton: { type: BUTTON_TYPE },
  UserButton: { type: BUTTON_TYPE },
  Tooltip: { trigger: '提示挂在触发器上（Avalonia、WPF 的 ToolTip 附加属性，WinForms 的扩展属性），不用渲染函数' },
  ScrollArea: { propsContent: '内容就是 Content 本身，平台的滚动呈现器直接排它，中间没有包一层' },
  Table: { propsRoot: '外层的外框与滚动区域由 Table 管：滚动方向是 ScrollAxis（WinForms 外面套 ScrollAreaControl），外观按密度、样式、斑马纹与主题 token' },
  ...Object.fromEntries(['TableHead', 'TableBody', 'TableFoot', 'TableRow', 'TableCaption', 'TableCell', 'TableHeaderCell'].map((name) => [name, { propsRoot: '行、单元格、各部分本身就是元素：样式直接写在它们身上' }])),
  Select: {
    placement: '弹出层固定贴着外壳下方、至少与它同宽（放不下时翻到上方），同平台的下拉框',
    name: FORM_NAME,
    renderValue: '触发框就是控件自己：单选显示已选项的文字，多选显示标签；读屏念全部已选',
    _search: '搜索词只在弹出层的搜索框里；自己发请求的远程搜索等 load 一起做',
    propsRoot: '外壳就是 Select 自己：样式直接写在 Select 上',
    propsTag: '标签是 SelectedTags 建的 sm、soft 的 Tag：样式写在 Tag 的主题或部件上',
  },
  SelectedTags: { propsTag: '标签固定是 sm、soft 的 Tag：样式写在 Tag 的主题或部件上' },
  // 上游没声明的属性落到对话框上：桌面上命令面板的对话框是固定的一种
  CommandPalette: Object.fromEntries(['closable', 'dismissible', 'beforeClose', 'onConfirm', 'confirmText', 'cancelText', 'tone', 'lazy', 'size',
    'propsConfirm', 'propsCancel', 'propsHeader', 'propsTitle', 'propsDescription', 'propsClose', 'propsBody', 'propsFooter']
    .map((prop) => [prop, '面板里的对话框是 CommandPalette 建的固定一种（sm 宽、停在上部、没有标题行与页脚，Esc 与点遮罩关闭），不转交对话框的属性'])),
  ...Object.fromEntries(['Dialog', 'AlertDialog'].map((name) => [name, { lazy: '正文是传进来的控件实例，对话框关着时不在可视树里（不排版、不绘制），本来就是打开时才有代价' }])),
  Popover: {
    lazy: '内容是传进来的控件实例，浮层关着时不在可视树里（不排版、不绘制），本来就是打开时才有代价',
    role: '浮层固定是对话框（Avalonia、WPF 的读屏节点、WinForms 的 AccessibleRole.Dialog）；需要打断操作的确认用对话框组件',
  },
  Tabs: { lazy: '看不见的面板不排版、不绘制（IsVisible / Visible 为 false），本来就是第一次显示时才有代价' },
  Toolbar: { items: '桌面上没有 CSS 选择器：参与方向键移动的固定是按钮与链接' },
  Progress: { propsRoot: '外层就是 Progress 自己（读屏的进度条也是它）：样式直接写在 Progress 上' },
  Switch: { propsRoot: '外层（TS 的 label）就是 Switch 自己：样式直接写在 Switch 上' },
  Checkbox: { propsRoot: '外层（TS 的 label）就是 Checkbox 自己：样式直接写在 Checkbox 上' },
  Radio: { propsRoot: '外层（TS 的 label）就是 Radio 自己：样式直接写在 Radio 上' },
  CheckboxGroup: { name: FORM_NAME },
  RadioGroup: { name: FORM_NAME },
};

/** 某个平台上不移植的 prop 与原因（盖在 PROPS_NOT_PORTED 上）：库 → { 组件: { prop: 原因 } } */
export const PLATFORM_PROPS_NOT_PORTED = {
  Avalonia: {
    ScrollArea: { propsScrollbar: '两条滚动条是平台 ScrollViewer 认的固定部件名：样式里用 ScrollAreaParts.VerticalScrollbar、HorizontalScrollbar 选中' },
  },
  WPF: {
    ScrollArea: { propsViewport: '视口就是 ScrollArea 自己：读屏名称、焦点都在它身上；滚动呈现器没有部件样式' },
    ...Object.fromEntries(['Dialog', 'AlertDialog'].map((name) => [name, Object.fromEntries(['propsHeader', 'propsTitle', 'propsClose', 'propsDescription', 'propsBody', 'propsFooter', 'propsCancel', 'propsConfirm']
      .map((prop) => [prop, '标题行、标题、关闭按钮、说明、正文、页脚、按钮是 Dialog 代码组合的子元素，部件样式键只有面板、遮罩与阴影；外观跟着各自组件的主题样式']))])),
    Select: Object.fromEntries(['propsListbox', 'propsSearch'].map((prop) => [prop, '弹出层里的列表、搜索框是 Select 建的，外观跟着 Listbox、TextField 的主题样式'])),
    CommandPalette: Object.fromEntries(['propsInput', 'propsListbox'].map((prop) => [prop, '面板里的搜索框、列表是 CommandPalette 建的，外观跟着 TextField、Listbox 的主题样式；部件样式键只有搜索行'])),
  },
  WinForms: {
    UserButton: { propsAvatar: '头像是 UserButton 画的 Avatar 元素，WinForms 还没有头像的部件属性记录（AvatarProps）；尺寸是 sm，配色跟着 Avatar 的作用域默认值' },
    SelectedTags: { propsMore: '“+N”同其余标签是 SelectedTagsControl 建的 sm、soft 的 TagControl（同 propsTag），WinForms 没有部件样式' },
    Tabs: { ripple: '标签是组件库画的容器控件（里面放文字与计数），没有按下的波纹' },
    NavItem: { ripple: '导航行是组件库画的容器控件（里面放图标、文字与计数），没有按下的波纹' },
    ItemListItem: Object.fromEntries(['propsTitle', 'propsDescription'].map((prop) => [prop, '标题、说明是 ItemListItemControl 里的 TextControl 子控件，WinForms 没有部件样式；说明固定 textMuted、xs'])),
    Menu: { ripple: '菜单项是组件库画的容器控件（里面放图标、文字、快捷键），没有按下的波纹' },
    ...Object.fromEntries(['MenuItem', 'MenuLinkItem', 'MenuCheckboxItem', 'MenuRadioItem', 'MenuSub'].map((name) => [name, Object.fromEntries(
      ['propsIndicator', 'propsIcon', 'propsLabel', 'propsShortcut', 'propsDescription', 'propsChevron', 'propsPanel']
        .map((prop) => [prop, '标记、图标、文字、说明、快捷键、箭头是菜单项里的 IconControl、TextControl 子控件，子菜单的面板是浮层窗口：WinForms 没有部件样式'])),
    ])),
    ...Object.fromEntries(['MenuGroup', 'MenuRadioGroup'].map((name) => [name, { propsLabel: '分组标题是 MenuGroupControl 里的 SmallControl（textMuted、sm），WinForms 没有部件样式' }])),
    CodeBlock: Object.fromEntries(
      ['propsCode', 'propsRoot', 'propsHeader', 'propsTitle', 'propsBody', 'propsLine', 'propsLineNumber', 'propsCopy', 'propsScroll', 'propsExpand', 'propsExpandIcon']
        .map((prop) => [prop, '外层、标题栏、代码与行号、展开按钮都是 CodeBlockControl 自己画的，没有可改的部件']),
    ),
    ScrollArea: {
      propsViewport: '视口就是 ScrollAreaControl 自己：读屏名称、焦点都在它身上',
      propsScrollbar: '滚动条是组件库画的滑块子控件，没有可改的部件属性',
      propsThumb: '滚动条是组件库画的滑块子控件，没有可改的部件属性',
    },
    Collapse: { lazy: '收起时子控件隐藏：隐藏的控件不建句柄、不参与布局，本来就是第一次展开才真正创建' },
    Tooltip: { propsArrow: '提示窗口整张是组件库画的位图，箭头没有可改样式的部件' },
    Select: {
      children: 'WinForms 的选项是 ListboxControl 画出来的，没有自定义内容',
      empty: '没有选项时是固定的空状态（语言包的"没有选项"、搜索时"没有匹配的选项"）',
      ...Object.fromEntries(['propsValue', 'propsTags', 'propsClear', 'propsChevron', 'propsPopover', 'propsListbox', 'propsSearch', 'propsIcon']
        .map((prop) => [prop, '部件是 SelectControl 组合的子控件与两层窗口，没有部件属性记录'])),
    },
    ...Object.fromEntries(['Dialog', 'AlertDialog'].map((name) => [name, Object.fromEntries(['propsHeader', 'propsTitle', 'propsClose', 'propsDescription', 'propsBody', 'propsFooter', 'propsCancel', 'propsConfirm']
      .map((prop) => [prop, '标题行、正文、页脚是 DialogControl 组合的子控件，面板与遮罩是两层窗口画的，没有部件属性记录']))])),
    CommandPalette: {
      renderOption: 'WinForms 的选项是 ListboxControl 画出来的，没有自定义内容',
      ...Object.fromEntries(['propsTrigger', 'propsInput', 'propsListbox'].map((prop) => [prop, '默认触发按钮、搜索框、列表是 CommandPaletteControl 组合的子控件，没有部件属性记录'])),
    },
    Tag: { propsRemove: '移除按钮是 CloseButtonControl 子控件，颜色、字号跟着标签；没有部件属性记录' },
    Alert: Object.fromEntries(['propsSpinner', 'propsBody', 'propsContent', 'propsAction', 'propsClose']
      .map((prop) => [prop, '加载指示、操作、关闭按钮与额外内容是 AlertControl 的子控件，文字区是排版出来的一块区域：没有部件属性记录（图标、标题、说明是画的元素，有 PropsIcon、PropsTitle、PropsDescription）'])),
    Tab: { propsClose: '关闭按钮是 TabItemControl 里的 CloseButtonControl 子控件（xs、圆形），没有部件属性记录' },
    TabList: {
      propsIndicator: '分隔线与指示条是 TabListControl 自己画的，没有可改的部件',
      propsBar: '标签栏就是 TabListControl 自己（横排时自己排滚动按钮、列表、菜单与新建按钮），没有另一层',
      ...Object.fromEntries(['propsAdd', 'propsScrollButton'].map((prop) => [prop, '新建、滚动按钮是 TabListControl 建的 IconButtonControl 子控件，外观走 ProviderControl 的组件定制，没有部件属性记录'])),
    },
    Popover: { propsArrow: '面板、阴影与箭头是组件库画在分层窗口上的位图，箭头没有可改样式的部件' },
    Listbox: Object.fromEntries(
      ['empty', 'propsOption', 'propsGroupLabel', 'propsCheck', 'propsEmpty', 'propsSkeleton']
        .map((prop) => [prop, '选项、组名、已选标记是 ListboxControl 画出来的，空状态与骨架是固定的子控件，没有部件属性与自定义内容']),
    ),
    TextField: Object.fromEntries(
      ['propsRoot', 'propsIcon', 'propsIconEnd', 'propsPrefix', 'propsSuffix', 'propsCount', 'propsClear', 'propsReveal']
        .map((prop) => [prop, '外壳是 TextInputControl 自己画的，里面的图标、前后缀、按钮、字数是子控件，没有部件属性：外观跟主题，按钮走 ProviderControl 的组件定制']),
    ),
    Textarea: Object.fromEntries(
      ['propsRoot', 'propsCount', 'propsClear']
        .map((prop) => [prop, '外壳是 TextInputControl 自己画的，清空按钮、字数是子控件，没有部件属性：外观跟主题，按钮走 ProviderControl 的组件定制']),
    ),
    AppShell: Object.fromEntries(
      ['propsHeader', 'propsBrand', 'propsHeaderContent', 'propsBody', 'propsSidebar', 'propsSidebarScroll', 'propsMain', 'propsNav', 'propsFooter',
        'propsCollapse', 'propsAside', 'propsAsideScroll', 'propsMenuButton', 'propsFullscreenButton', 'propsFullscreenExit', 'propsDrawer']
        .map((prop) => [prop, '各格是 AppShellControl 自己排、自己画的（底色、细线跟主题），按钮的外观走 ProviderControl 的组件定制，没有部件属性可加']),
    ),
  },
};

/** 平台控件自带、不用组件自己声明的属性（基类里已经有；ItemsSource 是 Avalonia、WPF 的 ItemsControl 的） */
export const PLATFORM_PROPS = new Set([
  'IsEnabled',
  'Content',
  'Tag',
  'AutomationProperties.Name',
  'ItemsSource',
  // ItemsControl 的项容器主题（Avalonia）、样式（WPF）：组的 propsItem
  'ItemContainerTheme',
  'ItemContainerStyle',
  // 平台的提示：Avalonia 的 ToolTip.*，WPF 的 ToolTip 与 ToolTipService.*（WPF 的映射见 PLATFORM_COMPONENT_PROP_RENAMES）
  'ToolTip.Tip',
  'ToolTip.IsOpen',
  'ToolTip.ShowDelay',
  'ToolTip.ServiceEnabled',
  'ToolTip.ToolTipOpening',
  'ToolTip',
  'ToolTipService.IsOpen',
  'ToolTipService.InitialShowDelay',
  'ToolTipService.IsEnabled',
  'ToolTipOpening',
  // 按钮类（菜单项）的点击是平台按钮、控件的 Click
  'Click',
  // 文本框从平台的 TextBox 派生：值、只读、长度上限是平台的
  'Text',
  'IsReadOnly',
  'MaxLength',
]);

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
/** 只在某个平台上不适用的上游测试用例：库 → { 组件: { 用例名的开头: 原因 } } */
export const PLATFORM_BEHAVIOR_NOT_APPLICABLE = {
  WinForms: {
    ItemList: { 'ItemList：列表是 root，条目是 item': '标题、说明是 ItemListItemControl 里的子控件，没有部件标记（PLATFORM_PROPS_NOT_PORTED）' },
    ScrollArea: { '内容层、滚动条、滑块都能加属性': '滚动条是组件库画的滑块子控件，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）' },
    CodeBlock: {
      '部件属性': '代码块是 CodeBlockControl 自己画的，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）',
    },
    AppShell: { '部件属性': '各格是 AppShellControl 自己排、自己画的，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）' },
  },
};

export const BEHAVIOR_NOT_APPLICABLE = {
  TextField: { 'TextField：前后缀是部件，不算在值里；是指向输入框的 label': '桌面的读屏名称不拼前后缀（网页是 label 指向输入框才念进去）：读屏名称取字段的标签，前后缀是显示用的固定文字，不进值里' },
  CharacterCount: { 'CharacterCount 的部件属性': '看得见的字数就是控件自己的文字，读屏读的是控件的名称（完整的句子），不另分部件（PROPS_NOT_PORTED）' },
  Tabs: {
    '只当导航用（没有 TabPanel）时标签不写 aria-controls': '桌面的读屏没有 aria-controls：标签与面板的关系由面板的名称（对应标签的文字）表达',
    'lazy：面板第一次显示时才渲染内容': '看不见的面板不排版、不绘制，本来就是懒的（PROPS_NOT_PORTED）',
  },
  LinkButton: { '站内链接经 router 跳转': '桌面没有站内地址：Href 是绝对地址，用系统打开（ROADMAP 平台化的约定）' },
  IconLinkButton: { '站内链接经 router 跳转': '桌面没有站内地址：Href 是绝对地址，用系统打开（ROADMAP 平台化的约定）' },
  Flex: { createFlex: '桌面没有语义标签：别的语义容器从 Flex 派生、改读屏角色（FlexList、FlexOrderedList 就是这样，测试覆盖）' },
  Container: { createContainer: '桌面没有语义标签：要别的语义时从 Container 派生' },
  Grid: { createGrid: '桌面没有语义标签：别的语义容器从 Grid 派生、改读屏角色（GridList 就是这样，测试覆盖）' },
  Collapse: { '页内搜索命中': '页内搜索命中收起的内容时展开（hidden="until-found"、beforematch）是浏览器的能力，桌面没有页内搜索' },
};

/** 像素对照（pixels.mjs）默认允许的差异比例：未盖掉文字的像素里，不同的占多少以内算一致 */
export const DEFAULT_PIXEL_ALLOWANCE = 0.005;

/**
 * 像素对照里允许更大差异的示例与原因：平台 → 页面 → 示例 → { ratio, reason }；平台写 all 时对三个平台都算。
 * 只登记查清了原因、决定不改的差异（平台画法的极限、上游示例里有平台没有的内容），改得了的直接改
 */
export const PIXEL_ALLOWANCE = {};
