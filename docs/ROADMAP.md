# 路线图

状态标记：`[ ]` 未开始、`[~]` 进行中、`[x]` 完成、`[!]` 待决（原因写在该项下面）。

已完成部分的写法与取舍见 [DESIGN.md](DESIGN.md)。

## 目标

**三套 .NET 桌面组件库（Avalonia、WPF、WinForms），对标 vue-tsx-pro 的 `packages/ui`。**

- **外观和行为以 TS 版为准，尽量一致**：token、主题由脚本生成；组件的配方按 vue-tsx-pro 对应组件逐条移植（尺寸、内边距、圆角、颜色 token、状态、过渡），行为以它的测试用例为规格，不凭印象重画；做不到的写进 DESIGN。核对靠工具（"信息与验证工具"），每个组件过样式、行为、无障碍三道核对。
- **写法是各平台地道的写法**：API 的形状按平台习惯（见"平台化的约定"），不照搬 Vue 的组合式写法。
- **三个库各自独立**：不共享代码，一致性靠 `tools/` 生成与对照。
- **范围是组件库的全部内容，只有发布暂时不做**（决定 22、23）：核心约 180 个组件与全部扩展（charts、maps、diagram、code-editor、xlsx、theme-customize、three）三个平台都尽量做，平台上做不到的写清原因；docs-kit 用来做 Gallery 的示例页；只对浏览器有意义的（SkipLink、ClientOnly、VisuallyHidden）登记为不适用。
- **上游有问题就反馈**：移植时发现 vue-tsx-pro 的 bug（行为与文档不符、对比度、从右到左、键盘缺失），记进 UPSTREAM.md 的"上游问题"，发给维护 vue-tsx-pro 的会话，由那边修复发版，我们升级锁定的版本（决定 24）。

## 现状（2026-11-06）

- 阶段 0、1 完成：三个平台的骨架、示例应用、测试工程；工具与生成脚本（ts-inspect、token-gen、lang-gen、icons-gen、recipe-gen、parity、三个平台的 inspect 与 bench）。
- 组件：Icon、Text 家族（5 个）、Spinner、Badge、Button 家族（Button、IconButton、CloseButton、ClearButton、LinkButton、IconLinkButton，Avalonia、WPF 另有 ButtonBase）三个平台都做了，162 个测试。
- 做过一轮性能（DESIGN §9"组件基准"）：Avalonia 按钮首次布局快 36%、WinForms 快 4 倍。
- 上游 vue-tsx-pro 做完了样式体系改造（0.11，分支 feat/style-overhaul，等合入 master），对照与要跟进的在 [UPSTREAM.md](UPSTREAM.md)。
- M1 进行中（2026-11-07）：Kbd、Code、Mark、Blockquote、StatusDot、H1–H6、Link、CodeText、List / OrderedList / ListItem、Indicator 三个平台都做了（Text 页的示例都有了，Avalonia、WPF 取值与文档站一致；List 有 basic 示例，icons 等 Card、Flex、Grid；Indicator 的示例在上游的 Avatar 页，等 Avatar）；TextSize 加了 xxl、xxxl，新的档位类型 `Space`（gap）。布局的 Flex / FlexList / FlexOrderedList、Grid / GridItem / GridList / GridListItem、Separator、Container 三个平台都做了（flex、grid 的计算是三个库各一份的孪生文件，`tools/twins` 在 CI 里核对）。已知缺口：WinForms 的 propsCode（CodeText）、propsBadge / propsCount（Indicator）、propsContent（ListItem）、propsLabel / propsLine（Separator）——WinForms 部件只有元素才有部件属性记录，这几处的部件是控件或 GDI 画的，等 `Sx` 记录（工作包 5）一起定。布局页（Flex、Grid、Separator、Container 共用一页）的示例要等 Card、Avatar、Switch、ToggleGroup、Field、TextField。Toolbar（漫游焦点 `RovingFocus`，以后的 Tabs、Menu、ToggleGroup 共用）、Prose、StatusBadge（状态映射 `StatusMap`）、Skeleton 也做了。SkeletonText、Card（装饰效果 tilt / spotlight / beam 还没做，要指针阻尼动画）、Avatar、Empty 也做了。WinForms 另外的缺口：Card 的 elevated 阴影（画不到控件外）、骨架换回正文的淡入（子控件不能半透明）。三个平台补了 List icons、Avatar（含 Indicator）、Card、Empty 的示例（布局页等 Switch、ToggleGroup、Field、TextField）。Collapse 也做了（Collapsible、Accordion 一起做，示例等 Field）。Progress 也做了（progress、rings、shapes 三个示例；WinForms 缺 propsIndicator、propsHeader）。代码组合控件的部件默认值已统一移进主题（DESIGN §7），WinForms 用 `TextProps` 补了 Empty 的 PropsTitle / PropsDescription、Separator 的 PropsLabel。临时替代清理（2026-11-07）：示例里的平台面板都换成 Flex / Grid / Text / Card，补了 Icon with-text、Button basic、IconButton links；Card 页脚放布局容器时撑满一行，Avalonia 的 Link 给混排内容里的文字下划线，WinForms 的 LinkControl 有了基线。Switch 三个平台都做了（勾选类共用的 `ToneFill`），Card 加载示例改用它。还缺的基础：TextField（IconButton 的 states、presets，Button 的 types 等），WinForms 的 Provider / `Sx`（工作包 5）与 WPF 任意元素当一层（方案待定），多元素宿主（WinForms 按钮里放 Badge），Icon 的描线动画。Checkbox、Radio 与复选组、单选组三个平台都做了（勾选类共用的 `ChoiceBase`，选项记录 `ListboxOption`），checkbox 页的示例都有了。Field（与表单标签 FieldLabel）三个平台都做了：标签、说明、错误，出错、必填、只读交给里面的控件，标签在起始边时按基线对齐（DESIGN §14 Field）；表单（Form、useForm）、字段布局的统一设置还没做。CopyButton、ScrollArea（浮层滚动条，DESIGN §14）、CodeBlock（内置简单高亮）、Tabs（核心）、Table（静态表格，列宽照 CSS 的 table-layout: auto）、NavList（NavItem、NavGroup、NavSection，紧凑模式）、TableOfContents（滚动高亮）、ToggleGroup（分段控件）、TextField（输入框外壳 ControlVariant、CharacterCount）、Tooltip（第一个浮层：沿用平台的提示，组件库补外观、位置、箭头、键盘；WinForms 是扩展组件 TooltipProvider，DESIGN §14）三个平台都做了。AppShell（顶栏兼作窗口标题栏，自绘窗口按钮，DESIGN §14）、Textarea 与 TextField 的 format、Listbox（列表状态 ListState 是孪生文件）、Popover（WinForms 是画面、内容两层窗口）、Tag（可移除，离场后才请求移除）、Select（单选、多选、搜索、新建，SelectedTags）、Dialog（模态内核；WinForms 是遮罩、面板两层窗口）、CommandPalette（快捷键 Hotkey 是孪生文件）三个平台都做了；跟到阶段 25 第三批：单选、复选的卡片外观与 End，可选中的 Tag，链接下划线的容器作用域（Card 页头）。下一个：静态 Table 的表头吸顶与固定首列（stickyHeader、sticky，连同 ScrollArea 的 both 示例），然后按 DOCSITE 的顺序做文档站要用的组件（ItemList、Menu、Select 的远程选项、Dialog 的命令式打开与 AlertDialog），Form（TextField 的 form 示例、Form 页要它与 Select、NumberField）。正式文档站与像素级对照的规划见 [DOCSITE.md](DOCSITE.md)（决定 26）；像素对照的工具已经通了（ts-shots、pixels.mjs），已做的页面有了差异总表。进度以 `node tools/parity/parity.mjs` 为准；每个组件跑 `tools/parity/styles.mjs`（取值）与 `behavior.mjs`（行为）。
- 生成文件锁定上游的开发线（`tools/vue-tsx-pro.ref`，现在是 3c0c817b = 0.12.0-dev 阶段 25 第三批）。下一步：里程碑 M0。

## 已定的决定

| # | 事项 | 决定 |
|---|---|---|
| 1 | 参照 | 以 vue-tsx-pro（TS 版）为准，外观尽量一致；RuneForBlazor 只作参考（进度、踩过的坑、"Blazor 化的约定"的思路），不共享代码 |
| 2 | 结构 | 不做单源、不做核心包；三个库各自独立，写法贴合各自平台 |
| 3 | 分层合并 | 组件 < 档位 < 组合档位 < 应用级 < 外层 Provider < 内层 Provider < 使用方，上层只写要改的部分；可以有适量的运行时 |
| 4 | 尺寸 | 沿用 rem / em（DESIGN §6） |
| 5 | 更新 | 细粒度更新，只更新该更新的节点（DESIGN §10） |
| 6 | 开发体验 | 开发期尽量热重载（DESIGN §11） |
| 7 | 骨架 | 支撑长期开发，不建临时目录；一次性的探测不进仓库，结论写进 DESIGN；反复要用的（检查、截图、基准）做成 `tools/` 里的正式工具 |
| 8 | 部件 | 每个子节点的 API 都能从外层转发，层层合并（DESIGN §7） |
| 9 | 顺序 | 先做基础设施，再按依赖从下往上做组件：基础组件 → 交互基础 → 复合 → 上层 → 页面组合（阶段 2、3） |
| 10 | 档位类型 | 配方的档位维度（variant、tone、radius、size……）用字符串包装类型，size 另外接受数字；行为类的封闭集合用 enum（DESIGN §3） |
| 11 | Avalonia 组合档位 | 调色板对象：色调样式设 `Palette`，样式档位绑定调色板的画刷；开销成问题时退到"每种样式一个 ControlTheme"（DESIGN §4） |
| 12 | WinForms 组件形态 | 无窗口元素是本体；每个组件另有单元素宿主，能放进设计器；数量大的地方直接用元素（DESIGN §9） |
| 13 | 推进节奏 | 基础设施层三个库一起做；组件按层推进，每个组件先做 Avalonia、WPF 紧跟；WinForms 可以落后一层，但不跳层 |
| 14 | WinForms 范围 | ~~先做到第 3 层（复合组件）；第 4、5 层等前面稳定后再定~~ 改为决定 22 |
| 15 | Avalonia 热重载 | HotAvalonia（MIT），只在 Debug 构建里引用，不进包的依赖 |
| 16 | 对照粒度 | parity 比对名字与配方取值（每个部件在各档位下用哪个 token）；截图比对放在阶段 4 |
| 17 | 工具的语言 | 读 vue-tsx-pro 的工具用 node，只依赖内置模块与 vue-tsx-pro 自带的 `typescript`；要加载三个库的工具用 C# |
| 18 | 测试与工具的依赖 | Playwright、FlaUI 只用在测试与工具里，不进组件库的依赖 |
| 19 | WPF 的 Text 家族 | 从 TextBlock 派生（行内内容、截断、读屏现成）；模板里的 Text 给部件样式键，Provider 合成部件键时叠在同一组件的类型键结果上，"所有实例"的样式就能进模板（2026-11-06 用户确认） |
| 20 | 自绘组件的部件 | 底色、描边、圆角自己画；图标、文字等部件是代码里组合的真组件（不建模板）：Avalonia 用子选择器 `rune\|Badge > rune\|Icon#PART_Icon`，WPF 子元素引用部件样式键，WinForms 用部件属性记录（2026-11-06 用户确认） |
| 21 | 跟进上游 0.11 | UPSTREAM.md 的 5 个工作包都做：`Tone` 改名 `TextTone`（不留旧名）、组件段只用主题设值、Provider 合成照上游的分段语义（跨模块，WPF 先做）、Badge 色调取共用色表、WinForms 定制入口；另有按钮字重 500、控件高度收成一处（2026-11-06 用户确认按规划修订路线图） |
| 22 | WinForms 的范围 | 改决定 14：核心五层与扩展都做，和另外两个平台一样（2026-11-06 用户：除了发布，其他都尽量做） |
| 23 | 扩展与发布 | 核心五层之后依次做 theme-customize、charts、maps、diagram、code-editor、xlsx、three，三个平台都尽量做（three 先评估各平台的 3D 能力，做不到的写进 DESIGN）；docs-kit 做成 Gallery 的示例页（代码预览、属性表）。NuGet 包本地能打出来、能装上试用，但不发布（2026-11-06 用户：发布暂时不做） |
| 24 | 锁定上游 | 跟上游的开发线（0.11 之后是 0.12.0-dev，用户 2026-11-06 定；上游之后会发布）：生成文件与 CI 对齐 `tools/vue-tsx-pro.ref` 锁定的提交，锁定的是 GitHub 上能取到的提交。上游推进时主动跟：改锁定、重新生成、按上游的变化清单逐条对照（UPSTREAM.md），一次专门的提交。上游的问题发给维护 vue-tsx-pro 的会话修复发版，不直接改那边的仓库；有设计分歧时问用户 |
| 25 | 完成标准 | 每个组件过三道核对：样式（ts-styles 的计算样式对 inspect props，加截图）、行为（上游测试用例逐条对应或登记不适用）、无障碍（UIA 对 ARIA 角色与状态）；parity 报告里没有未解释的差异才勾选（2026-11-06） |
| 26 | 文档站 | 三个平台各做一个桌面文档应用（替换现在的 Gallery），页面、示例与 vue-tsx-pro 文档站一一对应，用组件库自己的组件搭；与上游的像素对照另出静态 HTML 报告，不放进文档应用（用户 2026-11-07 定，规划见 DOCSITE.md） |

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
| 原生 popover / dialog | `Popup` / `FlyoutBase`；模态对话框用窗口内遮罩（`OverlayLayer`），要独立窗口时 `Window.ShowDialog` | `Popup`（独立 HWND，阴影要 `AllowsTransparency`）；模态对话框用窗口内遮罩或 `Window.ShowDialog` | 弹层是两层窗口：分层窗口画面板、阴影与箭头，上面的无边框窗体放内容、失去激活即关闭（Popover，DESIGN §14）；对话框 `Form.ShowDialog` |
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

## 自主执行的规矩（用户不在时照此推进，2026-11-06 起）

- **小步提交，直接落在 master**：每完成一项（一个工具、一块基础设施、一个组件或一组紧密相关的组件），跑相关测试与生成脚本的 `--check`，提交；不开 PR、不发包。整理与功能分开提交。
- **本地只验证改动相关的，全量交给 CI**（2026-11-06 起）：改了哪个平台就构建、测试哪个平台，相关工具跑 `--check`；三个平台的全量测试、示例截图、生成文件核对由 `.github/workflows/ci.yml` 并行跑。不等 CI，CI 报的问题下次提交前修。本地要跑全量时在另一个 git 工作树里后台跑，主工作区继续。
- **先查后写**：做组件前跑 `ts-inspect component <名字>` 与 `deps <名字>`，读 vue-tsx-pro 的源码与 RuneForBlazor 的同名组件；改 C# 用 LSP 查定义与引用。
- **遇到下面的情况，这一项停下，写进"待定的决定"**（日期、项、卡在哪、要决定什么、建议），转去做下一个不依赖它的项：要加"已定的决定"之外的依赖；vue-tsx-pro 的做法在某个平台上做不到、又有多种合理的替代；要改已定的决定或公共 API 的形状（已定的决定里列了的除外）；和上游有设计分歧。
- **组件的流水线**：`ts-inspect component` / `deps` 与上游的测试名 → Avalonia（组件、移植测试、inspect 对 ts-styles、截图、Gallery 示例）→ WPF 同样 → WinForms（最多落后一层，不跳层）。每个组件每个平台一次提交。上层只组合下层组件，要的能力先补进基础层。
- **上游的通知与问题**：维护 vue-tsx-pro 的会话会发来变化通知，对照写进 UPSTREAM.md；上游合入并发版后才改锁定（决定 24）。发现上游的问题时，在 UPSTREAM.md 的"上游问题"记一行（现象、复现、建议），再发消息给那个会话；修好发版后升级锁定，勾掉这一行。
- **并行**：耗时的构建、测试、采样挂后台；互不依赖的工具（ts-styles、截图对比）可以在 git 工作树里交给后台子代理，做完合回 master 复核。同一个组件的三个平台按顺序做，不并行改同一组文件。
- **不写过程流水账**：结论进 DESIGN（取舍与坑）或本文（勾选、一行结论），过程留在提交说明里；每完成一步更新 HANDOFF 的现状与下一步，换会话也能接上。

## 参考源码

本地浅克隆在 `C:\Users\Administrator\RiderProjects\refs\`（不进仓库，只读）：Avalonia（12.1.5）、Semi.Avalonia 与 Ursa.Avalonia（Avalonia 组件库）、HotAvalonia、dotnet/wpf 与 dotnet/winforms（release/10.0）、HandyControl（WPF 组件库）、AntdUI（全自绘的 WinForms 组件库）。查平台行为先读源码，参考别人的做法时写清出处与取舍。

## 信息与验证工具

移植一个组件时，要反复回答这几个问题：
- TS 版这个组件到底怎么写的（props、部件、配方、默认值、依赖）？
- 我们做出来的长什么样？每个部件取到了什么值、值从哪一层来？读屏看到了什么？
- 改了基础设施之后，开销变了没有？

这些都做成**一行就能跑的命令**，人和 Claude 用同一套：
- 输出确定，同样的输入给同样的结果。
- 文本用 Markdown 表（默认）或 JSON（`--json`），图片用 PNG（Claude 能直接看图）。
- 产物写到 `artifacts/`（不进仓库），文件名带上组件、示例、主题、状态。
- 每做好一个工具，就把命令写进 HANDOFF 的命令表。

| 工具 | 位置与语言 | 回答什么 | 输出 |
|---|---|---|---|
| `ts-inspect` | `tools/ts-inspect`，node + vue-tsx-pro 自带的 `typescript` | `component <名字>`：文件、props（类型、默认值、注释）、部件与 `propsXxx` 的类型、配方的 variants 与 compound（逐条列出 token）、ComponentDefaults、用到的组件与基础设施、层号、被谁用到。<br>`token <名字>`：这个 token 在 16 套主题里的取值。<br>`deps`：依赖分层表。<br>`lang <前缀>`：语言包的键与中英文案 | Markdown / JSON |
| `ts-styles` | `tools/ts-styles`，node；Playwright 借用 vue-tsx-pro 浏览器测试装好的那份（同 RuneForBlazor 的 `tools/parity`），不新增依赖 | 文档站上某个示例里，每个部件计算后的样式（亮、暗；常态、悬停、按下、聚焦），按 `(示例, data-scope, data-part, 第几个)` 编号；外加示例截图 | JSON + PNG |
| `inspect` | 三个平台各一个控制台程序，C#：`avalonia/tools/`、`wpf/tools/`、`winforms/tools/` | `snapshot <组件/示例>`：渲染成 PNG（主题、宽度、DPI、状态可选）。<br>`tree`：元素树，每个部件的名字、类型、边界。<br>`props`：每个部件解析后的属性（背景、前景、边框、内边距、字号、圆角、透明度）与值的来源（主题、Provider、使用方、本地值）。<br>`uia`：UI Automation 树（名称、角色、状态、支持的 Pattern、键盘焦点） | PNG、Markdown / JSON |
| `bench` | `inspect bench <场景>` | 把这轮的探测做成固定场景：3000 个控件的创建与布局、亮暗切换、换档位、WinForms 的失效与重画 | Markdown 表（多轮的中位数） |
| `parity` | `tools/parity`，node | 读上面几个工具的 JSON 做比对：名字（组件、部件、档位、默认值、语言键）与取值（部件在各档位下的 token、尺寸）；报告缺失、改名与差异 | Markdown 报告 |
| 行为对照 | 并进 `ts-inspect` 与 `parity` | 上游一个组件的测试用例（`describe` / `it` 的名字）；我们的测试用 `[Upstream("…")]` 之类的标注对上，或在 registry 里登记"平台不适用"和原因；parity 报出没对上的 | Markdown 表 |
| 截图对比 | `tools/` 里，node | 同一个示例在 ts-styles 与 inspect 出的图并排、算差异，CI 上传；差异人工确认后进基线 | PNG + Markdown |

做法：
- **示例登记**：Gallery 的示例按"组件 / 示例名"登记，`inspect` 与 Gallery 共用这张登记表。示例名与 vue-tsx-pro 文档站的小节 id 一致，`ts-styles` 和 `inspect props` 才能逐个配对。
- **出图**：
  - Avalonia：headless 打开 Skia 渲染（`UseSkia()`，`UseHeadlessDrawing = false`），用 `CaptureRenderedFrame()` 取图；
  - WPF：`RenderTargetBitmap`；
  - WinForms：画到 `BufferedGraphics` 上再存图（DESIGN §9）。
  - 悬停、按下、聚焦这些状态由工具直接设，不靠真实鼠标。
- **值的来源**：Avalonia 用 `Avalonia.Diagnostics` 的 `GetDiagnostic(property)`（带优先级），WPF 用 `DependencyPropertyHelper.GetValueSource`，WinForms 取解析链上命中的那一层。
- **做的先后**：
  1. `ts-inspect` 最先，token-gen、lang-gen、parity 都用它的解析；
  2. 三个平台的 `inspect`（snapshot、tree、props、uia）在阶段 2 的第一个组件之前做好；
  3. `ts-styles` 与 parity 的取值比对，在做 Button 时接上；
  4. `bench` 在用真实 Button 复测调色板开销时建立（2026-11-06 建好：`inspect bench`，三个平台；结论见 DESIGN §9"组件基准"）。

已经有、不用自己做的：
- Rider 的 MCP：符号、引用、编译问题、运行配置；
- context7：第三方库的文档；
- `refs/` 里的平台源码；
- 内置浏览器：打开 vue-tsx-pro 文档站（`pnpm dev:docs`，端口 5174）看示例和交互。

## 里程碑（2026-11-06 重新规划）

下面的阶段 0–4 是按内容分的；推进的先后按里程碑走，每个里程碑做完更新"现状"。

| 里程碑 | 内容 | 对应 |
|---|---|---|
| **M0 对齐上游、补齐核对工具** | 见下面的清单 | 决定 21、24、25；UPSTREAM.md |
| **M1 基础设施与第 1 层** | 阶段 2 没勾的基础设施，各自跟第一个要用它的组件一起做；第 1 层约 50 个组件 | 阶段 2、3 第 1 层 |
| **M2 第 2 层** | 约 55 个：选择与输入原语、Tag / Alert、Menu、对话框内核、导航、Accordion / Tree、日历、静态表格 | 阶段 3 第 2 层 |
| **M3 第 3 层** | 约 30 个：TextField 先做，选择类、Dialog / Drawer / Toast、Upload、日期输入 | 阶段 3 第 3 层 |
| **M4 第 4、5 层** | DatePicker、DataTable、TreeDataTable、ConditionBuilder；页面组合 | 阶段 3 第 4、5 层 |
| **M5 扩展** | theme-customize、charts、maps、diagram、code-editor、xlsx、three（三个平台）；docs-kit 做 Gallery 的示例页 | 决定 23 |
| **M6 收尾** | UIA 自动化测试、截图基线、NuGet 包本地打包试装（不发布） | 阶段 4 |

### M0 清单

- [x] CI 与生成脚本锁定上游提交（`tools/vue-tsx-pro.ref`；读到别的提交时工具提醒）。
- [x] 锁定上游 0.11.0（da5a4970），重新生成 token 与语言包（去 ocean、加 high-contrast-dark、着色页面、状态文字、占位文字）；之后跟 0.12.0-dev。
- [ ] 截图核对新的页面底色、白卡片、状态文字（ts-styles 做好后）。
- [x] 工作包 1：`Tone` → `TextTone`（三个平台，不留旧名；属性名 `Tone` 不变）。
- [x] 工作包 2：档位的值放到使用方盖得过的一层（DESIGN §4"档位的优先级"）：WPF 全部改成主题样式的触发器；Avalonia 不继承的（调色板、圆角）走主题 setter（`Resolved`），继承的（字号、字重、文字颜色）按模板优先级（Avalonia 的绑定回不到继承的值）。
- [x] DESIGN 写明各平台的定制入口与不稳定的资源键（§4"定制入口"）。
- [~] 工作包 3：Provider 合成照上游的分段语义。WPF 做了（`StyleCloner` 把外层的条件值带进内层的普通值，150 组随机分层对参考模型）；Avalonia 见"待定的决定"。
- [x] 工作包 4：Badge 色调取共用色表（Avalonia 取调色板对象，WPF、WinForms 由 recipe-gen 生成 Badge 配方；`StatusColors` 删掉）。
- [~] 工作包 5（2026-11-07 做了作用域、默认值、Sx：`ProviderControl`，按钮家族、Badge、Link、Indicator、Avatar、Card、Progress、Empty、Switch 的默认值与 `Sx` 都已接上；见 DESIGN §4"WinForms：分层解析"）：WinForms 的定制入口：并进 M1 的 WinForms Provider（作用域控件、默认值信号、`Sx` 记录，"平台化的约定"已定的形状），一开始就按上游语义逐属性合并。
- [x] 按钮字重 600 → 500。
- [x] 控件高度收成每个平台一处 `ControlHeight`（`max(round(系数 × 字号 ÷ 4) × 4, 24)`，xs 档系数 2，Avalonia、WPF 的系数继承；DESIGN §14 Button 家族）。
- [x] `ts-styles`（文档站每个示例、每个部件的计算样式，亮暗、悬停 / 聚焦 / 按下）与 `inspect styles`（Avalonia、WPF，同一种格式），`tools/parity/styles.mjs` 逐项比对。Button 的 variants、tones、sizes、radius 两个平台在亮暗主题下都一致（顺手修了按钮文字的行高与竖直对齐）。
- [ ] WinForms 的 `inspect styles`：元素在绘制时才解析外观，要给元素加一个报出解析结果（各部件的边界、颜色、内边距、圆角、字号）的内部接口。
- [ ] 行为对照（上游测试用例逐条对应）、截图对比；拿已做的 16 个组件过一遍三道核对，补齐差异（Button 页还缺 basic、types、full-width、group、disabled 示例，icons、loading 的内容与文档站不同）。
- [~] 按钮家族的缺口：focusableWhenDisabled（Avalonia、WPF 做了，WinForms 待定）、WinForms 的 propsContent 与 ButtonBase；ButtonGroup、CopyButton。行为对照（`tools/parity/behavior.mjs`）列出的其余缺口：加载中的读屏忙碌状态（Avalonia、WPF）、WPF / WinForms 的 Provider 默认值与 sx 测试。

## 阶段

### 0. 骨架（已完成）

- [x] 仓库结构：按平台分目录，每个平台有 src、apps、tests；集中的构建设置与包版本（DESIGN §2）。
- [x] 已验证的基础设施，都配了测试：
  - Avalonia：`EmExtension`、`PseudoClassesExtensions`、`RuneTheme` 入口。
  - WPF：`Provider`（分层合并）、`EmSize`、`ContainerQuery`、`BrushTransition`、`Motion`、`HotReload`。
  - WinForms：`Signal` / `Computed` / `Effect`、`Easing`、`Animator`、`Motion`、`HotReload`。
- [x] 三个示例应用：Avalonia 的 Debug 构建带 HotAvalonia。
- [x] 前置探测（2026-11-06）：档位类型、组合档位、部件的转发与层层合并、三个平台的主题切换、Avalonia 桌面热重载、WinForms 无窗口元素的焦点与无障碍。

### 1. 工具与生成脚本（tools/）

数据源都是 vue-tsx-pro。CI 里固定一个提交，生成脚本跑 `--check`，核对生成文件与源码一致。脚本怎么解析 TS 源码参考 RuneForBlazor 的 `tools/theme-gen`、`lang-gen`、`icon-gen`（读的是同一个数据源）。

- [x] `ts-inspect`（见"信息与验证工具"）：`component`、`token`、`deps`、`lang` 四个子命令（2026-11-06）。解析在 `tools/lib/`（找到 vue-tsx-pro、用它自带的 TypeScript 7 解析语法树、导入关系与分层），下面几个生成脚本都用它，不各写一份。

- [x] `token-gen`（`tools/token-gen`，2026-11-06）：从 `packages/ui/src/config/theme` 读 tokens 和 16 套主题，三个平台各输出一份，约定见 DESIGN §6"token 的生成"。
  - Avalonia：`Tokens.g.axaml`（并入 RuneTheme）与 `RuneThemeVariants.g.cs`；WPF：`Tokens.g.xaml`、`Themes/<主题>.g.xaml`、`RuneTheme`（换主题）、`CubicBezierEase`；WinForms：`Tokens.g.cs`、`Themes.g.cs`（`BuiltinThemes`）、`Easing.g.cs`。各有测试。
  - 留到后面的：CI 里跑 `--check`（阶段 4 建 CI 时）；色调调色板与按时长档位的 Transitions 集合，做 Button 时再生成（角色对照表来自 Button、Badge 的配方）。
- [x] `lang-gen`（`tools/lang-gen`，2026-11-06）：从 `config/lang` 生成三个库各自的 `Lang`（内置 ZhCn、EnUs），插值文案按语法树翻译成 C# 的 lambda，每个键带中文说明；约定见 DESIGN §6"语言包的生成"。各有测试。
- [x] `icons-gen`（`tools/icons-gen`，2026-11-06）：Lucide 全集（2024 个）直接放进每个库的 `Lucide` 类，路径归一成 M、L、C、Z；Avalonia、WPF 解析成 Geometry（WPF 冻结），WinForms 建 GraphicsPath；许可随包分发。约定见 DESIGN §6"图标"。各有测试。
- [x] `parity`（`tools/parity`，2026-11-06，名字对照）：vue-tsx-pro 的组件按依赖分层列出，三个库各做了没有；已做的组件比对 props 与部件属性（TS 名字 → C# 名字，改名与不做的组件登记在 `registry.mjs`）；语言包的键。C# 一侧读源码，不用先构建。
  - 顺手查出两处撞名：WPF 的容器查询 `Container` 与 em 尺寸 `Em` 和 TS 的组件同名，已改成 `ContainerQuery`、`EmSize`。
  - 取值对照（部件在各档位下的 token）等组件做出来、有 `inspect props` 之后再接。
  - 同时输出组件的依赖表与层号（阶段 3 的顺序以它为准）：vue-tsx-pro 改了组件之间的依赖时重算，不手抄。

### 2. 基础设施层

**先做基础设施，再做组件；组件也是先做基础的、再做上层的**（阶段 3）。这一层对应 vue-tsx-pro 的 `utils/` 与 `config/`，按 TS 里被组件用到的次数排了先后（括号里是次数，2026-11-06，vue-tsx-pro `417081a5`）。每一项三个平台各做一份，平台写法见"平台化的约定"。

合并与配置：
- [~] 部件的转发与层层合并（`utils/props.ts`，178）：DESIGN §7 的三种写法落地（Spinner、Badge 用上了）。
  - Avalonia：部件名常量（`ButtonParts.Icon = "PART_Icon"`），主题里给每个内部节点起名；不需要新的运行时。
  - WPF：[x] Provider 合成递归进部件样式的 `Style.Resources`（嵌套组件的部件叠在同键结果上）；[x] 部件键用 `PartKey`（带部件类型），类型键样式叠到同类型的部件下面（决定 19）；[x] Provider 的合成做成附加属性（`Provider.Styles` 本身就是附加属性，合成在 `ProviderLayer`），任何元素（包括组件自己）都能当一层，单个实例不用再包 Provider（2026-11-07）。
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
  - [ ] `ElementHost` 正式化：布局（Flex 的子集）、焦点、Tab、键盘、命中测试、无障碍（HANDOFF §4.1 的做法）、DPI 变化。元素的基类 `Element`、`IElementHost`、`ElementContext` 已有（`Elements/`），多元素的宿主做 Button 家族时写。
  - [x] 单元素宿主 `ElementControl<T>`：一个元素包成一个控件，能放进设计器（属性、序列化、默认值），绘制 effect 追踪主题（DESIGN §14 Icon）。
  - [ ] 配方结果按（档位组合, 作用域）共享 `Computed`；Provider 作用域控件（主题、默认值、语言信号）。应用级的主题信号 `RuneTheme.Current` 已有。

验证用的工具，第一批组件之前做好：
- [x] Gallery 的示例登记表（"组件 / 示例名"，示例名对齐 vue-tsx-pro 文档站的小节 id）：各平台的 `apps/Rune.*.Examples`，Gallery 与 inspect 共用；Avalonia、WPF 的示例写 XAML，WinForms 写代码。
- [x] 三个平台的 `inspect`：`list`、`snapshot`、`tree`、`props`、`uia`（`*/tools/Rune.*.Inspect`，命令行与表格输出共用 `tools/inspect/InspectShared.cs`）。WinForms 不支持 `--scale`（按显示器的 DPI 渲染）。

用来验证这一层的第一批组件（都在阶段 3 的第 1、2 层，做完就是正式组件）：Icon → Text → Spinner → Badge → Button 家族 → Field。每个都把档位、调色板、部件转发、Provider 的默认值与样式、size 与 em、主题切换、减少动态效果、热重载、无障碍走一遍。各组件的进度以 `node tools/parity/parity.mjs` 为准。
- [x] Icon：三个平台都做了（DESIGN §14）；描线动画 `draw` 暂缓，parity 里列为缺失。
- [x] 档位类型：`IVariant` / `VariantConverter` 与 `TextSize`、`TextTone`（原名 `Tone`，2026-11-06 随上游改名），三个库都做了。
- [x] Text 家族（Text、Paragraph、Strong、Em、Small）：三个平台都做了（DESIGN §14）；WPF 从 TextBlock 派生（决定 19）。示例做了 scale、tones、numeric、overflow，inline、links、headings 等 Kbd、Link、Heading。
- [x] Spinner（第一个带部件的组件，`propsIcon`）：三个平台都做了，部件转发按 DESIGN §7 的三种写法落地（DESIGN §14）；parity 认部件名常量与 `PropsXxx`。
- [x] Badge（第一个自绘、带配方与组合档位的组件）：三个平台都做了，部件按决定 20（代码里组合真的子组件）；示例做了 matrix、shapes，in-context、statuses、tag 等 Table、StatusBadge、Tag。
- [~] Button 家族：Button、IconButton 三个平台都做了，ButtonBase 做了 Avalonia、WPF（DESIGN §14，分层照 TS 的 ButtonBase 与 button-core），效果照 TS（波纹、颜色过渡、焦点环、加载指示防闪烁）；缺 focusableWhenDisabled、WinForms 的 propsContent。CloseButton、ClearButton、LinkButton、IconLinkButton 三个平台都做了（读屏名称取语言包，语言包的作用域见 DESIGN §6；链接的 Href 是绝对地址，用系统打开）。CopyButton 三个平台都做了（DESIGN §14）；ButtonGroup 接着做。

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

**第 5 层 页面组合（TS 的 patterns）**：ActionBar、AuthLayout、DeadlineBadge、KanbanBoard、NotificationCenter、SlideCaptcha、StepsForm、ThemeSwitcher、AppShell、FormDialog、CommentThread、DataImport、FilterBar、QueryTable。扩展见决定 23（M5）。

TS 里专为浏览器的登记为不适用：VisuallyHidden（桌面上用 `AutomationProperties.Name` / `HelpText`）、SkipLink、ClientOnly。Can（按权限显示内容）是逻辑组件，照做；Ellipsis、Watermark 照做，写法按平台能力。

同一层里按 COMPONENTS.md §10.6 的分类先后做；文本输入（TextField）是平台差异最大的地方，第 3 层先做它。各类组件的平台要点：

| 类别 | 平台要点 |
|---|---|
| 文字 | Avalonia / WPF 的 Text 系列派生自 `TextBlock`（一个元素一个组件，各自的默认字号与字重） |
| 轻量标记 | Icon、Badge、Tag、StatusDot、Skeleton 自绘（DESIGN §9） |
| 布局 | Flex、Grid 是自定义面板（gap、align、justify、wrap、`minItemWidth` 自动换列）；Container 的断点用容器查询（Avalonia `ContainerQuery`、WPF `ContainerQuery`）；ScrollArea 是平台 ScrollViewer 换主题（浮层滚动条，Avalonia `AllowAutoHide`） |
| 展开收起 | Collapse 测量内容高度后动画 Height，收起后不参与布局、不进 Tab 顺序 |
| 选择类 | Tabs、NavList、Listbox 建在平台的选择类基类上；勾选、单选、开关建在 `ToggleButton` 上（三态、UIA 的 Toggle 平台自带） |
| 浮层 | Tooltip、ContextMenu 先看平台自带的能否换主题做到；对话框走窗口内遮罩；关闭原因、焦点还回照 TS；Toast 走窗口里的宿主 |
| 输入 | 文本类包平台 TextBox；精确数字用 `decimal` |
| 表单 | 校验接 `INotifyDataErrorInfo`；必填、长度从 DataAnnotations 推出 |
| 表格、树 | 建在移植的 Virtualizer 上，单元格自绘 |
| 日期 | `DateOnly` / `TimeOnly` / `DateTimeOffset`，周起始、月份名取 `CultureInfo` |

RuneForBlazor 已经走过一遍这些组件，同一个组件先看它踩过的坑。

每个组件的完成标准：
1. 外观与行为对齐（决定 25）：配方逐条移植，ts-styles 的计算样式与截图对得上；上游的测试用例逐条有对应或登记了不适用；parity 报告没有未解释的差异（亮、暗主题）；
2. API：参数、默认值、部件与 vue-tsx-pro 对应，形状符合"平台化的约定"；每个内部节点都有部件名，嵌套组件的部件能从外层转发、层层合并（DESIGN §7）；公开成员有中文文档注释；
3. 行为测试：受控与非受控用法、主要分支、无障碍属性；
4. 无障碍：键盘与读屏符合 WAI-ARIA 的做法（以 vue-tsx-pro 的实现为准），UIA 客户端能看到名称、角色、状态；
5. 性能：大量重复的部件自绘；档位变化只切伪类或触发器；大列表虚拟化；
6. 文档：Gallery 示例照 vue-tsx-pro 文档站；DESIGN 记下新的取舍与坑；本文勾选。

### 4. 质量与发布准备（贯穿全程，最后收尾）

- [ ] 截图对照：三个库各渲染示例成图片，与 vue-tsx-pro 文档站的截图比较（亮、暗主题），差异人工确认。
- [ ] UIA 自动化测试覆盖有键盘、焦点逻辑的组件（FlaUI，只在测试里用）。
- [~] CI（`.github/workflows/ci.yml`）：三个平台各一个 Windows 任务并行构建、测试、给每个示例截亮暗两张图（上传为 artifact）；Linux 任务核对生成文件、把 parity 报告写进摘要。还差 Avalonia 的 Linux 构建测试。
- [ ] 打包：三个库各一个 NuGet 包，本地安装试用；暂不发布（决定 23）。

## 待定的决定

新的待定项写在这里：日期、事项、卡在哪、要决定什么，附上建议。

- **2026-11-07 Avalonia 的分段语义（工作包 3）**：上游是"使用方 / Provider 的普通值整体替换组件档位的取值，连条件值"。Avalonia 里带伪类的样式（主题里的 `^:solid`、`^:solid:pointerover`）属于"样式触发器"一级，任何不带伪类的使用方样式都盖不过；档位解析出的不继承属性已经放到主题这一层（`Resolved`），但颜色这类由伪类样式按档位给的值仍然盖不过。
  - 做法 A（现在的）：沿用 DESIGN §4 的平台规则"要覆盖档位样式，选择器里至少带一个类或伪类"，写进文档与示例。没有运行时开销。
  - 做法 B：Provider 的样式按上游语义合成——给 Provider 里不带伪类的样式，照主题里设了同一属性的每条伪类样式补一条同条件、同值的样式（要在运行时改写选择器，Avalonia 的选择器解析器不公开，得自己按选择器对象重建）；补出来的样式各带激活器，开销随定制的属性数涨。
  - 建议：先 A；等做 ThemeSwitcher / 主题定制（M5 的 theme-customize）时，那里要的就是"整体换掉某个属性"，到时按 B 做并量开销。

