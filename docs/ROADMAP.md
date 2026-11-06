# 路线图

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。

已完成部分的写法与取舍见 [DESIGN.md](DESIGN.md)。

## 目标

**三套 .NET 桌面组件库（Avalonia、WPF、WinForms），对标 vue-tsx-pro 的 `packages/ui`。**

- **外观以 TS 版为准，尽量一致**：token、主题由脚本生成；组件的配方按 vue-tsx-pro 对应组件逐条移植（尺寸、内边距、圆角、颜色 token、状态、过渡），不凭印象重画；做不到的写进 DESIGN。
- **写法是各平台地道的写法**：API 的形状按平台习惯（见"平台化的约定"），不照搬 Vue 的组合式写法。
- **三个库各自独立**：不共享代码，一致性靠 `tools/` 生成与对照。
- three 不做；charts、maps、diagram、code-editor、xlsx 等前面完成后再定。

## 现状（2026-11-06）

- 阶段 0 骨架完成：三个平台各有组件库、示例应用、测试工程，基础设施都配了测试（37 个，全部通过）。
- 前置探测都做完了，结论在 DESIGN：档位类型（§3）、Avalonia 组合档位（§4）、部件的转发与层层合并（§7）、WinForms 主题切换（§9）、Avalonia 桌面热重载（§11）。只剩 WPF 的 IDE 热重载要手动试。
- 梳理了 vue-tsx-pro 的结构：基础设施（`utils/`、`config/`）与组件按依赖分层（阶段 2、3）。
- 档位类型、Avalonia 组合档位、WinForms 组件形态已定（"已定的决定" 10–12）。
- 还没有任何组件。下一步：定下"待定的决定"（推进节奏、WinForms 范围等），然后做阶段 1、2。

## 已定的决定

| # | 事项 | 决定 |
|---|---|---|
| 1 | 参照 | 以 vue-tsx-pro（TS 版）为准，外观尽量一致；RuneForBlazor 只作参考（进度、踩过的坑、"Blazor 化的约定"的思路），不共享代码 |
| 2 | 结构 | 不做单源、不做核心包；三个库各自独立，写法贴合各自平台 |
| 3 | 分层合并 | 组件 < 档位 < 组合档位 < 应用级 < 外层 Provider < 内层 Provider < 使用方，上层只写要改的部分；可以有适量的运行时 |
| 4 | 尺寸 | 沿用 rem / em（DESIGN §6） |
| 5 | 更新 | 细粒度更新，只更新该更新的节点（DESIGN §10） |
| 6 | 开发体验 | 开发期尽量热重载（DESIGN §11） |
| 7 | 骨架 | 支撑长期开发，不建临时目录；探测工程不进仓库，结论写进 DESIGN |
| 8 | 部件 | 每个子节点的 API 都能从外层转发，层层合并（DESIGN §7） |
| 9 | 顺序 | 先做基础设施，再按依赖从下往上做组件：基础组件 → 交互基础 → 复合 → 上层 → 页面组合（阶段 2、3） |
| 10 | 档位类型 | 配方的档位维度（variant、tone、radius、size……）用字符串包装类型，size 另外接受数字；行为类的封闭集合用 enum（DESIGN §3） |
| 11 | Avalonia 组合档位 | 调色板对象：色调样式设 `Palette`，样式档位绑定调色板的画刷；开销成问题时退到"每种样式一个 ControlTheme"（DESIGN §4） |
| 12 | WinForms 组件形态 | 无窗口元素是本体；每个组件另有单元素宿主，能放进设计器；数量大的地方直接用元素（DESIGN §9） |

## 平台化的约定

以 TS 版为准的是**外观、交互、无障碍和名字**；**API 的形状**按各平台。做组件时照下表写，遇到新的概念补进来。标"待验证"的，在第一个用到它的组件里实测后改成结论。

### 组件与属性

| TS 版 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 一个元素一个组件 | 结构复杂、使用方可能改模板的：`TemplatedControl` + ControlTheme；大量重复的轻量部件：`Control` 重写 `Render`（DESIGN §9） | `Control` + Generic.xaml 模板；轻量部件：`FrameworkElement` 重写 `OnRender` | 无窗口元素，由宿主控件布局、绘制（DESIGN §9）；放进设计器时用单元素宿主包一层（阶段 2） |
| 可双向绑定的 `_value` / `_checked` / `_open` | `StyledProperty`，`defaultBindingMode: TwoWay`。名字按平台：值叫 `Value`、`SelectedItem`，布尔状态叫 `IsChecked`、`IsOpen`、`IsExpanded` | `DependencyProperty` + `BindsTwoWayByDefault`，命名同左 | 属性 + `XxxChanged` 事件（同 `CheckBox.CheckedChanged`）；实现 `INotifyPropertyChanged`，能经 `BindingSource` 绑定 |
| 外观档位 `variant` / `tone` / `size` / `radius` | 字符串包装类型（DESIGN §3），映射成伪类 | 同左，主题样式里的 `Trigger` / `MultiTrigger` | 同左，配方函数里分支 |
| 行为类的取值（orientation、placement、selectionMode） | enum | enum | enum |
| 空值 | 可空类型（`int?`、`DateOnly?`）；多选清空是空集合 | 同左 | 同左 |
| `disabled` / `readonly` / `loading` | `IsEnabled`（平台自带）、`IsReadOnly`、`IsLoading`，同时映射成伪类 | `IsEnabled`、`IsReadOnly`、`IsLoading` | `Enabled`（平台自带）、`ReadOnly`、`Loading` |
| 组件默认值（`ComponentDefaults`） | Provider.Styles 里的 Setter（DESIGN §5） | 同左，随 Provider 合成 | 作用域控件上的默认值信号 |
| 导出的 prop 都写注释 | 公开成员写 XML 文档注释（库里 CS1591 已是错误）；档位的每个常用取值一条注释 | 同左 | 同左；另加 `[Description]`、`[Category]`，设计器里能看到 |

### 内容、事件与命令

| TS 版 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 默认内容 `children` | `Content` + `ContentTemplate`（`ContentControl`） | 同左 | 子元素 |
| 具名内容（`header`、`footer`） | `object` 属性 + 对应的模板属性（`Header` / `HeaderTemplate`） | 同左 | 元素属性（`Header`） |
| 带参数的渲染函数 `{(item) => …}` | `IDataTemplate`，数据作 DataContext（`ItemTemplate`） | `DataTemplate` / `DataTemplateSelector` | 委托 `Func<T, Element>`，或自绘事件（同 `DrawItem`） |
| `items` + `toOption` | `ItemsSource` + `ItemTemplate` / `DisplayMemberBinding`，取值 `SelectedValueBinding`；能声明的直接写子项（`<rune:TabItem>`） | `ItemsSource` + `DisplayMemberPath` / `SelectedValuePath` | `DataSource` + `DisplayMember` / `ValueMember`（同 `ListControl`） |
| `selectionMode` | `SelectionMode` + `SelectedItem` / `SelectedItems`，选择类控件建在 `SelectingItemsControl` 上（键盘、选择模型平台自带） | 建在 `Selector` / `MultiSelector` 上 | `SelectedItem` / `SelectedItems` 属性 |
| 变化回调带原因 `onOpenChange(open, { reason })` | 路由事件，参数里带 `Reason`；能拦下的用前置事件（`Opening` / `Closing` 带 `Cancel`，同 `Window.Closing`） | 同左 | `EventHandler<XxxEventArgs>`，前置事件的参数继承 `CancelEventArgs` |
| 事件"使用方先处理、可接管"（`preventDefault`） | 隧道事件里 `Handled = true`（DESIGN §7） | Preview 事件里 `Handled = true` | 可取消的前置事件 |
| 命令式控制器（`useForm()` → `controller`、`handle`） | 控件上的方法与 `ICommand` 属性（`Command` / `CommandParameter`），MVVM 能用；控制器对象作属性传入 | 同左（`RoutedCommand` 也可以） | 控件上的方法 |
| `useToast()` / `useDialog()` | 窗口里的宿主（放在 `OverlayLayer` / 顶层的 `ToastHost`、`DialogHost`）+ 服务入口（待验证：按 `TopLevel` 找宿主） | 窗口里的宿主（`AdornerLayer` 或顶层面板）+ 服务入口 | 放到窗体上的组件（同 `ToolTip`、`ErrorProvider`） |
| 状态标记 `data-state` | 伪类（`:open`、`:checked`、`:loading`）+ 只读状态属性 | 只读依赖属性，主题样式的触发器读它 | 状态字段，绘制时读取，同步到无障碍的 State |
| 表单：zod + `useForm` | `INotifyDataErrorInfo` / DataAnnotations，错误经平台的 `DataValidationErrors` 显示在 Field 上 | `INotifyDataErrorInfo` + `Validation.Errors`（`ValidatesOnNotifyDataErrors`） | `INotifyDataErrorInfo` + DataAnnotations 的 `Validator`，Field 显示错误（同 `ErrorProvider` 的用法） |
| 路由适配（`UiRouter`、LinkButton） | 桌面没有地址：LinkButton 打开外部链接（`TopLevel.Launcher`）或执行命令；导航组件的当前项由使用方给（`SelectedItem` / `IsSelected`） | 外部链接用 `Process.Start`，其余同左 | 同左 |
| `http` / 远程选项 | 不内置请求客户端；远程数据是异步委托（`Func<查询, CancellationToken, Task<结果>>`） | 同左 | 同左 |
| "现在"（今天、相对时间） | 注入 `TimeProvider`，测试可以固定 | 同左 | 同左 |

### 样式与主题

| TS 版 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 配方 `css.recipe` | 组件的 ControlTheme；档位是伪类（DESIGN §4） | Generic.xaml 主题样式的 `Style.Triggers`（DESIGN §4） | 配方函数：`(档位, 作用域主题) → Look`，按组合共享 `Computed`（DESIGN §4） |
| 组合档位（variant × tone） | 调色板对象（DESIGN §4） | `MultiTrigger` 逐条照搬 | 配方函数里按调色板取色 |
| `sx` | 使用方 Styles / `Classes`（选择器带伪类才能盖过档位） | 普通 Style、Provider.Styles | `Sx` 记录（字段可空，逐字段覆盖） |
| 部件 `propsXxx`（含嵌套组件的部件，层层合并，DESIGN §7） | 样式穿透模板：作用域里写路径选择器（`rune\|Select /template/ rune\|TextField#PART_Search /template/ rune\|Icon#PART_Icon`），单个实例写控件自己的 Styles | 部件样式键 + Provider 合成；嵌套组件的部件写在外层部件样式的 `Style.Resources` 里 | 部件属性记录（`PropsIcon`），嵌套组件的部件是嵌套记录，递归合并 |
| `createUi` | `Application.Styles` 里的 `rune:RuneTheme`，应用级配置作它的属性 | `Application.Resources` 合并 Generic 之外的主题字典 | 应用级的 `Rune.Configure(...)`（静态入口，信号） |
| `Provider` | `rune:Provider`：Styles、默认值、主题作用域、语言 | 同左，合成 BasedOn 链（DESIGN §4） | 作用域控件 `Provider : Panel` |
| 主题、16 套内置主题 | 每套主题一个自定义 `ThemeVariant`（`new ThemeVariant("paper", ThemeVariant.Light)`，取不到的回落到亮色或暗色）；局部主题 `ThemeVariantScope` | 每套主题一份合并字典，换字典 | 每套主题一个 `Palette` 记录，换信号 |
| 跟随系统亮暗 | `RequestedThemeVariant="Default"` | `SystemParameters` / 注册表的应用模式，切换时换字典（待验证） | 同左 |
| 语言 `lang` | 生成的 `Lang` 记录，作为可继承的附加属性挂在作用域上；文案用标记扩展 `{rune:Lang Confirm}` | 同左 | 作用域上的信号 |
| 格式化（Intl、`useFormat`） | `CultureInfo`（数字、日期、货币）、`TimeZoneInfo` | 同左 | 同左 |
| 减少动态效果 | 系统设置 + 作用域里的 Transitions 资源（DESIGN §8） | 系统设置 + `Motion`（DESIGN §8） | 系统设置 + `Motion` |
| 从右到左 | `FlowDirection` | `FlowDirection` | `RightToLeft` + `RightToLeftLayout` |

### 交互与无障碍

| TS 版 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 原生 popover / dialog | `Popup` / `FlyoutBase`；模态对话框用窗口内遮罩（`OverlayLayer`），要独立窗口时 `Window.ShowDialog` | `Popup`（独立 HWND，阴影要 `AllowsTransparency`）；模态对话框用窗口内遮罩或 `Window.ShowDialog` | `ToolStripDropDown` 承载弹层（失焦关闭、键盘交给它，待验证）；对话框 `Form.ShowDialog` |
| floating-ui 定位 | `Popup.Placement` + `PlacementConstraintAdjustment`（翻转、贴边） | `Popup.Placement` + `CustomPopupPlacementCallback` | 按 `Screen.WorkingArea` 自己算（移植 TS 的 flip / shift） |
| 焦点：打开时进入、模态里循环、关闭后还回 | `FocusManager`，`KeyboardNavigation.TabNavigation="Cycle"` | 同左 | 宿主的 `ProcessDialogKey`（HANDOFF §4.1） |
| WAI-ARIA 角色与状态 | `AutomationProperties` + 自定义 `AutomationPeer`（Invoke、Toggle、ExpandCollapse、Selection、RangeValue、Value） | 同左 | `AccessibleObject` 子树（DESIGN §9 已实测 Invoke、焦点、命中） |
| 键盘映射 | 以 vue-tsx-pro 文档里的键位为准，平台已有的（列表的方向键、Tab）沿用平台实现 | 同左 | 宿主里自己实现 |
| 虚拟滚动（`Virtualizer`） | 列表用 `VirtualizingStackPanel`；表格、树建在自己移植的 Virtualizer 上 | `VirtualizingStackPanel`（回收模式）；表格同左 | 宿主内按 TS 的算法算可见范围 |
| 拖动 `startMove` | 指针捕获（`e.Pointer.Capture`）或 `Thumb` | `CaptureMouse` 或 `Thumb` | `Control.Capture` |
| 文本输入、输入法 | 平台 `TextBox`，换 ControlTheme | 平台 `TextBox`，换模板 | 真的 `TextBox` 子控件（无窗口元素唯一的例外） |
| 波纹 | 模板里的叠加层 + 动画 | 同左 | `Animator` |

### 开发与测试

| 事项 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 设计期 | `Design.PreviewWith` 预览 ControlTheme，`d:DataContext` | 设计器预览、`d:` 设计期数据 | **设计器是主要写法**：属性标 `[Category]`、`[Description]`、`[DefaultValue]`，`ShouldSerializeXxx` / `ResetXxx`；档位类型的 TypeConverter 给出常用取值 |
| 热重载 | HotAvalonia（DESIGN §11） | IDE 的 XAML 热重载 + `MetadataUpdateHandler` | `MetadataUpdateHandler` + `HotReload.CodeVersion` |
| 测试 | headless（DESIGN §12） | STA + 离屏窗口 | STA + 消息循环 |

## 参考源码

本地浅克隆在 `C:\Users\Administrator\RiderProjects\refs\`（不进仓库，只读）：Avalonia（12.1.5）、Semi.Avalonia 与 Ursa.Avalonia（Avalonia 组件库）、HotAvalonia、dotnet/wpf 与 dotnet/winforms（release/10.0）、HandyControl（WPF 组件库）、AntdUI（全自绘的 WinForms 组件库）。查平台行为先读源码，参考别人的做法时写清出处与取舍。

## 阶段

### 0. 骨架（已完成）

- [x] 仓库结构：按平台分目录，每个平台有 src、apps、tests；集中的构建设置与包版本（DESIGN §2）。
- [x] 已验证的基础设施，都配了测试：
  - Avalonia：`EmExtension`、`PseudoClassesExtensions`、`RuneTheme` 入口。
  - WPF：`Provider`（分层合并）、`Em`、`Container`、`BrushTransition`、`Motion`、`HotReload`。
  - WinForms：`Signal` / `Computed` / `Effect`、`Easing`、`Animator`、`Motion`、`HotReload`。
- [x] 三个示例应用：Avalonia 的 Debug 构建带 HotAvalonia。
- [x] 前置探测（2026-11-06）：档位类型、组合档位、部件的转发与层层合并、三个平台的主题切换、Avalonia 桌面热重载、WinForms 无窗口元素的焦点与无障碍。

### 1. 生成脚本（tools/）

数据源都是 vue-tsx-pro。CI 里固定一个提交，跑 `--check`，核对生成文件与源码一致。脚本怎么解析 TS 源码参考 RuneForBlazor 的 `tools/theme-gen`、`lang-gen`、`icon-gen`（读的是同一个数据源）。

- [ ] `token-gen`：从 `packages/ui/src/config/theme` 读 tokens 和 16 套主题：
  - Avalonia：`Tokens.axaml`（字号、em 系数、边框宽度、阴影、时长）；每套主题一个自定义 `ThemeVariant` 的 ThemeDictionary（颜色画刷 + 6 个色调的调色板）；按时长档位的 Transitions 集合（DESIGN §8）。
  - WPF：`Tokens.xaml` + 每套主题一份 `.xaml` 合并字典；颜色、画刷、阴影（`DropShadowEffect` 参数）。
  - WinForms：`Tokens.g.cs`（常量与 em 系数）+ `Themes.g.cs`（16 套主题的 `Palette` 记录）。
  - em 类 token 输出成系数；px 类（边框、焦点环）输出设备无关像素，WinForms 按 `DeviceDpi` 换算。
- [ ] `lang-gen`：从 `config/lang` 生成三个库各自的 `Lang` 记录（zh-CN、en-US），插值文案生成方法，每个键带中文注释。
- [ ] `icons-gen`：Lucide 的路径数据。Avalonia `StreamGeometry`、WPF `Geometry`（冻结）、WinForms `GraphicsPath` 工厂；线宽取 `strokeWidths`。
- [ ] `parity`：用 TS 编译器 API 从 vue-tsx-pro 抽清单（组件、`css.recipe` 的 name / parts / variants、`propsXxx` 部件与它们的类型、ComponentDefaults、Lang 的键），读三个库的程序集对照，报告缺失与改名（改名要在对照表里登记，例如 `open` → `IsOpen`）。
  - 同时输出组件的依赖表与层号（阶段 3 的顺序以它为准）：vue-tsx-pro 改了组件之间的依赖时重算，不手抄。

### 2. 基础设施层

**先做基础设施，再做组件；组件也是先做基础的、再做上层的**（阶段 3）。这一层对应 vue-tsx-pro 的 `utils/` 与 `config/`，按 TS 里被组件用到的次数排了先后（括号里是次数，2026-11-06，vue-tsx-pro `417081a5`）。每一项三个平台各做一份，平台写法见"平台化的约定"。

合并与配置：
- [ ] 部件的转发与层层合并（`utils/props.ts`，178）：DESIGN §7 的三种写法落地。
  - Avalonia：部件名常量（`ButtonParts.Icon = "PART_Icon"`），主题里给每个内部节点起名；不需要新的运行时。
  - WPF：Provider 合成递归进部件样式的 `Style.Resources`（嵌套组件的部件也叠在外层结果上，DESIGN §7 实测发现的缺口）；Provider 的合成做成附加属性，任何元素（包括组件自己）都能当一层，单个实例不用再包 Provider。
  - WinForms：部件属性记录与源代码生成器（`[PartProps]` 生成逐字段合并、嵌套记录递归合并、事件委托"使用方先执行"）。
- [ ] 作用域与 Provider（`config/context`，121）：主题、语言、默认值、部件样式沿作用域继承。
- [ ] 组件默认值（`config/defaults`，19）与配方（`config/styles`，25）：DESIGN §4、§5。

主题与样式：
- [ ] token 与主题（`config/theme/tokens`，86）：接上阶段 1 的生成结果；16 套主题与局部主题切换。
- [ ] 档位类型（DESIGN §3）、字号与间距档位（`utils/style/styles`、`typography`，48 / 36）、色调与调色板（`tone-styles`，14）。
- [ ] 过渡与减少动态效果（`transitions`，32）、焦点环、骨架扫光（`shimmer`，9）、列表项样式（`item-styles`，6）。

语言与格式：
- [ ] 语言包与格式化（`config/lang`，31 + 16）：`Lang`、`CultureInfo` 格式化；数字解析与精确运算（`utils/data/number`、`numeric`，31 / 23）、日期工具（16）、文字匹配（12）。

交互：
- [ ] 开合状态与关闭原因（`open-change`，19）、浮层定位（`floating`，13）、焦点进出与循环（`focus`、`overlay`）。
- [ ] 出现与离场（`presence`，12）、防闪烁的加载指示（`delayed-loading`，11）、尺寸观察（`resize`，11）。
- [ ] 拖动（`move`，9）、快捷键（`hotkeys`，8）、读屏播报（`announcer`，6 → UIA 通知）、漫游焦点（`roving-focus`，5）、按字母跳转（`typeahead`）。
- [ ] 选择模型（`selection`，9）、树数据（`tree`，5）、虚拟化（`virtualizer`）、波纹（`ripple`）。

平台各自的：
- Avalonia：
  - [ ] 调色板 `TonePalette` 与生成的资源；用真实 Button 模板复测 DESIGN §4 的开销。
  - [ ] `rune:Provider`：Styles、默认值、主题作用域（`ThemeVariantScope` 加自定义主题）、语言。
  - [ ] 开发期钩子：HotAvalonia 重新加载后，给代码创建的已有控件重新套主题（DESIGN §11）。
- WPF：
  - [ ] Provider 接上默认值、语言、局部主题（换合并字典）；上面"合并与配置"里的两项。
  - [ ] 在 Rider / VS 里实测改 Provider.Styles 的 XAML 热重载（DESIGN §13）。
- WinForms：
  - [ ] `ElementHost` 正式化：布局（Flex 的子集）、焦点、Tab、键盘、命中测试、无障碍（HANDOFF §4.1 的做法）、DPI 变化。
  - [ ] 单元素宿主 `ElementControl<T>`：一个元素包成一个控件，能放进设计器（属性、序列化、默认值）。
  - [ ] 配方结果按（档位组合, 作用域）共享 `Computed`；Provider 作用域控件（主题、默认值、语言信号）。

用来验证这一层的第一批组件（都在阶段 3 的第 1、2 层，做完就是正式组件）：Icon → Text → Spinner → Badge → Button 家族 → Field。每个都把档位、调色板、部件转发、Provider 的默认值与样式、size 与 em、主题切换、减少动态效果、热重载、无障碍走一遍。

### 3. 按依赖分层移植组件

**一个组件开工前，它用到的组件在这个平台上必须已经完成**；上层组件只组合下层组件，不另写一遍布局、文字、动效（同 vue-tsx-pro COMPONENTS.md §10 的原则）。

分层取自 vue-tsx-pro `packages/ui/src` 的导入关系（2026-11-06，`417081a5`）：组件只用基础设施的是 L0，否则是"用到的组件的最大层号 + 1"。核心组件分到 L0–L10，patterns 到 L12。下面把它并成五层，组件名后面是 TS 的层号；`←n` 是被多少个组件文件直接用到，越大越要先做扎实。之后以 `parity` 生成的依赖表为准。

**第 1 层 基础组件（TS L0–L2）**：只用基础设施，或只用本层更早的组件。
- 文字与标记：Icon（L0，←51）、Text / Paragraph / Strong / Em / Small（L0，←18）、Heading（L1）、Code、CodeText、Kbd、Mark、Blockquote、Link（L1）、List、Spinner（L1，←10）、Badge（L1，←8）、StatusDot、Indicator
- 布局：Flex（L0，←16）、Grid、Container、Separator、ScrollArea（L0，←20）、PageScrollbar、ResizeHandle、Affix、AsideLayout、Prose、ViewportFill
- 展示与反馈：Collapse（L0）、Collapsible（L1）、Skeleton（L0，←16）、SkeletonText、Empty（L1）、Result、Progress、LoadingBar、LoadingOverlay、Avatar、AvatarGroup、Card（L2）、Stat、AnimatedNumber、ValueChange、ProportionBar、Checklist、ItemList、Marquee、Watermark、Gauge、Sparkline、VirtualList、Ellipsis
- 浮层原语：Popover、Tooltip、HoverCard（L0：TS 里它们不依赖别的组件，只用定位与开合的基础设施）
- 导航原语：Toolbar、Pager、TableOfContents
- 内核：按钮内核（`button-core`，L2，←15）、表单骨架 Field（L2，←30）/ Form / FormErrorSummary / CharacterCount、静态 Table 与列定义（`columns`）、日期内核（`date-locale`、`date-segments`、`calendar-state`、`clock`、DateSegmentGroup、RelativeTime、Countdown）

**第 2 层 交互基础组件（TS L3–L5）**：
- 按钮家族：Button（L3，←25）、IconButton（L3，←30）、LinkButton、IconLinkButton、CopyButton、ButtonGroup、CloseButton（L4）
- 选择与输入原语：Checkbox、Radio、Switch、Slider、Rating、OtpField、FileDrop、Listbox（L3，←8）、ListPopover、ToggleGroup、ControlGroup、输入框外壳（`input/control`，L5，←15）
- 标记与提示：Tag（L5）、Alert（L5）、Banner
- 浮层：Menu（L4，←8）、ContextMenu、对话框内核（`ModalCore`，L5）、Popconfirm、Tour、Lightbox、Image
- 导航：Tabs、Steps、NavList、NavMenu、NavBar、Pagination、Breadcrumbs
- 展示与布局：Accordion、Tree、Sortable、Timeline、Descriptions、CodeBlock、FieldCompare、Splitter、BackTop、FormSection
- 日期与表格：CalendarView、Calendar、RangeCalendar、CalendarBoard、ColumnTable、PivotTable、SelectionBar、ColumnSettings

**第 3 层 复合组件（TS L6–L8）**：
- 文本与数字：TextField（L6，←9）、Textarea、NumberField、NumberRangeField、ColorField
- 选择：Select（L7，←8）、Combobox、Autocomplete、TagsInput、SelectedTags、Mentions、Transfer、Cascader、TreeSelect、PeoplePickerPanel、PeoplePicker
- 浮层与反馈：Dialog、Drawer、AlertDialog、CommandPalette、Toast；上传 Upload
- 日期：SegmentedField、DateField、TimeField、TimePicker、QuarterCalendar
- 导航与表格：PageJump、PageSizeSelect、DataTableView、ColumnHeaderActions

**第 4 层 上层组件（TS L9–L10）**：DatePicker、DateRangePicker、PaginationBar、DataTable、TreeDataTable、ConditionBuilder。

**第 5 层 页面组合（TS 的 patterns）**：ActionBar、AuthLayout、DeadlineBadge、KanbanBoard、NotificationCenter、SlideCaptcha、StepsForm、ThemeSwitcher、AppShell、FormDialog、CommentThread、DataImport、FilterBar、QueryTable。主题定制、charts 等扩展之后再定。

TS 里专为浏览器的不做：VisuallyHidden（桌面上用 `AutomationProperties.Name` / `HelpText`）、SkipLink、ClientOnly、Can；Ellipsis、Watermark 这类按平台能力决定。

同一层里按 COMPONENTS.md §10.6 的分类先后做；文本输入（TextField）是平台差异最大的地方，第 3 层先做它。各类组件的平台要点：

| 类别 | 平台要点 |
|---|---|
| 文字 | Avalonia / WPF 的 Text 系列派生自 `TextBlock`（一个元素一个组件，各自的默认字号与字重） |
| 轻量标记 | Icon、Badge、Tag、StatusDot、Skeleton 自绘（DESIGN §9） |
| 布局 | Flex、Grid 是自定义面板（gap、align、justify、wrap、`minItemWidth` 自动换列）；Container 的断点用容器查询（Avalonia `ContainerQuery`、WPF `Container`）；ScrollArea 是平台 ScrollViewer 换主题（浮层滚动条，Avalonia `AllowAutoHide`） |
| 展开收起 | Collapse 测量内容高度后动画 Height，收起后不参与布局、不进 Tab 顺序 |
| 选择类 | Tabs、NavList、Listbox 建在平台的选择类基类上；勾选、单选、开关建在 `ToggleButton` 上（三态、UIA 的 Toggle 平台自带） |
| 浮层 | Tooltip、ContextMenu 先看平台自带的能否换主题做到；对话框走窗口内遮罩；关闭原因、焦点还回照 TS；Toast 走窗口里的宿主 |
| 输入 | 文本类包平台 TextBox；精确数字用 `decimal` |
| 表单 | 校验接 `INotifyDataErrorInfo`；必填、长度从 DataAnnotations 推出 |
| 表格、树 | 建在移植的 Virtualizer 上，单元格自绘 |
| 日期 | `DateOnly` / `TimeOnly` / `DateTimeOffset`，周起始、月份名取 `CultureInfo` |

RuneForBlazor 已经走过一遍这些组件，同一个组件先看它踩过的坑。

每个组件的完成标准：
1. 外观对齐：配方逐条移植，parity 报告没有未解释的差异（亮、暗主题）；
2. API：参数、默认值、部件与 vue-tsx-pro 对应，形状符合"平台化的约定"；每个内部节点都有部件名，嵌套组件的部件能从外层转发、层层合并（DESIGN §7）；公开成员有中文文档注释；
3. 行为测试：受控与非受控用法、主要分支、无障碍属性；
4. 无障碍：键盘与读屏符合 WAI-ARIA 的做法（以 vue-tsx-pro 的实现为准），UIA 客户端能看到名称、角色、状态；
5. 性能：大量重复的部件自绘；档位变化只切伪类或触发器；大列表虚拟化；
6. 文档：Gallery 示例照 vue-tsx-pro 文档站；DESIGN 记下新的取舍与坑；本文勾选。

### 4. 质量与发布准备（贯穿全程，最后收尾）

- [ ] 截图对照：三个库各渲染示例成图片，与 vue-tsx-pro 文档站的截图比较（亮、暗主题），差异人工确认。
- [ ] UIA 自动化测试覆盖有键盘、焦点逻辑的组件。
- [ ] CI：WPF、WinForms 在 Windows 上构建测试，Avalonia 另加 Linux；生成脚本 `--check`。
- [ ] 打包：三个库各一个 NuGet 包，本地安装试用；发不发、发到哪由用户决定。

## 待定的决定

每项后面是建议。

1. **推进节奏**：基础设施层（阶段 2）三个库一起做，平台差异早点暴露。组件按层推进，每个组件先做 Avalonia（伪类、选择器最接近 TS 的写法），WPF 紧跟；WinForms 可以落后一层，但不跳层。
2. **WinForms 的范围**：它全靠自绘，工作量最大。先做到第 3 层（复合组件，覆盖常见的表单、选择、弹层），第 4、5 层（DataTable、日期选择、页面组合）等前面稳定后再定。
3. **Avalonia 热重载**：用 HotAvalonia（MIT，桌面窗口里已实测可用，只在 Debug 构建里引用，不进包的依赖），不用官方授权版。
4. **对照粒度**：parity 除了名字，再比对配方取值（每个部件在各档位下用哪个 token），三个库的取值从生成的数据和程序集里读出来比。截图比对放到阶段 4。
5. **生成脚本的语言**：node，读 TS 源码最直接，RuneForBlazor 的脚本可以参考。只用 node 内置模块和 vue-tsx-pro 自带的 `typescript`。
6. **测试与工具的依赖**：Playwright（截 vue-tsx-pro 文档站）、FlaUI（UIA 测试，MIT）只用在测试与工具里，不进组件库的依赖；到阶段 4 再引入。
