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
  // 控件都有 Name（元素名）：字段路径叫 Path；valueProp 是要绑定的控件属性名
  Field: { name: 'Path', valueProp: 'BoundProperty' },
  FormSection: { collapsible: 'IsCollapsible' },
  // 表单与错误汇总是对话框里的部件实例：直接改它们的属性
  FormDialog: { propsForm: 'Form', propsSummary: 'Summary' },
  // 多行文本框的值是平台 TextBox 的 Text（TextField 有 format，值另是 Value：有格式时是去掉分隔的原文）
  Textarea: { _value: 'Text' },
  // 数据是平台的 ItemsSource；点选是事件
  Listbox: { items: 'ItemsSource', onPick: 'Picked' },
  // Text 是 TextBlock 自己的属性
  CodeText: { text: 'Source' },
  // Measure 是布局的方法
  Prose: { measure: 'LimitLineLength' },
  // 每页条数本身叫 PageSize，换了条数的事件跟着叫 PageSizeChanged
  PageSizeSelect: { onSizeChange: 'PageSizeChanged' },
  // Search 是立即搜索的方法，事件叫 SearchRequested
  SearchField: { onSearch: 'SearchRequested' },
  // Name 是控件名；图片给平台的图片对象
  Avatar: { name: 'DisplayName', src: 'Source' },
  // 地址是 Uri（同 Avatar 的 Source）；组共用的预览直接是 Lightbox 实例（改它的属性、听它的事件）
  Image: { src: 'Source' },
  // 区域的高度（控件自己的 Height 包括工具条）、裁剪框离边的距离（Padding 是平台的内边距）
  ImageCropper: { src: 'Source', height: 'ViewportHeight', padding: 'FrameInset' },
  // 控件自己有 MinWidth、MaxWidth（排版的宽度范围）：笔宽的范围加 Pen
  SignaturePad: { minWidth: 'PenMinWidth', maxWidth: 'PenMaxWidth' },
  ImageGroup: { propsLightbox: 'Lightbox' },
  // tooltip 的 false 是 ShowTip = false；速拨的主按钮直接是 FloatButton 实例
  FloatButton: { tooltip: 'Tip' },
  // onChange 报告贴住与否：事件叫 StuckChanged，看 IsStuck
  Affix: { onChange: 'StuckChanged' },
  // 点“取消选择”只报告，清空选择由使用方做
  SelectionBar: { onClear: 'ClearRequested' },
  // 数据经 ItemsSource 给（平台的叫法），一张的模板是 ItemTemplate
  Carousel: { items: 'ItemsSource' },
  // 展开、选中、勾选的是 key 的列表（不是布尔）；isDisabled 是判断节点的函数
  // 数据模板是 ItemTemplate（WinForms 是建控件的 ItemFactory）；方向用平台的 Orientation；可见范围变了是事件
  VirtualList: { items: 'ItemsSource', children: 'ItemTemplate', horizontal: 'Orientation', onRangeChange: 'RangeChanged' },
  // 完成是事件；镂空的留白叫 SpotlightPadding（控件自己有 Padding）
  Tour: { onFinish: 'Finished', padding: 'SpotlightPadding' },
  // 看板：放下、弹回、列内排序都是事件；columnHeight 是最大高度
  KanbanBoard: { items: 'ItemsSource', onMove: 'Moved', onReorder: 'Reordered', onReject: 'Rejected', columnHeight: 'ColumnMaxHeight', children: 'ItemTemplate' },
  // 各项是可改的列表，顺序就地改（ObservableCollection 用 Move）
  Sortable: { _items: 'ItemsSource', children: 'ItemTemplate' },
  // noMore 是文字（false 不显示是 ShowNoMore）
  InfiniteListStatus: { noMore: 'NoMoreText' },
  // 控件自己的 MaxHeight 包括工具条与路径栏：上游的 maxHeight 只管树
  JsonViewer: { maxHeight: 'TreeMaxHeight', toolbar: 'ShowToolbar' },
  Tree: { items: 'ItemsSource', isDisabled: 'IsItemDisabled', _expanded: 'Expanded', _selected: 'Selected', _checked: 'Checked', onMove: 'Moved', renderItem: 'ItemTemplate' },
  FloatButtonGroup: { propsTrigger: 'Trigger' },
  UserButton: { name: 'DisplayName', src: 'Source' },
  // 地址给 Uri；加载完的事件不叫 Loaded（那是控件放进界面的事件）
  Embed: { src: 'Source', onLoad: 'PageLoaded' },
  // Controls 在 WinForms 是子控件集合：三个平台统一叫 ControlBar（Native、Custom、None 对应 true、'custom'、false）
  Video: { src: 'Source', controls: 'ControlBar' },
  // 文字多行用换行分开（TS 是字符串数组）；Width、Height 是控件自己的大小，图片的大小另起名字
  Watermark: { content: 'Text', width: 'ImageWidth', height: 'ImageHeight' },
  // 布尔状态按平台习惯带 Is
  Marquee: { paused: 'IsPaused' },
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
  // 输入框就是平台 TextBox：没确认的文字是 Text；已选的值是 Values（同多选 Select）
  TagsInput: { _value: 'Values', items: 'ItemsSource', _inputValue: 'Text', children: 'ItemTemplate' },
  // 输入框就是平台 TextBox：输入的文字是 Text，选中项的值是 Value
  Combobox: { items: 'ItemsSource', _inputValue: 'Text', children: 'ItemTemplate' },
  // 值就是输入框里的文字：平台 TextBox 的 Text
  Autocomplete: { _value: 'Text', items: 'ItemsSource', children: 'ItemTemplate' },
  // 平台的 ComboBox 自己有 Items / ItemsSource（选项行由 Options 生成）与 PlaceholderText
  NativeSelect: { items: 'Options', placeholder: 'PlaceholderText' },
  // C# 的事件用过去式（同 Canceled、Closed），不和取消按钮的部件名 Cancel 重名
  Popconfirm: { onCancel: 'Canceled' },
  // 到点是事件
  Countdown: { onEnd: 'Ended' },
  // 一次操作结束是事件
  Slider: { onValueCommit: 'ValueCommitted' },
  // 收起是布尔属性，按 C# 的习惯带 Is
  Splitter: { _collapsed: 'IsCollapsed' },
  // 手柄改的是父元素的宽度：Width、MinWidth、MaxWidth 是平台控件自己的布局属性，这里叫 Length（横线时是高度）；调整了是事件
  ResizeHandle: { width: 'Length', minWidth: 'MinLength', maxWidth: 'MaxLength', defaultWidth: 'DefaultLength', onWidthChange: 'LengthChanged' },
  // 值是平台 TextBox 的 Text；字符集叫 Charset（Type 是 TextField 的输入类型）；填满是事件
  OtpField: { _value: 'Text', type: 'Charset', onComplete: 'Completed' },
  // 点选了一天是事件
  Calendar: { onPick: 'Picked' },
  RangeCalendar: { onPick: 'Picked' },
  // 选择方式同 Calendar 叫 SelectionMode（值的两种形式分成 Value、RangeValue）；选好了是事件
  QuarterCalendar: { mode: 'SelectionMode', onPick: 'Picked' },
  // 边长是平台的宽度；Margin 是平台的外边距，静区叫 QuietZone；颜色是平台的前景、背景画刷
  QRCode: { size: 'Width', margin: 'QuietZone', color: 'Foreground', bgColor: 'Background' },
  // Intl 的逐项选项是 .NET 的格式字符串（按语言包的文化）
  DateTime: { options: 'Format' },
  // 数据是平台的 ItemsSource；执行了一项是事件；输入框的文字同 TextBox 叫 Text；自定义选项内容是数据模板
  CommandPalette: { items: 'ItemsSource', onAction: 'ItemInvoked', _inputValue: 'InputText', renderOption: 'ItemTemplate' },
  // 平台的 ItemsControl 自己有 Items（直接写的项）；数据给 ItemsSource
  CheckboxGroup: { items: 'ItemsSource' },
  RadioGroup: { items: 'ItemsSource' },
  // 平台控件自己有 Focusable（能不能聚焦）：内容超出时才可聚焦的开关另起名
  ScrollArea: { focusable: 'IsViewportFocusable' },
  // 代码的语言：lang 是缩写，C# 写全
  CodeBlock: { lang: 'Language' },
  // 同 CodeBlock；显示源码是布尔的状态，按 Is 前缀
  CodePreview: { lang: 'Language', _showCode: 'IsCodeShown' },
  // Table 自己有 Rows（表体的行）；DocSection 的 id 是锚点，控件自己有 Name
  TextTable: { rows: 'TextRows' },
  DocSection: { id: 'SectionId' },
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
    NativeSelect: { placeholder: 'ComboBox.PlaceholderText' },
    CheckboxGroup: { propsItem: 'ItemContainerTheme' },
    RadioGroup: { propsItem: 'ItemContainerTheme' },
  },
  WPF: {
    // 同上：项容器的样式，按项不同时用 ItemContainerStyleSelector 或绑定
    CheckboxGroup: { propsItem: 'ItemContainerStyle' },
    RadioGroup: { propsItem: 'ItemContainerStyle' },
    // FrameworkElement 自己有 Language（界面文字的语言）
    CodeBlock: { lang: 'CodeLanguage' },
    CodePreview: { lang: 'CodeLanguage' },
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
    // 控件自己有 Size（边长）与 Refresh 方法
    QRCode: { size: 'Size', onRefresh: 'RefreshRequested' },
    // 控件自己有 Layout 事件
    FieldCompare: { layout: 'CompareLayout' },
    // 张片是控件，直接放进 Slides
    Carousel: { items: 'Slides' },
    // 每一项是控件，由函数建（没有数据模板）
    VirtualList: { children: 'ItemFactory' },
    Sortable: { children: 'ItemFactory' },
    // 卡片内容由函数建；空列只能换文字
    KanbanBoard: { children: 'ItemFactory', empty: 'EmptyText' },
    // 控件自己有 HasChildren；节点是画出来的，没有匹配时只能换说明文字
    Tree: { hasChildren: 'HasChildNodes', empty: 'EmptyText' },
    // 控件自己有 Size：起始一栏的百分比叫 Ratio，范围跟着叫 MinRatio、MaxRatio
    Splitter: { _size: 'Ratio', minSize: 'MinRatio', maxSize: 'MaxRatio' },
    // 组自己生成控件：生成一项时的事件，处理完之后值由组写回
    CheckboxGroup: { propsItem: 'ItemGenerated' },
    RadioGroup: { propsItem: 'ItemGenerated' },
    Container: { size: 'ContainerSize', padding: 'InlinePadding' },
    Card: { padding: 'CardPadding' },
    // 控件自己有 Layout 事件
    Descriptions: { layout: 'ItemLayout' },
    Checklist: { layout: 'ItemLayout' },
    // 横条的粗细不是字号档位（同名的 TextSize 会误导）
    ProportionBar: { size: 'BarSize' },
    // 标题是控件的 Text（同按钮、导航项的文字）
    ItemListItem: { title: 'Text' },
    // 同 TextFieldControl
    Select: { placeholder: 'PlaceholderText' },
    // 控件自己有 Size
    Dialog: { size: 'DialogSize' },
    FormDialog: { size: 'DialogSize' },
    // 同上；面板宽度也不能用控件自己的 Width、MinWidth（抽屉不参与排版，但 WinForms 的控件尺寸照样存在、会被设计器序列化）
    Drawer: { size: 'DialogSize', _width: 'PanelWidth', minWidth: 'MinPanelWidth' },
    // 选项是画出来的，没有匹配时只能换说明文字（同 CommandPalette）
    Combobox: { empty: 'EmptyText' },
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
  // 三个平台都有 System.DateTime：日期时间的文字叫 DateTimeText
  // Toast.tsx 的单元测试（文件名 Toast）测的是控制器 createToaster：桌面上是 ToasterController
  Avalonia: { 'form-dialog': 'FormDialog', 'form-binding': 'Field', 'date-segments': 'DateSegments', 'calendar-state': 'CalendarState', Label: 'FieldLabel', DateTime: 'DateTimeText', Toast: 'ToasterController' },
  WPF: { 'form-dialog': 'FormDialog', 'form-binding': 'Field', 'date-segments': 'DateSegments', 'calendar-state': 'CalendarState', Label: 'FieldLabel', DateTime: 'DateTimeText', Toast: 'ToasterController' },
  // WinForms 自己有 ListControl（ListBox、ComboBox 的基类）
  // WinForms 的提示是扩展组件（同平台的 ToolTip 组件），不是控件；平台自带 TabControl、TabPage：标签叫 TabItemControl
  WinForms: { 'form-dialog': 'FormDialog', 'form-binding': 'Field', 'date-segments': 'DateSegments', 'calendar-state': 'CalendarState', List: 'UnorderedList', Container: 'ContainerPanel', Label: 'FieldLabel', Tooltip: 'TooltipProvider', Tab: 'TabItem', DateTime: 'DateTimeText', Toast: 'ToasterController' },
};

const FORM_NAME = '表单提交的字段名是浏览器的概念；值经 Value 绑定，单选组的互斥由组自己管';

const BUTTON_TYPE = '表单提交是浏览器的概念；对话框的默认、取消按钮用平台的 IsDefault、IsCancel';

/** 某个组件不移植的 prop 与原因（平台上没有对应的概念）：parity 不报缺 */
export const PROPS_NOT_PORTED = {
  Calendar: { valueType: '值是 ISO 字符串的 Value（多选是 Values），另有 DateOnly 的 Date 属性：没有 @internationalized/date 的日期值类型，不按传进来的值推类型' },
  QuarterCalendar: Object.fromEntries(['propsPrevious', 'propsNext', 'propsHeader', 'propsHeading', 'propsGrid', 'propsCell', 'propsCellButton']
    .map((prop) => [prop, '部件同 CalendarView（上游同一个配方）：Avalonia 是 CalendarViewParts 的部件名（样式选择器 :is(rune|CalendarSurface)），WPF 是 CalendarViewParts 的部件样式键，WinForms 的四格是自己画的 QuarterGridControl；按季度给的部件属性没有对应，格子的外观按状态写样式'])),
  RangeCalendar: { valueType: '值是 IsoDateRange（两端 ISO 字符串），另有日期的 Range 属性：没有 @internationalized/date 的日期值类型，不按传进来的值推类型' },
  ...Object.fromEntries(['SegmentedField', 'DateField', 'TimeField', 'DatePicker', 'DateRangePicker'].map((name) => [name, {
    name: FORM_NAME,
    propsSegment: '分段是里面的 DateSegmentGroup：样式写在每段的 DateSegmentBox 与分隔符 Text#PART_Literal 上',
    propsLiteral: '分段是里面的 DateSegmentGroup：样式写在每段的 DateSegmentBox 与分隔符 Text#PART_Literal 上',
    ...(name === 'SegmentedField'
      ? { outOfRange: '超出范围由派生类覆盖 IsOutOfRange 判断（DateField、TimeField 按 Min、Max 与不可选的日期时间）' }
      : { valueType: '值是 ISO 字符串的 Value，另有类型化的属性（DateField 的 Date、DateTime，TimeField 的 Time）：不按传进来的值推类型' }),
    ...(name === 'DateRangePicker'
      ? {
        valueType: '值是 IsoDateRange（两端 ISO 字符串）的 Value，另有日期的 Range 属性：没有 @internationalized/date 的日期值类型，不按传进来的值推类型',
        startName: FORM_NAME,
        endName: FORM_NAME,
        propsStart: '开始那组是 SegmentedFieldParts.Input（同单个分段输入框的主元素），结束那组是 SegmentedFieldParts.End',
      }
      : {}),
    ...(name === 'DatePicker' || name === 'DateRangePicker'
      ? Object.fromEntries(['propsButton', 'propsPopover', 'propsPresets', 'propsPreset', 'propsToday', 'propsCalendar']
        .map((prop) => [prop, '弹层里的元素是打开时建的：Avalonia 的部件名在 PickerParts（样式选择器 rune|SegmentedField，选择器的样式键是外壳的），WPF 是 PickerParts 的部件样式键，WinForms 的按钮与弹层内容是组合的子控件，没有部件属性记录；日历的选项由选择器给（粒度、范围、并排月数）']))
      : {}),
  }])),
  DateSegmentGroup: {
    labelledBy: '读屏名称不靠 id 关联：每段的名称是这一组的 AutomationProperties.Name（WinForms 的 AccessibleName）加段名，外壳控件（DateField）把标签写在这一组上',
    describedBy: '说明与错误由外壳控件（DateField）作为这一组的帮助文字给读屏，不靠 id 关联',
  },
  OtpField: {
    propsRoot: '格子的容器是 OtpField 自己（模板里的格子与透明的输入框）：样式直接写在它上面',
    ...Object.fromEntries(['propsSlot', 'propsSeparator'].map((prop) => [prop, '格子与分隔是画出来的（Avalonia、WPF 的 OtpSlots，WinForms 的 OtpFieldControl），外壳的颜色同输入框的主题；没有每格的元素可加属性'])),
  },
  QRCode: {
    propsCode: '模块是 QRCode 自己画的一条路径（同 TS 的单条 path），没有元素可加属性；要自己处理时用 QrEncoder.Encode 的矩阵或 QrEncoder.Svg',
  },
  Splitter: {
    propsScroll: '两栏本身就是 ScrollArea（部件 PART_Start、PART_End），滚动区域的属性用样式写在它们上面',
  },
  Slider: {
    name: FORM_NAME,
    propsRoot: '外层就是 Slider 自己：样式直接写在它上面',
    ...Object.fromEntries(['propsTrack', 'propsRange', 'propsMark'].map((prop) => [prop, '轨道、已选范围与刻度点是 Slider 自己画的（色调、字号跟着 Tone、Size），没有元素可加属性；滑块是 SliderThumb（WinForms 是读屏子节点）'])),
    propsMarkLabel: '刻度文字是 Slider 建的 Text（sm、textMuted），外观跟着 Text 的主题样式',
  },
  Rating: {
    name: FORM_NAME,
    propsStar: '星是 Rating 自己画的（空心描线打底、按比例裁出的实色），没有每颗星的元素；分数的单选项是透明的点击区域（Avalonia、WPF 的 RatingOption，WinForms 的读屏子节点）',
  },
  NativeSelect: {
    propsRoot: '外壳就是 NativeSelect 自己（平台 ComboBox 的模板）：样式直接写在它上面',
    ...Object.fromEntries(['propsChevron', 'propsIcon'].map((prop) => [prop, 'Avalonia、WPF 的下拉箭头、前置图标是模板部件（PART_Chevron、PART_Icon），在主题里选中它们；WinForms 的箭头是系统画的'])),
  },
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
  Collapsible: {
    lazy: '内容是传进来的控件实例：收起后隐藏、不在 Tab 顺序与读屏里，本来就是展开时才有代价（同 Collapse）',
    propsContent: '内容区就是里面的 Collapse，没有另包一层：要改样式写在内容控件上',
  },
  AccordionItem: { propsHeading: '没有单独的标题元素：标题层级写在标题按钮的读屏属性上（Accordion 的 HeadingLevel）' },
  Embed: {
    propsRoot: '外层就是 Embed 自己（定比例的框）：样式直接写在 Embed 上',
    propsPlaceholder: '加载中的骨架是内部的 Skeleton（image 形状）：外观跟主题',
  },
  Marquee: Object.fromEntries(['propsTrack', 'propsGroup']
    .map((prop) => [prop, '做动画的那一层、每一份内容是内部的面板（复制的那份是画出来的），没有部件属性：间距用 Gap'])),
  HoverCard: { lazy: '内容是传进来的控件实例：关着时在弹出层里不显示、不排版，本来就是打开时才有代价（同 Popover）' },
  Popconfirm: { lazy: '问题、说明与按钮是 Popconfirm 建的控件：关着时在弹出层里不显示、不排版，本来就是打开时才有代价（同 Popover）' },
  Watermark: { propsLayer: '水印层是内部画出来的一层（Avalonia 的子元素或窗口浮层里的元素、WPF 的子元素或装饰、WinForms 的分层窗口），只画不交互，外观由 Color、FontSize 等属性决定' },
  Video: {
    propsRoot: '外层就是 Video 自己（定比例的黑底框）：样式直接写在 Video 上',
    ...Object.fromEntries(['propsControls', 'propsButton', 'propsSeek', 'propsVolume'].map((prop) => [prop, '自绘控制条画在网页内核的页面里（HTML 元素），外观跟主题的颜色与字号，没有部件属性'])),
  },
  Image: {
    sources: 'picture 的备选源（按格式回退、按媒体条件换图）是浏览器的响应式图片：桌面上一个地址就是一张图，按尺寸换图由调用方换 Source',
    propsRoot: '外层就是 Image 自己（能预览时它就是按钮）：样式直接写在 Image 上',
  },
  FloatButtonGroup: { propsActions: '速拨展开的那一列是组内部的 Flex（Avalonia 的部件名 PART_Actions），按钮直接放进组里：间距、对齐跟组' },
  FieldCompare: {
    ...Object.fromEntries(['propsTable', 'propsList', 'propsItem', 'propsLabel', 'propsValue']
      .map((prop) => [prop, '表格、列表与里面的格子是 FieldCompare 按行生成的（表格是紧凑的 Table）：外观跟主题，要别的样子用字段的 Cell 换显示的内容'])),
  },
  VirtualList: {
    getKey: '给 Vue 对比列表、按 key 缓存测量结果用：Avalonia、WPF 的虚拟化面板按容器回收；WinForms 换数据时重建、按序号量',
    handle: '滚动方法直接是控件上的 ScrollToIndex、ScrollToOffset（ScrollAlign 对齐）',
    propsItem: '每一项的外层是组件管的读屏列表项（同级位置与总数），外观由 ItemTemplate / ItemFactory 给',
  },
  Tree: {
    estimateSize: '虚拟化按实际的行高排（Avalonia、WPF 的虚拟化面板；WinForms 每行一样高），不用估计值',
    propsRow: '行就是 TreeItem 自己（Avalonia 的 TreeParts.Item 选中它，WPF 在主题里写 TreeItem 的样式）；WinForms 的行是画出来的',
    propsScrollArea: '滚动区域是树自己的（Avalonia、WPF 是列表模板里的 ScrollViewer，WinForms 是 ScrollAreaControl），外观跟主题',
  },
  Carousel: { getKey: '给 Vue 对比列表用的键：桌面上按 ItemsSource 的顺序重建每一张，没有按键复用' },
  SignaturePad: {
    name: FORM_NAME,
    handle: '方法直接在控件上：ToPng（PNG 字节）、ToSvg、Undo、Clear',
    ...Object.fromEntries(['propsUndo', 'propsClear'].map((prop) => [prop, '撤销、清空是 SignaturePad 建的 IconButton（sm、ghost、中性色），没有部件属性：外观跟主题'])),
  },
  Toaster: {
    ...Object.fromEntries(['propsList', 'propsItem', 'propsToast']
      .map((prop) => [prop, '列表、每一项与每条通知（Alert）是 Toaster 按控制器生成的：外观跟主题，按通知不同的内容由 ToastOptions 给（色调、图标、操作）'])),
    propsCollapse: '离场时收起高度由每一项自己做（没有单独的 Collapse 元素）',
  },
  ToastItem: Object.fromEntries(['toast', 'controller', 'fromTop', 'fallbackFocus', 'depth', 'progress', 'propsItem', 'propsToast', 'propsProgress', 'propsCollapse']
    .map((prop) => [prop, 'ToastItem 由 Toaster 按控制器的通知生成、不单独使用：这些是 Toaster 交给它的'])),
  ImageCropper: {
    handle: '方法直接在控件上：ToBitmap（TS 的 toCanvas）、Encode（toBlob、toFile：PNG 或 JPEG 的字节，存文件、交给上传由调用方做）、Reset',
    propsImage: '图片、遮罩与裁剪框是区域自己画的（不是元素）：没有 img 的跨域属性，外观跟主题',
    propsButton: '工具条的按钮是 ImageCropper 建的 IconButton（sm、ghost、中性色），没有部件属性：外观跟主题',
  },
  Lightbox: { propsButton: '工具栏与两侧的按钮是 Lightbox 建的 IconButton（深色底上用 onScrim 的颜色），没有部件属性：外观跟主题' },
  Abbr: { propsTooltip: '提示就是平台的 ToolTip：位置、箭头写在 Abbr 自己身上（Avalonia、WPF 的 Tooltip.Placement、Tooltip.Arrow 附加属性）；WinForms 自带的提示用默认的位置' },
  Pagination: { propsItem: '外层的 li 是 HTML 列表的结构：桌面上页码按钮、省略号直接排在列表里' },
  NumberField: {
    valueType: '值固定是 decimal?：十进制精确，金额、大整数不经过浮点数，不必按场景选 number 或 bigint；要别的类型时调用方转换',
    name: FORM_NAME,
  },
  Steps: { propsConnector: '连接线由 Steps 按圆圈的位置自己画（横竖两种画法统一；WinForms 的子控件是各自的窗口，会盖住父级画的东西），不是一个元素' },
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
  AnimatedNumber: { propsLabel: '给读屏的终值不是一段隐藏的文字：AnimatedNumber 的读屏名称就是前缀、终值与后缀（peer / AccessibleObject）' },
  Tooltip: { trigger: '提示挂在触发器上（Avalonia、WPF 的 ToolTip 附加属性，WinForms 的扩展属性），不用渲染函数' },
  ScrollArea: { propsContent: '内容就是 Content 本身，平台的滚动呈现器直接排它，中间没有包一层' },
  Table: { propsRoot: '外层的外框与滚动区域由 Table 管：滚动方向是 ScrollAxis（WinForms 外面套 ScrollAreaControl），外观按密度、样式、斑马纹与主题 token' },
  TextTable: { propsRoot: 'TextTable 本身就是 Table：同 Table 的 propsRoot' },
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
  TagsInput: {
    placement: '弹出层固定贴着外壳下方、至少与它同宽（放不下时翻到上方），同 Select',
    name: FORM_NAME,
  },
  ...Object.fromEntries(['Combobox', 'Autocomplete'].map((name) => [name, {
    placement: '弹出层固定贴着外壳下方、至少与它同宽（放不下时翻到上方），同 Select',
    name: FORM_NAME,
  }])),
  // 上游没声明的属性落到对话框上：桌面上命令面板的对话框是固定的一种
  CommandPalette: Object.fromEntries(['closable', 'dismissible', 'beforeClose', 'onConfirm', 'confirmText', 'cancelText', 'tone', 'lazy', 'size',
    'propsConfirm', 'propsCancel', 'propsHeader', 'propsTitle', 'propsDescription', 'propsClose', 'propsBody', 'propsFooter']
    .map((prop) => [prop, '面板里的对话框是 CommandPalette 建的固定一种（sm 宽、停在上部、没有标题行与页脚，Esc 与点遮罩关闭），不转交对话框的属性'])),
  ...Object.fromEntries(['Dialog', 'AlertDialog', 'Drawer', 'FormDialog'].map((name) => [name, { lazy: '正文是传进来的控件实例，对话框关着时不在可视树里（不排版、不绘制），本来就是打开时才有代价' }])),
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
    DateSegmentGroup: { propsSegment: '每段是 DateSegmentBox：用类型选择器 rune|DateSegmentBox（伪类 :placeholder、:focus）写样式' },
    VirtualList: { overscan: '平台的虚拟化面板自己决定多建几项（VirtualizingStackPanel 没有可设的前后余量）' },
    ScrollArea: { propsScrollbar: '两条滚动条是平台 ScrollViewer 认的固定部件名：样式里用 ScrollAreaParts.VerticalScrollbar、HorizontalScrollbar 选中' },
  },
  WPF: {
    Tour: Object.fromEntries(['propsSpotlight', 'propsPanel', 'propsCover', 'propsTitle', 'propsContent', 'propsFooter', 'propsProgress', 'propsSkip', 'propsPrevious', 'propsNext']
      .map((prop) => [prop, '遮罩、卡片、标题、说明、步数与按钮是 Tour 代码组合的，外观跟 token 与 H2、Text、Button 的主题样式：没有部件样式键；下一步先不可用用 CanGoNext'])),
    KanbanBoard: Object.fromEntries(['propsBoard', 'propsColumn', 'propsHeader', 'propsCount', 'propsReason', 'propsList', 'propsEmpty', 'propsCard', 'propsHandle']
      .map((prop) => [prop, '各列（KanbanColumnView）、列头、卡片（KanbanCard）与把手是 KanbanBoard 代码组合的，外观跟 token 与 Badge、ButtonBase 的主题样式：没有部件样式键'])),
    Sortable: Object.fromEntries(['propsItem', 'propsHandle', 'propsContent']
      .map((prop) => [prop, '每一项（SortableItem）、把手（ButtonBase）与内容是 Sortable 代码组合的，外观跟 token 与 ButtonBase 的主题样式：没有部件样式键'])),
    VirtualList: {
      overscan: '平台的虚拟化面板自己决定多建几项（VirtualizingStackPanel 按 CacheLength，组件不另设）',
      propsSkeleton: '骨架行是 VirtualList 代码组合的 Skeleton，外观跟 Skeleton 的主题样式：没有部件样式键',
    },
    InfiniteListStatus: { propsRetry: '重试按钮是 InfiniteListStatus 组合的 Button（sm、ghost），外观跟 Button 的主题样式：没有部件样式键' },
    JsonViewer: Object.fromEntries(['propsSearch', 'propsTree', 'propsPathBar', 'propsExpandAll', 'propsCollapseAll']
      .map((prop) => [prop, '搜索框、树、路径栏与两个按钮是 JsonViewer 代码组合的子元素，没有部件样式键：外观跟着 SearchField、Tree、Button、CopyButton 的主题样式'])),
    Tree: Object.fromEntries(['propsItem', 'propsToggle', 'propsCheckbox', 'propsLabel', 'propsSkeleton']
      .map((prop) => [prop, '每一行是 TreeItem 代码组合的（缩进、展开箭头、勾选框、图标、文字），骨架是 Skeleton，外观跟着 token 与各自组件的主题样式；部件样式键只有行列表（TreeParts.Rows）'])),
    SignaturePad: { propsCanvas: '画布是 SignaturePad 组合的子元素，没有部件样式键：底色与墨色是纸面，墨色用 PenColor' },
    Toaster: { propsProgress: '剩余时间条是每一项里的 Border，没有部件样式键：圆角 full、textMuted 50%' },
    ImageCropper: Object.fromEntries(['propsViewport', 'propsToolbar']
      .map((prop) => [prop, '区域与工具条是 ImageCropper 组合的子元素，没有部件样式键：外观跟主题'])),
    Carousel: Object.fromEntries(['propsViewport', 'propsSlide', 'propsPrevious', 'propsNext', 'propsControls', 'propsPause', 'propsDot']
      .map((prop) => [prop, '张片区、每一张、两侧按钮、控制条与页点是 Carousel 组合的子元素，没有部件样式键：外观跟主题，张片的样子由 ItemTemplate 给'])),
    SelectionBar: Object.fromEntries(['propsCount', 'propsClear', 'propsActions']
      .map((prop) => [prop, '数目、取消选择、批量操作区是 SelectionBar 组合的子元素，没有部件样式键：外观跟主题，批量操作的按钮放在 Actions 里'])),
    PaginationBar: Object.fromEntries(
      ['propsControls', 'propsPagination', 'propsSizeSelect', 'propsJump']
        .map((prop) => [prop, '页码、每页条数、跳页是公开的 PaginationPart、SizeSelectPart、JumpPart（各自有主题样式），直接改它们；结束边那一组是 PaginationBar 代码建的面板']),
    ),
    ScrollArea: { propsViewport: '视口就是 ScrollArea 自己：读屏名称、焦点都在它身上；滚动呈现器没有部件样式' },
    ...Object.fromEntries(['Dialog', 'AlertDialog', 'Drawer', 'FormDialog'].map((name) => [name, Object.fromEntries(['propsHeader', 'propsTitle', 'propsClose', 'propsDescription', 'propsBody', 'propsFooter', 'propsCancel', 'propsConfirm']
      .map((prop) => [prop, '标题行、标题、关闭按钮、说明、正文、页脚、按钮是 Dialog 代码组合的子元素，部件样式键只有面板、遮罩与阴影；外观跟着各自组件的主题样式']))])),
    Select: Object.fromEntries(['propsListbox', 'propsSearch'].map((prop) => [prop, '弹出层里的列表、搜索框是 Select 建的，外观跟着 Listbox、TextField 的主题样式'])),
    TagsInput: { propsListbox: '弹出层里的列表是 TagsInput 建的 Listbox，外观跟着 Listbox 的主题样式（同 Select）' },
    ...Object.fromEntries(['Combobox', 'Autocomplete'].map((name) => [name, { propsListbox: '弹出层里的列表是 ListPopover 建的 Listbox，外观跟着 Listbox 的主题样式（同 Select）' }])),
    Popconfirm: Object.fromEntries(['propsIcon', 'propsTitle', 'propsDescription', 'propsFooter', 'propsConfirm', 'propsCancel']
      .map((prop) => [prop, '图标、问题、说明、按钮行与按钮是 Popconfirm 代码组合的子元素（同 Dialog），外观跟着各自组件的主题样式；面板、阴影、箭头的部件样式键同 Popover'])),
    QRCode: Object.fromEntries(['propsLogo', 'propsOverlay', 'propsRefresh'].map((prop) => [prop, 'logo 底板、状态层与刷新按钮是 QRCode 代码组合的子元素（Border、Flex、Button），外观跟着各自组件的主题样式；没有部件样式键'])),
    Checklist: Object.fromEntries(['propsItem', 'propsSummary', 'propsProgress', 'propsList', 'propsStatus', 'propsLabel', 'propsDescription', 'propsMeta', 'propsAction']
      .map((prop) => [prop, '每一项是 ChecklistRow 代码组合的（状态、名称、说明、负责人 · 日期 · 证据、操作），汇总是 Text 与 Progress，外观跟着各自组件的主题样式；没有部件样式键'])),
    LoadingOverlay: Object.fromEntries(['propsOverlay', 'propsSpinner'].map((prop) => [prop, '遮罩与加载指示是 LoadingOverlay 代码组合的 Grid 与 Spinner，外观跟着 token 与 Spinner 的主题样式；没有部件样式键'])),
    Ellipsis: Object.fromEntries(['propsText', 'propsToggle'].map((prop) => [prop, '文字与展开按钮是 Ellipsis 代码组合的 Text 与 ButtonBase，外观跟着它们的主题样式；没有部件样式键'])),
    Timeline: Object.fromEntries(['propsItem', 'propsRail', 'propsMarker', 'propsLine', 'propsContent', 'propsTitle', 'propsMeta', 'propsTime', 'propsDescription', 'propsDetails', 'propsToggle', 'propsAvatar', 'propsPending']
      .map((prop) => [prop, '每一行是 TimelineRow 代码组合的（节点、连接线、标题、操作人、时间、说明、详情、展开按钮），外观跟着各自组件的主题样式与 token；没有部件样式键'])),
    CommandPalette: Object.fromEntries(['propsInput', 'propsListbox'].map((prop) => [prop, '面板里的搜索框、列表是 CommandPalette 建的，外观跟着 TextField、Listbox 的主题样式；部件样式键只有搜索行'])),
  },
  WinForms: {
    ...Object.fromEntries(['SegmentedField', 'DateField', 'TimeField', 'DatePicker', 'DateRangePicker'].map((name) => [name, Object.fromEntries(['propsRoot', 'propsClear', 'propsIcon']
      .map((prop) => [prop, '外壳是 SegmentedFieldControl 自己画的（ControlShell，同 SelectControl），图标、清空按钮是组合的子控件：没有部件属性记录']))])),
    DateSegmentGroup: Object.fromEntries(['propsSegment', 'propsLiteral'].map((prop) => [prop, '各段与分隔符是 DateSegmentGroupControl 自己画的（同 OtpFieldControl），没有部件属性记录：外观跟主题'])),
    ...Object.fromEntries(['Calendar', 'RangeCalendar'].map((name) => [name, Object.fromEntries(['propsMonths', 'propsHeader', 'propsPrevious', 'propsNext', 'propsHeading', 'propsGrid', 'propsWeekday', 'propsWeekNumber', 'propsCell', 'propsCellButton', 'propsFooter']
      .map((prop) => [prop, '网格是 CalendarGridControl 自己画的（表头、周数、格子、区间那一条），页头是 IconButtonControl、ButtonControl 子控件：没有部件属性记录；格子大小是 CellSizeEm，内容用 CellText、DrawCell']))])),
    FormSection: Object.fromEntries(['propsHeader', 'propsHeading', 'propsTrigger', 'propsChevron', 'propsDescription', 'propsExtra', 'propsBody', 'propsCollapse']
      .map((prop) => [prop, '标题栏、标题、展开按钮、说明、操作区、栅格与展开收起层是 FormSectionControl 组合的子控件：没有部件属性记录，外观跟主题；栅格是 Grid 属性'])),
    FormErrorSummary: Object.fromEntries(['propsTitle', 'propsList', 'propsLink']
      .map((prop) => [prop, '标题、列表与每项的链接是 FormErrorSummaryControl 组合的 FlexControl、UnorderedListControl、LinkControl：没有部件属性记录，外观跟主题'])),
    Field: Object.fromEntries(['propsLabel', 'propsControl']
      .map((prop) => [prop, '标签是 FieldLabelControl、控件列是 FieldControl 自己排的：没有部件属性记录，标签外观跟主题（说明、错误有 PropsDescription、PropsError）'])),
    Tour: Object.fromEntries(['propsSpotlight', 'propsPanel', 'propsCover', 'propsTitle', 'propsContent', 'propsFooter', 'propsProgress', 'propsSkip', 'propsPrevious', 'propsNext']
      .map((prop) => [prop, '遮罩是分层窗口、卡片是 FloatingSurface，标题、说明、步数与按钮是组合的子控件：没有部件属性记录；下一步先不可用用 CanGoNext'])),
    KanbanBoard: Object.fromEntries(['propsBoard', 'propsColumn', 'propsHeader', 'propsCount', 'propsReason', 'propsList', 'propsEmpty', 'propsCard', 'propsHandle']
      .map((prop) => [prop, '各列、卡片是 KanbanBoardControl 画出来的子控件，列头是 TextControl 与 BadgeControl，把手是 IconButtonControl：没有部件属性记录'])),
    Sortable: Object.fromEntries(['propsItem', 'propsHandle', 'propsContent']
      .map((prop) => [prop, '每一项是 SortableControl 画边框的子控件，把手是 IconButtonControl，内容由 ItemFactory 建：没有部件属性记录'])),
    VirtualList: Object.fromEntries(['propsSkeleton', 'propsList']
      .map((prop) => [prop, '骨架行与放项的那一层是 VirtualListControl 组合的子控件，没有部件属性记录：外观跟主题'])),
    InfiniteListStatus: { propsRetry: '重试按钮是 InfiniteListStatusControl 组合的 ButtonControl（sm、ghost），走 ProviderControl 的组件定制' },
    JsonViewer: Object.fromEntries(['propsSearch', 'propsTree', 'propsPathBar', 'propsExpandAll', 'propsCollapseAll']
      .map((prop) => [prop, '搜索框、树、路径栏与两个按钮是 JsonViewerControl 组合的子控件，没有部件属性记录：外观跟主题，按钮走 ProviderControl 的组件定制'])),
    Tree: Object.fromEntries(['renderItem', 'propsItem', 'propsToggle', 'propsCheckbox', 'propsLabel', 'propsEmpty', 'propsSkeleton']
      .map((prop) => [prop, '行（箭头、勾选框、图标、文字）是 TreeControl 画出来的，空状态与骨架是固定的子控件，没有部件属性与自定义内容：要多显示的文字写进 GetLabel'])),
    SignaturePad: { propsCanvas: '墨迹画在 SignaturePadControl 自己身上（没有单独的画布控件）：底色与墨色是纸面，墨色用 PenColor' },
    Toaster: { propsProgress: '剩余时间条是每一项里画出来的细条，没有部件属性：圆角 full、textMuted 与表面色各半（控件不能半透明）' },
    ImageCropper: Object.fromEntries(['propsViewport', 'propsToolbar']
      .map((prop) => [prop, '区域与工具条是 ImageCropperControl 组合的子控件，没有部件属性记录：外观跟主题'])),
    Carousel: Object.fromEntries(['propsViewport', 'propsSlide', 'propsPrevious', 'propsNext', 'propsControls', 'propsPause', 'propsDot']
      .map((prop) => [prop, '张片区、每一张、两侧按钮、控制条与页点是 CarouselControl 组合的子元素，没有部件属性记录：外观跟主题，张片的样子由放进 Slides 的控件自己定'])),
    SelectionBar: Object.fromEntries(['propsCount', 'propsClear', 'propsActions']
      .map((prop) => [prop, '数目、取消选择、批量操作区是 SelectionBarControl 组合的子元素，没有部件属性记录：外观跟主题，批量操作的按钮放在 Actions 里'])),
    NativeSelect: Object.fromEntries(['variant', 'radius', 'icon']
      .map((prop) => [prop, '就是 Win32 的下拉框：外框、下拉按钮是系统画的，没有外观档位、圆角与前置图标（出错时在系统外框上描一圈 danger）'])),
    Marquee: { fade: '子控件各有窗口，盖不了它们：没有两端渐隐' },
    ActionBar: {
      sticky: '没有网页的 position: sticky（子控件是窗口，和容器的排版抢位置不可靠）：停靠在滚动区域外面的底边，一直看得见',
      ...Object.fromEntries(['propsStart', 'propsActions'].map((prop) => [prop, '说明是 TextControl、按钮区是 FlexControl（Actions 是它的子控件集合），没有部件属性：外观跟主题'])),
    },
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
    ...Object.fromEntries(['Combobox', 'Autocomplete'].map((name) => [name, {
      children: 'WinForms 的选项是 ListboxControl 画出来的，没有自定义内容（同 SelectControl）',
      ...Object.fromEntries(['propsRoot', 'propsClear', 'propsChevron', 'propsPopover', 'propsListbox', 'propsIcon']
        .map((prop) => [prop, '外壳是 ComboboxControl 自己，图标、清空与展开按钮是子控件，弹出层是两层窗口：没有部件属性记录（同 SelectControl）'])),
    }])),
    TagsInput: {
      children: 'WinForms 的建议是 ListboxControl 画出来的，没有自定义内容（同 SelectControl）',
      ...Object.fromEntries(['propsRoot', 'propsTag', 'propsClear', 'propsPopover', 'propsListbox', 'propsIcon']
        .map((prop) => [prop, '外壳是 TagsInputControl 自己，标签、图标、清空按钮是子控件，弹出层是两层窗口：没有部件属性记录（同 SelectControl）'])),
    },
    ...Object.fromEntries(['Dialog', 'AlertDialog', 'FormDialog'].map((name) => [name, Object.fromEntries(['propsHeader', 'propsTitle', 'propsClose', 'propsDescription', 'propsBody', 'propsFooter', 'propsCancel', 'propsConfirm']
      .map((prop) => [prop, '标题行、正文、页脚是 DialogControl 组合的子控件，面板与遮罩是两层窗口画的，没有部件属性记录']))])),
    Drawer: {
      ...Object.fromEntries(['propsHeader', 'propsTitle', 'propsClose', 'propsDescription', 'propsBody', 'propsFooter', 'propsCancel', 'propsConfirm']
        .map((prop) => [prop, '标题行、正文、页脚是 DialogControl 组合的子控件，面板与遮罩是两层窗口画的，没有部件属性记录'])),
      propsResizeHandle: '宽度调整手柄是 DrawerControl 放在面板窗口里的 ResizeHandleControl 子控件，没有部件属性记录',
    },
    Image: { propsFallback: '读不了时的占位是 ImageControl 自己画的图标与文字：文字用 Fallback 换，颜色跟主题' },
    Lightbox: Object.fromEntries(['propsViewport', 'propsCaption']
      .map((prop) => [prop, '图片区、说明是 LightboxControl 放在面板窗口里的子控件（图片区自绘），没有部件属性记录'])),
    CommandPalette: {
      renderOption: 'WinForms 的选项是 ListboxControl 画出来的，没有自定义内容',
      ...Object.fromEntries(['propsTrigger', 'propsInput', 'propsListbox'].map((prop) => [prop, '默认触发按钮、搜索框、列表是 CommandPaletteControl 组合的子控件，没有部件属性记录'])),
    },
    Tag: { propsRemove: '移除按钮是 CloseButtonControl 子控件，颜色、字号跟着标签；没有部件属性记录' },
    Alert: Object.fromEntries(['propsSpinner', 'propsBody', 'propsContent', 'propsAction', 'propsClose']
      .map((prop) => [prop, '加载指示、操作、关闭按钮与额外内容是 AlertControl 的子控件，文字区是排版出来的一块区域：没有部件属性记录（图标、标题、说明是画的元素，有 PropsIcon、PropsTitle、PropsDescription）'])),
    Descriptions: Object.fromEntries(['propsList', 'propsItem', 'propsValue', 'propsCopy']
      .map((prop) => [prop, '列表、格子是 DescriptionsControl 画的面板，控件的值与复制按钮是子控件，没有部件属性记录（标题、标签是画的元素，有 PropsTitle、PropsLabel）'])),
    Stat: Object.fromEntries(['propsLabel', 'propsValue', 'propsIcon', 'propsNumber', 'propsUnit', 'propsChange', 'propsChangeLabel', 'propsDescription', 'propsBody', 'propsFooter']
      .map((prop) => [prop, '指标名、图标、单位、说明是 StatControl 画的元素，数字是 AnimatedNumberControl 子控件，内容与底栏是使用方的控件：没有部件属性记录'])),
    AvatarGroup: { propsMore: '"+N"的圆是 AvatarGroupControl 画的，没有部件属性记录（里面的文字有 PropsMoreText）' },
    Result: { propsAction: '操作是 ResultControl 的子控件（排在最下面、居中），没有部件属性记录（图标、状态码、标题、说明是画的元素，有 PropsIcon、PropsCode、PropsTitle、PropsDescription）' },
    Banner: Object.fromEntries(['propsAction', 'propsClose']
      .map((prop) => [prop, '操作与关闭按钮是 BannerControl 的子控件，没有部件属性记录（图标、标题、正文是画的元素，有 PropsIcon、PropsTitle、PropsContent）'])),
    Tab: { propsClose: '关闭按钮是 TabItemControl 里的 CloseButtonControl 子控件（xs、圆形），没有部件属性记录' },
    TabList: {
      propsIndicator: '分隔线与指示条是 TabListControl 自己画的，没有可改的部件',
      propsBar: '标签栏就是 TabListControl 自己（横排时自己排滚动按钮、列表、菜单与新建按钮），没有另一层',
      ...Object.fromEntries(['propsAdd', 'propsScrollButton'].map((prop) => [prop, '新建、滚动按钮是 TabListControl 建的 IconButtonControl 子控件，外观走 ProviderControl 的组件定制，没有部件属性记录'])),
    },
    Popover: { propsArrow: '面板、阴影与箭头是组件库画在分层窗口上的位图，箭头没有可改样式的部件' },
    HoverCard: { propsArrow: '同 PopoverControl：面板、阴影与箭头是组件库画在分层窗口上的位图，箭头没有可改样式的部件' },
    QRCode: Object.fromEntries(['propsLogo', 'propsOverlay', 'propsRefresh'].map((prop) => [prop, 'logo 与状态层是 QRCodeControl 画的（刷新按钮是子控件 ButtonControl），没有部件属性记录'])),
    Splitter: Object.fromEntries(['propsStart', 'propsEnd', 'propsHandle', 'propsToggle'].map((prop) => [prop, '两栏是 ScrollAreaControl、分隔条是 Divider（SplitterHandleControl）、收起按钮是 IconButtonControl，都是真实的子控件，外观跟着各自的控件；没有部件属性记录'])),
    Checklist: Object.fromEntries(['propsItem', 'propsSummary', 'propsProgress', 'propsList', 'propsStatus', 'propsLabel', 'propsDescription', 'propsMeta', 'propsAction']
      .map((prop) => [prop, '每一项是 ChecklistRowControl 组合的子控件（状态、名称、说明、负责人 · 日期 · 证据、操作），没有部件属性记录'])),
    LoadingOverlay: {
      blur: '遮罩画在属于窗体的分层窗口里，子控件各有窗口，画不到也模糊不了它们的内容',
      ...Object.fromEntries(['propsOverlay', 'propsSpinner'].map((prop) => [prop, '遮罩与加载指示整张画在分层窗口的位图上，没有部件属性记录'])),
    },
    Ellipsis: Object.fromEntries(['propsText', 'propsToggle'].map((prop) => [prop, '文字与展开按钮是 EllipsisControl 组合的 TextControl 与链接样式的按钮，没有部件属性记录'])),
    Timeline: Object.fromEntries(['propsItem', 'propsRail', 'propsMarker', 'propsLine', 'propsContent', 'propsTitle', 'propsMeta', 'propsTime', 'propsDescription', 'propsDetails', 'propsToggle', 'propsAvatar', 'propsPending']
      .map((prop) => [prop, '节点与连接线是 TimelineRowControl 画的，标题、操作人、时间、说明、详情、展开按钮是它组合的子控件，没有部件属性记录'])),
    Popconfirm: {
      propsArrow: '同 PopoverControl：面板、阴影与箭头是组件库画在分层窗口上的位图，箭头没有可改样式的部件',
      ...Object.fromEntries(['propsIcon', 'propsTitle', 'propsDescription', 'propsFooter', 'propsConfirm', 'propsCancel']
        .map((prop) => [prop, '图标、问题、说明、按钮行与按钮是 PopconfirmControl 组合的子控件，没有部件属性记录；外观跟着各自组件与主题'])),
    },
    Listbox: Object.fromEntries(
      ['empty', 'propsOption', 'propsGroupLabel', 'propsCheck', 'propsEmpty', 'propsSkeleton']
        .map((prop) => [prop, '选项、组名、已选标记是 ListboxControl 画出来的，空状态与骨架是固定的子控件，没有部件属性与自定义内容']),
    ),
    ...Object.fromEntries(['TextField', 'SearchField'].map((name) => [name, Object.fromEntries(
      ['propsRoot', 'propsIcon', 'propsIconEnd', 'propsPrefix', 'propsSuffix', 'propsCount', 'propsClear', 'propsReveal']
        .map((prop) => [prop, '外壳是 TextInputControl 自己画的，里面的图标、前后缀、按钮、字数是子控件，没有部件属性：外观跟主题，按钮走 ProviderControl 的组件定制']),
    )])),
    NumberField: Object.fromEntries(
      ['propsRoot', 'propsIcon', 'propsPrefix', 'propsSuffix', 'propsDecrement', 'propsIncrement', 'propsClear']
        .map((prop) => [prop, '外壳是 TextInputControl 自己画的，里面的图标、前后缀、加减与清空按钮是子控件，没有部件属性：外观跟主题，按钮走 ProviderControl 的组件定制']),
    ),
    Accordion: {
      headingLevel: 'WinForms 的读屏接口（AccessibleObject）没有标题层级',
      size: '字号跟着宿主的 Font（同其他 WinForms 容器）',
    },
    AccordionItem: Object.fromEntries(['propsTrigger', 'propsChevron', 'propsContent']
      .map((prop) => [prop, '标题按钮、箭头、内容区是 AccordionItemControl 建的子控件，没有部件属性：外观跟主题'])),
    PageHeader: Object.fromEntries(
      ['propsBreadcrumbs', 'propsMain', 'propsBack', 'propsHeading', 'propsTitleRow', 'propsTitle', 'propsMeta', 'propsDescription', 'propsActions']
        .map((prop) => [prop, '面包屑、标题行、返回按钮、标题、说明是 PageHeaderControl 建的子控件，外观跟主题（按钮走 ProviderControl 的组件定制）；标记、操作是使用方给的控件，直接改它们']),
    ),
    PageJump: Object.fromEntries(
      ['propsBefore', 'propsInput', 'propsAfter'].map((prop) => [prop, '"跳至""页"与输入框是 PageJumpControl 的子控件，没有部件属性：外观跟主题，宽度与字号按 TextSize']),
    ),
    PaginationBar: Object.fromEntries(
      ['propsTotal', 'propsControls', 'propsPagination', 'propsSizeSelect', 'propsJump']
        .map((prop) => [prop, '页码、每页条数、跳页是公开的 PaginationPart、SizeSelectPart、JumpPart，直接改它们的属性；总数与结束边那一组是 PaginationBarControl 排的子控件']),
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
  // Avalonia 的 ComboBox 自带占位文字（NativeSelect 继承它，映射见 PLATFORM_COMPONENT_PROP_RENAMES）
  'ComboBox.PlaceholderText',
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
  // 写成 CSS 长度的高度（Sparkline 的 height）：平台控件自己的 Height（像素），不写时组件给默认值（2em）
  'Height',
  // 同上，边长（QRCode 的 size）：平台控件自己的 Width（像素），不写时组件给默认值（10em）
  'Width',
  // 抽屉面板的宽度、拖动时的最小宽度（Drawer 的 width、minWidth）：平台控件自己的 Width、MinWidth（抽屉自己不参与排版）
  'MinWidth',
]);

/** 桌面上不做的组件与原因（ROADMAP 阶段 3） */
export const EXCLUDED = {
  VisuallyHidden: '桌面上用 AutomationProperties.Name / HelpText',
  SkipLink: '浏览器专用（跳到主内容的链接）',
  ClientOnly: '浏览器专用（服务端渲染时不输出）',
  FormValue: '浏览器原生表单提交专用（隐藏的 input 让 FormData 读到）；桌面上值经绑定或 FormController 交给模型',
};

/**
 * 桌面上不适用的 vue-tsx-pro 测试用例（tools/parity/behavior.mjs）：组件 → { it 名字的开头: 原因 }。
 * 其余用例要有测试标了 [Upstream("组件", "名字开头")]
 */
/** 只在某个平台上不适用的上游测试用例：库 → { 组件: { 用例名的开头: 原因 } } */
export const PLATFORM_BEHAVIOR_NOT_APPLICABLE = {
  WPF: {
    Checklist: { '部件：各层带 data-part': '每一项是代码组合的元素，没有部件样式键（PLATFORM_PROPS_NOT_PORTED）；结构由 Rows 的测试核对' },
    Timeline: {
      'Timeline：根、每项': '每一行是代码组合的元素，没有部件样式键（PLATFORM_PROPS_NOT_PORTED）；结构由 Rows 的测试核对',
      '操作人头像、进行中的加载指示也转发部件属性': '同上：头像、加载指示是 TimelineRow 组合的元素，没有部件样式键',
    },
  },
  WinForms: {
    Checklist: { '部件：各层带 data-part': '每一项是组合的子控件，没有部件属性记录（PLATFORM_PROPS_NOT_PORTED）；结构由 Rows 的测试核对' },
    Timeline: {
      'Timeline：根、每项': '节点与连接线是画的，其余是组合的子控件，没有部件属性记录（PLATFORM_PROPS_NOT_PORTED）；结构由 Rows 的测试核对',
      '操作人头像、进行中的加载指示也转发部件属性': '同上：头像、加载指示是 TimelineRowControl 组合的子控件，没有部件属性记录',
    },
    ItemList: { 'ItemList：列表是 root，条目是 item': '标题、说明是 ItemListItemControl 里的子控件，没有部件标记（PLATFORM_PROPS_NOT_PORTED）' },
    Collapse: { '属性落到外层，propsContent 落到内容层': 'CollapseControl 的子控件直接放在它里面，没有单独的内容层（WinForms 的控件就是一层窗口）' },
    ScrollArea: { '内容层、滚动条、滑块都能加属性': '滚动条是组件库画的滑块子控件，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）' },
    CodeBlock: {
      '部件属性': '代码块是 CodeBlockControl 自己画的，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）',
    },
    Stat: {
      '骨架换回数值时数值与变化量淡入': 'WinForms 的子控件不能半透明：骨架与数值直接互换（同 CardControl）',
      '每个节点都有部件属性': '指标名、图标、单位、说明是 StatControl 画的元素，数字是 AnimatedNumberControl 子控件，没有部件属性记录（PLATFORM_PROPS_NOT_PORTED）',
    },
    AppShell: { '部件属性': '各格是 AppShellControl 自己排、自己画的，没有部件属性可加（PLATFORM_PROPS_NOT_PORTED）' },
  },
};

export const BEHAVIOR_NOT_APPLICABLE = {
  Calendar: {
    '应用里统一显示周数': 'TS 的 components.Calendar 默认值与配方定制是网页的样式系统：桌面用样式统一设 ShowWeekNumbers 与部件外观（Avalonia 的 :is(rune|CalendarView) 选择器、WPF 的部件样式键），部件名在 CalendarViewParts',
    'CalendarDate 进、CalendarDate 出': '值是 ISO 字符串（另有 DateOnly 的 Date 属性），没有 @internationalized/date 的日期值类型',
    'CalendarDateTime 选了另一天，时间不变': '同上：没有带时间的日期值类型；多选时没变的那几天原样保留（带时间的 ISO 字符串不丢时间）',
    '一开始为空：valueType': '同上：值总是 ISO 字符串，要 DateOnly 时读 Date',
  },
  RangeCalendar: { 'ZonedDateTime 的区间': '值是 IsoDateRange（两端是日期的 ISO 字符串），没有带时区的日期值类型' },
  DateField: {
    '输入法：组合中的文字不进段': '桌面的输入法上屏后才发文字输入事件（Avalonia 的 TextInput、WPF 的 TextComposition、WinForms 的 KeyPress），段里没有可编辑的文字被组合改掉',
    'DateField：ZonedDateTime 改了月份': '值是 ISO 字符串（另有 DateOnly、DateTime 属性），没有带时区的日期值类型',
  },
  'form-dialog': { '字段的 id 不和页面上已有的撞': 'id 与标签的 for 是网页的：桌面上标签直接指向对话框里自己的控件实例' },
  KanbanBoard: { '部件：列、卡片可以按列、按卡片给部件属性': '没有按列、按卡片的部件属性（PROPS_NOT_PORTED）：卡片的外观在 ItemTemplate / ItemFactory 里，Avalonia 的部件名在 KanbanBoardParts' },
  VirtualList: { 'propsItem 可以按项计算': '没有 propsItem、propsList（PROPS_NOT_PORTED）：每项的外观在 ItemTemplate / ItemFactory 里；序号与总数由组件管（Only_visible_items_are_realized_with_set_positions 覆盖）' },
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
  Stat: { '部件带标记；按色调档位追加样式': 'TS 的 components.Stat 配方定制是网页的样式系统；桌面改部件样式（Avalonia 的 rune|Stat #PART_Number 选择器、WPF 的 StatParts 样式键），数值的色调是 Tone' },
  Embed: { 'iframe 带 title、src': 'loading="lazy"、referrerpolicy 是 iframe 的属性：桌面的网页内核放进界面就加载，没有懒加载；title 是读屏名称（测试覆盖）' },
  Video: {
    '自绘控制条跟着 video 的事件走': '控制条的脚本在网页内核的页面里跑，测试里没有内核：页面的元素与文案由 Custom_bar 的测试核对，事件与键盘在真实窗口里核对',
    '不用真的播起来的部分': '同上：没有原生控制条、M 静音、字幕与倍速菜单都是网页内核里页面脚本的事，测试里没有内核',
    '挂载时按 video 现在的状态显示': '同上：接管时读 video 的状态是页面脚本的事，测试里没有内核',
    '不知道时长（直播）': '同上：时长、进度条与方向键都在页面脚本里，测试里没有内核',
  },
  Listbox: { '写 16rem 高就是 16rem 高': '网页的回归（高度写在 sx 上落到哪一层、border-box 算不算内边距）与部件标记 data-scope：桌面控件的 Height 就是整个控件的高度' },
  Collapse: { '页内搜索命中': '页内搜索命中收起的内容时展开（hidden="until-found"、beforematch）是浏览器的能力，桌面没有页内搜索' },
  Slider: { '轨道与已选范围都能加属性': '轨道与已选范围是 Slider 自己画的，没有元素可加属性（PROPS_NOT_PORTED）' },
  FieldCompare: { 'FieldCompare：根元素也带标记': '部件标记（data-scope、data-part）是网页给样式与测试找元素用的；桌面的部件是控件本身' },
  Image: { 'sources：渲染成 picture': '桌面没有 picture 与 srcset（PROPS_NOT_PORTED）：一个地址就是一张图' },
  ImageGroup: { '组里的图片 preview 是每次新建的对象': 'Vue 的响应式重渲染问题：桌面的组打开时按界面收集图片，平时不读它们的属性，没有重渲染' },
  RelativeTime: { '服务端渲染记下': '桌面没有服务端渲染与水合：控件在界面线程上直接按此刻渲染；要固定"现在"（截图、测试）写 Now' },
};

/** 像素对照（pixels.mjs）默认允许的差异比例：未盖掉文字的像素里，不同的占多少以内算一致 */
export const DEFAULT_PIXEL_ALLOWANCE = 0.005;

/**
 * 像素对照里允许更大差异的示例与原因：平台 → 页面 → 示例 → { ratio, reason }；平台写 all 时对三个平台都算。
 * 只登记查清了原因、决定不改的差异（平台画法的极限、上游示例里有平台没有的内容），改得了的直接改
 */
export const PIXEL_ALLOWANCE = {};
