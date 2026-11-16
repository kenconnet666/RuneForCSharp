# 设计

RuneForCSharp 做三套桌面组件库：**Rune.Avalonia**、**Rune.Wpf**、**Rune.WinForms**。对标 vue-tsx-pro 的 `packages/ui`，three（3D）部分不做。

文中的数字都来自 2026-11-05、06 两天的探测工程。探测在 Avalonia headless、WPF 离屏窗口、WinForms 离屏窗口上跑，本机 DPI 200%。

---

## 1. 原则

- **以 vue-tsx-pro 为准**：组件、部件、档位、token、文案都对照它，**外观尽量一致**；实现与 API 的形状用各平台更合适的写法（ROADMAP 的"平台化的约定"）。RuneForBlazor 是同源的 Blazor 版，还在开发中，只作参考，不共享代码。
- **三套库各自独立**：不做核心包，也不做单源样式。三个平台的样式系统差别很大，各自写成符合本平台习惯的样子。一致性靠脚本维护：生成 token、主题、语言包、图标，并检查名字是否对齐（见 ROADMAP）。
- **核心特点是分层合并**：组件 < 档位 < 组合档位 < 应用级 < 外层 Provider < 内层 Provider < 使用方。上层只写要改的部分，其余沿用下层。
- **每个子节点的 API 都能转发**：每个内部节点都是有名字的部件，嵌套组件的部件也能从最外层改到，每一层逐项合并（第 7 节）。
- **尺寸沿用 rem / em**：整体缩放和响应式只改少数几个基准值。
- **细粒度更新**：只更新该更新的节点。
- **开发期尽量热重载**。

## 2. 仓库结构

```
RuneForCSharp/
├─ Directory.Build.props / .targets   全仓库的构建设置（net10.0、可空、警告即错误；库才打包）
├─ Directory.Packages.props           集中管理包版本
├─ dotnet-tools.json                  仓库级 dotnet 本地工具（csharp-ls）
├─ RuneForCSharp.slnx
├─ .claude/                           Claude Code 的项目设置与本地插件市场（C# LSP 插件）
├─ docs/                              DESIGN.md（本文）、ROADMAP.md
├─ tools/                             读 vue-tsx-pro 的工具（node）；lsp/ 是 C# 语言服务器的启动脚本
├─ avalonia/
│  ├─ Directory.Build.props           导入根设置
│  ├─ src/Rune.Avalonia/              组件库
│  ├─ src/Rune.Avalonia.Web/          可选包：要网页内核的组件（Embed、Video），主包不带这份依赖
│  ├─ apps/Rune.Avalonia.Docs/        文档应用（Debug 带 HotAvalonia）
│  └─ tests/Rune.Avalonia.Tests/      headless 测试
├─ wpf/
│  ├─ Directory.Build.props           导入根设置，改成 net10.0-windows + UseWPF
│  ├─ src/Rune.Wpf/
│  ├─ src/Rune.Wpf.Web/
│  ├─ apps/Rune.Wpf.Docs/
│  └─ tests/Rune.Wpf.Tests/           STA 线程 + 离屏窗口
└─ winforms/
   ├─ Directory.Build.props           导入根设置，改成 net10.0-windows + UseWindowsForms + PerMonitorV2
   ├─ src/Rune.WinForms/
   ├─ src/Rune.WinForms.Web/
   ├─ apps/Rune.WinForms.Docs/
   └─ tests/Rune.WinForms.Tests/      STA 线程 + 消息循环
```

按平台分目录：三套库互不依赖，每个平台目录能放自己的构建设置（WPF、WinForms 要 Windows 目标），CI 也能按路径只跑改动的平台。工具分两处（见 ROADMAP 的"信息与验证工具"）：读 vue-tsx-pro 的生成脚本与检查工具放在根目录的 `tools/`（node）；要加载组件库的截图、检查、基准工具放在各平台目录下的 `tools/`（C#）。产物写到 `artifacts/`，不进仓库。

### 库内的目录

库内目录按 vue-tsx-pro 的 `packages/ui/src` 分类，一个组件一个文件夹，放它的代码与主题：

```
Rune.Avalonia/                      Rune.Wpf/                         Rune.WinForms/
├─ Base/Button/                     ├─ Base/Button/                   ├─ Base/Button/
│  ├─ Button.cs                     │  ├─ Button.cs                   │  └─ Button.cs（自绘）
│  └─ Button.axaml（ControlTheme）  │  └─ Button.xaml（并入 Generic） ├─ Display/ Feedback/ Form/ …
├─ Display/ Feedback/ Form/ Input/ Layout/ Navigation/ Overlay/ Patterns/ Table/ Date/
├─ Config/       Provider、默认值、主题、语言包（生成的放 Generated/）
├─ Themes/       主题入口（RuneTheme.axaml / Generic.xaml）
├─ Utils/Style/  em、伪类、容器断点等样式工具
├─ Utils/Motion/ 过渡、缓动、动画时钟
├─ Elements/     （WinForms）无窗口元素的基类与单元素宿主
└─ Reactivity/   （WinForms）信号运行时
```

- 空目录不提交：分类目录随第一个组件一起出现。
- **公开类型都放在库的根命名空间**（`Rune.Avalonia` / `Rune.Wpf` / `Rune.WinForms`），目录只用来组织文件（.editorconfig 关掉了"命名空间要对应目录"的检查）。这样使用方一个 using、一个 XAML 前缀就够：

```xml
xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/avalonia"
xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf"
```

- Avalonia 库里不要写 `Avalonia.Xxx` 这样的限定名。在 `Rune.Avalonia` 命名空间里，`Avalonia` 会先解析成 `Rune.Avalonia`。需要时用 using 指令，或者写 `global::`。

## 3. 与 vue-tsx-pro 的对应

| TS 版 | 三个库 |
|---|---|
| 组件名（`Button`、`TextField`） | 同名。与平台控件重名时靠命名空间或 XAML 前缀区分（`rune:Button`） |
| 部件（`css.recipe` 的 `parts: ['root', 'icon']`） | 模板里的命名元素 `PART_Root`、`PART_Icon`；WPF 另有部件样式键 `ButtonParts.Icon` |
| 档位（`variant`、`tone`、`size`、`radius`） | 同名属性，取值同名（`solid`、`soft`……）。类型见下面"档位类型" |
| `data-scope` / `data-part` / `data-state` | Avalonia 用伪类（`:solid`、`:checked`）；WPF 用依赖属性加触发器；WinForms 用状态字段 |
| 主题 tokens、16 套主题、语言包 | 由 `tools/` 从 vue-tsx-pro 的源码生成，三种平台各一种格式 |
| `createUi` / `Provider` 的 `defaults`、`styles` | 各平台的 Provider（第 4、5 节） |

其余概念（双向绑定、回调、内容、弹层、无障碍……）在各平台上的写法见 ROADMAP 的"平台化的约定"。

### 档位类型（2026-11-06 实测并定下）

TS 版的档位是"带注释接口的 `keyof`"：使用方可以扩展接口、再用 `RecipeExtension` 给新取值写样式。`size` 还能直接给数字。两种 C# 写法的实测：

| | enum | 字符串包装类型（`readonly record struct ButtonVariant(string Value)`，带常用取值的静态字段） |
|---|---|---|
| Avalonia 编译期 XAML | **区分大小写**：只能写 `Variant="Soft"`，`"soft"` 编译失败（AVLN3000） | `Variant="soft"` 可用：编译器调用类型的静态 `Parse(string)`，不经 TypeConverter |
| Avalonia 运行时 XAML（HotAvalonia 用它） | 同上，小写解析失败 | 可用，同样走 `Parse` |
| Avalonia 样式 | Setter、属性选择器 `[Variant=Ghost]` 可用 | Setter、属性选择器 `[Variant=ghost]`、`{x:Static}` 都可用 |
| WPF（BAML 与 XamlReader） | 不区分大小写，`Trigger Value="soft"` 可用 | 靠 TypeConverter：属性、Setter、`Trigger Value` 都能转换；触发器按值相等匹配（`x:Static` 给的值同样命中） |
| 使用方扩展新取值 | 做不到 | `Variant="gradient"` + 使用方自己的 `:gradient` 样式 / `Trigger Value="gradient"` 都生效（已实测） |
| 写错取值 | 编译期报错 | 变成一个没有样式的新取值，不报错 |
| C# 里分支 | `switch` 穷举 | 不能用 `case ButtonVariant.Soft`，按 `.Value` 的字符串常量分支 |

定下的做法：
- **配方的档位维度**（variant、tone、radius、density……，TS 里能经 `RecipeExtension` 扩展的）用字符串包装类型。XAML 里的写法与 TS 一致（`Variant="soft"`），使用方能扩展。
- **size** 用同一种包装，`Parse` 同时接受档位名和数字（TS 的 `TextSize | number`）。
- **行为类的封闭集合**（Orientation、Placement、SelectionMode）用 enum。
- 包装类型的约定：静态字段给常用取值；`Parse` 与 TypeConverter 都提供（Avalonia 走 `Parse`，WPF、WinForms 设计器走 TypeConverter）；TypeConverter 的 `GetStandardValues` 列出常用取值，WinForms 设计器里是下拉。`default` 的 `Value` 为 null，按组件默认值处理。
- 写错取值没有提示：Debug 构建里遇到没登记的取值时记一条日志（登记表放在包装类型上，使用方扩展时登记）。
- XAML 编辑器里能否补全包装类型的取值（VS 读 `GetStandardValues`，Rider 待确认），做 Button 时在 IDE 里看。

## 4. 分层合并

### Avalonia：原生级联加约束

- **库的样式全部写在 ControlTheme 里，档位做成伪类**（`^:solid`、`^:solid:danger`），Classes 留给使用方。
- Avalonia 先按优先级大类比较，顺序是动画 > 样式触发器 > 模板 > 样式。同一大类里，普通 Styles 胜过 ControlTheme（源码 `FramePriority`）。所以使用方写同样形状的选择器，就一定能盖过库，内层也一定盖过外层。
- 以下几点都已实测：
  - 带伪类或类的选择器属于"样式触发器"大类，永远胜过纯类型选择器，跟写在哪一层无关。内层只写 `rune|Button` 会输给外层的 `rune|Button:solid`。规则是：**要覆盖档位样式，选择器里至少带一个伪类**。
  - 使用方写 `rune|Button:solid` 设背景，悬停时也会压过库里 `^:pointerover` 的悬停色。这和 TS 版"普通值整体替换条件值"的语义一致。
  - 控件自己的 `Styles` 里不能用 `^`（会报 AVLN2000），要写 `rune|Button /template/ …`。
  - Provider 级的样式能进到别的组件的模板里：给所有 `rune|Icon` 设的线宽，对按钮模板里的 Icon 也生效。
- 档位换值时**先加新伪类再去旧伪类**（`PseudoClassesExtensions.Swap`）。顺序反过来，属性会先回落到基础值，多变一次。

#### 组合档位：调色板对象（2026-11-06 实测并定下）

TS 的 Button 是 6 种样式 × 6 种色调，配方里 36 条组合，每条带悬停色。Avalonia 给每个控件实例的每条带伪类的嵌套样式都建一个激活器，**嵌套样式的条数直接决定创建开销**，与有几条命中无关。3000 个按钮（36 种组合轮流），headless，中位数：

| 写法 | 每个控件面对的嵌套样式 | 创建 + 布局 | 分配 | 亮 → 暗 | 3000 个都换样式 |
|---|---|---|---|---|---|
| A. 一个 ControlTheme 照搬 36 条组合（各带悬停、按下） | 114 | 2721 ms | 308 MB | 507 ms | 565 ms |
| B. 调色板对象：6 条色调样式设 `Palette`，6 条样式档位绑定到调色板的画刷 | 24 | 1585 ms | 170 MB | 435 ms | 312 ms |
| C. 每种样式一个 ControlTheme（BasedOn 基础主题，同 Semi.Avalonia），色调是嵌套样式 | 18 | 1209 ms | 131 MB | 441 ms | 966 ms（换样式要换主题、重建模板） |
| D. 下限参照：只有 6 条样式，不分色调 | 18 | 1138 ms | 131 MB | 326 ms | 321 ms |

三种写法的常态、悬停、悬停 + 暗色取值都核对过；单个按钮悬停都在 5–7 ms。

定下用 **B**：
- 档位和色调仍然只切伪类（第 9 节"不换 ControlTheme"的规则不破）；创建比下限多约 39%，A 是 2.4 倍；换样式与下限一样快。
- 调色板（`TonePalette`：实色、实色悬停、实色上的字、浅底、浅底悬停、文字、边框……）是数据，由 token-gen 按主题生成，每套主题 6 份。中性色调用的 token 与其他色调不同（TS 里 neutral 用反色表面），这种不规则也收在调色板数据里，样式只写 6 条。TS 自己的勾选框、开关就是这样做的（`toneFillStyles` 按色调设 CSS 变量，部件的选中样式不再分六份）；RuneForBlazor 的 `Tones` 也是。
- Provider 能在一片区域里换掉某个色调的调色板（`rune|Button:danger` 设 `Palette`）；使用方要改单个按钮的颜色，照常在带伪类的选择器里设 Background。
- C 创建最便宜，但换样式时要换 ControlTheme，还会占用使用方的 `Theme` 属性。B 的开销成了问题时再退到 C。
- 数量大的轻量部件（Badge、Tag、表格里的状态）本来就自绘（第 9 节），不受这里影响。Badge 的颜色同样取调色板（与按钮同一张色表，vue-tsx-pro 的 `statusToneCompound`），样式档位在代码里取字段。

#### 档位的优先级（2026-11-06，跟进 vue-tsx-pro 0.11 的"组件段 < 定制段"）

上游的语义：组件自己的样式（基础 + 档位）整体低于每层定制与使用方的样式，后者只写普通值也盖得过。Avalonia 上档位按代码解析（不写成 N 条伪类样式，省激活器），解析出的值放在哪一级决定了谁盖得过它：

| 值 | 放在 | 谁盖得过 |
|---|---|---|
| 不继承的属性：色调 → `Palette`、圆角档位 → `CornerRadius`（Badge 的 `Palette`） | 代码存进 `Resolved` 的附加属性，组件主题里一条 setter 绑过来（`{rune:Resolved Palette}`、`{rune:Em Resolved=Radius}`），落在主题这一层 | 使用方、Provider 的任何样式，**与上游一致** |
| 继承的属性：字号档位 → `FontSize`，Text 的字重、字体、色调，Icon 的色调颜色；Badge 由调色板取出的文字色、底色、描边 | 代码按模板优先级绑定（`TemplateResources`） | 带类或伪类的样式、本地值；只按类型选的样式盖不过 |

WPF 没有这个问题：主题样式（DefaultStyleKey）的触发器整体低于任何样式的 setter，没命中时回到继承，所以字号、圆角档位与 Badge 的样式 × 色调都写成主题样式的触发器（`Trigger Property="Size" Value="lg"`），与上游一致；只有字号写成像素数时是本地值（同 TS 的行内样式）。使用方自己扩展的档位名不在触发器里，要在样式里补一条同样的触发器。

继承属性走不了主题 setter：Avalonia 的绑定没有值时给属性的**默认值**，回不到继承的值（`UntypedBindingExpressionBase.GetValueOrDefault`、`BindingEntryBase.SetValue`），档位没写时字号就成了 12 而不是外面的字号。只有清掉绑定才回到继承，所以这类值留在代码里，没写档位时解绑。代码按样式优先级绑定也不行：同一优先级里后建的 frame 赢，档位在样式之后才第一次写（数据绑定晚到）时会盖过使用方的样式，顺序不可靠。使用方要盖过这类档位，选择器带一个类（`rune|Text.title`）或伪类即可。

开销（1000 个，`inspect bench`，同一台机器上前后两版交替跑三轮）：布局、换主题的耗时差别在噪声内（机器负载让同一版本也差到 1.6 倍）；分配 Button 115.5 → 117.0 MB、Badge 25.05 → 25.08 MB，常驻每个按钮多约 1 KB。Text、Icon 没有变化。

### WPF：主题样式触发器 + Provider 合成

- **档位与状态写成 Generic.xaml 主题样式里的 `Style.Triggers`**，不写在 `ControlTemplate.Triggers` 里。官方优先级表中，主题样式的触发器排在页面和应用 Style 的 setter 之后，所以使用方任何 setter 都能盖过档位（已实测）。
- WPF 原生的嵌套隐式样式是**内层整个替换外层**：外层设的 Padding 在内层直接丢失（已实测）。`Provider` 把每一层串成 BasedOn 链：
  - 按资源键合成，结果按 (外层, 片段) 缓存；
  - 类型键不叠主题样式，部件键叠到主题里的部件样式上。
  - 部件键用 `PartKey`（ComponentResourceKey 加部件元素的类型）。某一层写了类型键（给所有 `rune:Icon`）时，这一层也合成同类型的所有部件键：外层结果 < 这一层的类型键片段 < 这一层的部件键片段（决定 19）。这样"所有实例"的样式也进得了模板里、代码组合的部件（Spinner、Badge 的图标），部件样式仍然更具体。库里的部件键在第一次合成时统一建出来（各 *Parts 类的静态构造），使用方的部件键随建随登记。
  - 片段的 `Style.Resources` 里按部件键写的嵌套样式递归叠上去：基础取 BasedOn 链里同键的嵌套样式（WPF 查资源时也沿 BasedOn 往上），没有时取这一层看到的同键结果。带嵌套样式的片段，结果依赖所在位置，不进缓存。
- 合成时机踩到三个坑，都由测试覆盖：
  1. FrameworkElement 在 XAML 里是自上而下构造的，挂到父级时 `Styles` 还没赋值；
  2. `Styles` 赋值时，片段里 Setter 的值还没做完类型转换；
  3. 子元素先于父元素 Initialized，内层合成时外层可能还没合成。
- 解决办法：自己和外层都初始化完才合成；外层合成后通知内层；内层先写 Resources、外层后写。最终每个子元素只套一次 Style。
- **后段带入前段的条件值**（vue-tsx-pro 的分段语义，2026-11-07）：WPF 里样式触发器总是胜过样式 setter，外层 Provider"悬停时红"会盖过内层的普通"绿"；上游是内层的普通值整体替换外层的取值（连条件值）。合成时（`StyleCloner.Rebase`）片段每个普通 setter 的属性，外层链里每条设了它的触发器照原条件补一条、值换成片段的，排在片段自己的触发器前面（片段自己的条件值仍然优先）。随机生成 1–4 层、各带普通值与条件值的 150 组，与"合成一个"的参考模型逐个状态比对（`ProviderTests.Random_layers_match_the_merged_as_one_model`）。主题样式整体低于任何样式，不用补。
- 组合档位直接照 TS 写成主题样式里的 `MultiTrigger`（36 条）：WPF 的触发器条数对创建开销没有明显影响（第 9 节），不需要调色板这一层；绑定在 WPF 上反而更慢（第 6 节 em 的数字）。

### 定制入口（2026-11-07，对应 vue-tsx-pro 0.11 的 `components.Xxx` 与 propsXxx.sx）

上游 0.11 把外观定制收到一个入口（`components.Xxx = { defaults, styles, variants }` 与部件的 `propsXxx.sx`），组件内部的样式表不再导出。三个库对应的入口与"哪些名字保证稳定"：

| | Avalonia | WPF | WinForms |
|---|---|---|---|
| 默认值（defaults） | 应用或 `rune:Provider` 的 Styles 里按类型写 Setter（`rune\|Button` 设 Variant、Tone、Size） | `Provider.Styles` 里按类型写 Style（合成 BasedOn 链） | 作用域控件 `ProviderControl` 的 `Defaults.Set(new ButtonDefaults { … })`（每种组件一个记录） |
| 外观（styles） | 同上；要盖过档位给的颜色时选择器带类或伪类（"待定的决定"里的做法 A）；整片换色调设 `Palette` | 同上；任何 setter 都盖过主题样式的档位，内层普通值连外层条件值一起替换 | `ProviderControl.Styles.Set(new ButtonSx { … })` |
| 部件（propsXxx） | `/template/` 选择器、代码组合部件的子选择器（第 7 节） | 部件样式键 `ButtonParts.Icon`（`PartKey`） | 部件属性记录 `PropsIcon` |
| 单个实例（sx） | 实例的 Styles、Classes | 实例的 Style、元素上的附加 `Provider.Styles` | 实例的 `Sx` 记录（`ButtonSx`、`BadgeSx`） |

稳定的名字：组件的公开属性、部件名常量与部件样式键（`ButtonParts.*`、`*Parts`）、token 资源键（`Rune.Brush.*`、`Rune.FontSize.*`……，由 token-gen 按上游生成）、`Rune.Palette.*`。
**不保证稳定**的组装用资源键：Avalonia 的 `Rune.ButtonCore`（按钮家族共用的 ControlTheme）、WPF 的 `Rune.ButtonCore` 与 `Rune.Recipe.*`（生成的配方触发器）。XAML 的资源键藏不住，所以写在这里；要改按钮家族的外观，写类型键的样式，不要 BasedOn 这些键。

### WinForms：分层解析

- 没有样式系统。外观是一个"字段可空"的记录，按"Sx > 内层作用域 > 外层作用域 > 配方"逐字段取第一个非空值（已实测）。
- 已做（2026-11-07）：作用域控件 `ProviderControl`（`Config/`）：`Theme`、`Lang`（null 沿用外层，最外层是应用级信号）、`Defaults`、`Styles`（每种组件一个记录，`IScopeRecord<T>.Merge` 逐字段合并）。组件沿 Parent 链取最近一层：元素经 `ElementContext.Scope`（宿主用 `ElementContext.Of(control)` 建环境），控件用 `ProviderControl.ThemeOf / LangOf`。档位的取值是"实例写了的 > 作用域合并的默认值 > 组件默认"；实例写了的单独存，设计器只序列化它们（以前按"不等于组件默认"判断，显式写成默认值会被作用域盖掉）。作用域里任何一项变了，就通知里面的宿主（`IScopeAware`）重新排版、重画；宿主换了父级时也重新解析。已接上的组件：按钮家族（`ButtonDefaults`、`ButtonSx`）、Badge（`BadgeDefaults`、`BadgeSx`）、Link、Indicator、Avatar、Card、Progress、Empty（各自的 `XxxDefaults`，字段与上游 `defaults.Xxx` 对应；Card 的 `Padding` 在控件上叫 `CardPadding`）、Switch、Checkbox、单选组（上游勾选类共用的 `ChoiceDefaults`）、CodeBlock（`CodeBlockDefaults`：Wrap、Copyable、LineNumbers、MaxLines、Flush，上游 0.14.0 之后的 CodeBlockOptions；MaxLines 写成 null 也算写了，不折叠）。取值顺序写在 `Variants.Or` 一处。外观覆盖记录（`XxxSx`：实例的 `Sx` 与作用域的 `Styles`，合并在 `ProviderControl.ResolveStyles(own)` 一处）这些组件都有，字段只收组件实际画的东西：按钮家族、Badge（底色、前景、描边色、圆角，按钮另有内边距）、Link（前景）、Indicator（标记的底色、数字色）、Avatar（底色、前景、圆角）、Card（底色、描边色、圆角）、Progress（轨道色、进度色、圆角）、Empty（标题色）、Switch（开 / 关的轨道色、滑块色）、Checkbox、Radio（勾选时的实底、没勾时的描边色、对勾或圆点的颜色）。上游的 sx 是任意 CSS，这里只能是记录里列出的字段；部件内部的细节仍用部件属性（`PropsXxx`）。
- 没做：解析结果按（档位组合, 作用域）共享 `Computed`（现在每次取值沿 Parent 链合并，作用域变了靠通知而不是信号）；作用域的部件属性（如 `PropsIcon` 的默认值）；上游的 `components.X.variants`；语言包的部分覆盖（上游 `lang` 是 Partial，三个平台都还是整份替换）。
- 原设想：作用域控件沿 Parent 链查找，作用域里的样式与默认值都是信号。解析结果是 `Computed`，绘制时读取它（第 10 节）。
- 配方结果**按（档位组合, 作用域）共享一个 `Computed`**，不要每个元素一个：主题 token 的订阅者从"元素数 × token 数"降到"组合数 × token 数"（3000 个元素读 3 个 token：8100 → 18，第 9 节）。

## 5. 默认值

TS 版的规则是"显式 prop > 最近一层 Provider > createUi > 组件默认"。

| 平台 | 做法 |
|---|---|
| Avalonia | Provider 的 Styles 里直接 `Setter Property="Variant"`。本地值优先级最高，离得近的 Styles 优先，都没写就用属性元数据的默认值。原生成立，不需要运行时 |
| WPF | 同上，写在 Provider.Styles 里，随合成一起叠加 |
| WinForms | 作用域控件上的默认值信号，组件解析档位时沿 Parent 链取最近的一层 |

## 6. 尺寸：rem / em / px

### token 的生成（tools/token-gen）

- 数据源：vue-tsx-pro 的 `config/theme`。tokens.ts 各组的默认值就是亮色主题；16 套内置主题各自用 `createTheme` 覆盖其中四组（colors、shadows、fonts、radii），并写明是亮还是暗（`colorScheme`）。
- **资源键**：`Rune.<组>.<token>`，组名用单数 PascalCase：`Rune.Color.Accent`（Color）与 `Rune.Brush.Accent`（SolidColorBrush）、`Rune.Space.Md`、`Rune.Radius.Md`、`Rune.FontSize.Md`、`Rune.Shadow.Md`、`Rune.Duration.Md`、`Rune.Easing.Standard`。
- **单位**：em 类（间距、圆角、控件高度、字间距）输出**系数**，由使用处乘以字号；px 类输出设备无关像素；rem 按根字号 16 换算成设备无关像素，整体缩放交给运行时。同一组里混用单位的（`radii.full` 是 9999px、`sizes.minTarget` 是 24px）以生成文件里的注释为准。
- **颜色**：CSS 的 `#RRGGBBAA` 换成 .NET 的 `#AARRGGBB`；阴影的每层也照此转换。
- **主题**：不随主题变的组放在公共字典里；随主题变的四组，每套主题一份完整的值。各平台：
  - Avalonia：`Config/Generated/Tokens.g.axaml` 并入 RuneTheme。亮、暗用自带的变体，其余 14 套各是一个自定义 `ThemeVariant`（`RuneThemeVariants`，取不到的回落到所属的亮或暗），每套一个 ThemeDictionary；局部主题用 ThemeVariantScope。
  - WPF：`RuneTheme : ResourceDictionary` 合并到 `Application.Resources`，里面是公共的 `Tokens.g.xaml` 加当前主题的 `Themes/<主题>.g.xaml`；改 `Theme` 就换掉主题字典。画刷、阴影都冻结。
  - WinForms：没有资源系统，生成普通的 C# 对象。`Tokens` 是常量；`BuiltinThemes` 是 16 个 `ThemeTokens`（Colors、Shadows、Fonts、Radii 四组），运行时的信号与作用域建在它们上面。
- **阴影**：Avalonia 的 `BoxShadows` 与 WinForms 的 `ShadowLayer` 列表保留全部层。WPF 没有多层阴影，取最显眼的一层（不透明度不低于 0.05 里模糊最大的）做 `DropShadowEffect`，spread 丢掉；要精确时再做专门的绘制。
- **缓动**：三次贝塞尔。Avalonia 用自带的 `SplineEasing`；WPF 自带的缓动没有三次贝塞尔，补了 `CubicBezierEase`（EasingMode 固定 EaseIn，曲线不被翻转）；WinForms 用已有的 `Easing`。三个平台的 Standard、Enter、Exit 都由生成的代码提供，不再手写取值。
- **字体**：CSS 字体栈去掉浏览器专用的通用名（system-ui、sans-serif……），其余按顺序作回退列表；Avalonia、WPF 的 FontFamily 直接支持逗号分隔的回退，WinForms 绘制时取第一个装了的。
- 分部类里生成的静态成员不能依赖手写部分的静态字段：各文件的静态字段谁先初始化不确定。WPF 的 `CubicBezierEase.Standard` 因此放在嵌套类里创建（踩过：依赖属性还没注册，构造时抛 ArgumentNullException）。
- 取值的解析只认 vue-tsx-pro 里出现的写法，认不出、或新加了没登记的组就报错；平台上用不了的 token（`sizes.full` 是 100%）登记在跳过表里，并写进生成文件。
- 时长只取默认值；"减少动态效果"由作用域里的 Transitions 资源负责（第 8 节）。
- **代码里的键**（Avalonia、WPF）：生成 `ThemeKeys`（`ThemeKeys.Space.Md`、`ThemeKeys.Brush.Border`……）的常量，代码里查资源、设资源引用都用它，不写字符串字面量。查值用 `this.Resource<T>(key)`（`ThemeResources`）：查不到（没合并 RuneTheme）时取 `ThemeKeys.Fallback` 里的生成值（随主题变的长度取亮色主题的；颜色、阴影、字体没有默认值），组件里不再各写一份兜底数字（以前手写的兜底有的已经与上游不一致，如圆角 md 写成 0.375）。WinForms 没有资源系统，直接用 `Tokens` 常量。

### 语言包的生成（tools/lang-gen）

- 数据源：vue-tsx-pro 的 `config/lang`。`lang.ts` 的 Lang 接口给出键的顺序、类型与说明，`zh-CN.ts`、`en-US.ts` 给出取值。三个库各生成一份完全相同的 `Config/Generated/Lang.g.cs`（只是命名空间不同）。
- **形状**：`sealed record Lang`，每条文案一个 `required init` 属性，带 lang.ts 的中文说明；内置 `Lang.ZhCn`（默认）与 `Lang.EnUs`。改几条用 `Lang.ZhCn with { Confirm = "好" }`（对应 TS 的 `defineLang`）。
- **类型**：string → `string`，number → `double`，可选或 `| null` → 可空；`Record<…, T>` → `IReadOnlyDictionary<string, T>`；数组、元组 → `IReadOnlyList<T>`；对象字面量类型 → 值元组（`DateFieldPeriodPatterns.Quarter`）；函数 → `Func<…, string>`。
- **插值文案**：vue 里的箭头函数按语法树翻译成 C# 的 lambda。模板字符串 → 插值字符串；里面有数字时包一层 `string.Create(CultureInfo.InvariantCulture, …)`，数字的写法同 JS，不受当前文化的小数点影响。`=== 1` / `== null` / 真值判断 → C# 的比较、`is null`、`!string.IsNullOrEmpty`。`toLocaleString(代码)` → `LangFormat.Number`（千分位，最多三位小数）。`.join` → `string.Join`。对象字面量按键取值 → `switch` 表达式。参数名撞上 C# 关键字时加 `@`。认不出的写法报错并指出键。
- **没有移植的键**：`errorCodes`（桌面库不内置请求客户端）、`formIssues`（zod 的校验文案；三个库走 INotifyDataErrorInfo / DataAnnotations）、`messages`（页面级组件的文案表，做到时再定）。原因写在生成文件的说明里。
- 标了 `<auto-generated />` 的文件默认关闭可空注解，生成文件里要写 `#nullable enable`。
- **运行时**：没有指定的地方按界面文化取（`Lang.ForCulture(CultureInfo.CurrentUICulture)`：zh-* 用中文，其余英文；TS 版固定默认中文，桌面应用跟随系统语言）。Avalonia、WPF 是可继承的附加属性 `Provider.Lang`（挂在 Provider 或任何元素上，内层盖过外层），组件用 `Provider.GetLang(this)` 取；WinForms 是应用级信号 `RuneLang.Current`（同 RuneTheme，局部的等 Provider）。读屏名称这类按需读取的文案不用绑定，取的时候读当前作用域即可。XAML 里取文案的标记扩展（`{rune:Lang Confirm}`）等第一个要在 XAML 里显示文案的组件再做。

### 图标（tools/icons-gen）

- **Lucide 全集直接放进每个库**：`Base/Icon/Lucide.g.cs`，2024 个图标，名字同 Lucide 的 JS 导出名（`Lucide.CircleX`、`Lucide.Trash2`）。组件与使用方都用它，不另做内置子集，也不单独成包（2026-11-06 用户定）。代价是每个程序集多出约 1 MB 的路径字符串（现在各约 1.7 MB）。
- 数据取自 vue-tsx-pro 装好的 `@lucide/icons`，版本跟着 vue-tsx-pro 走。Lucide 是 ISC 许可，部分图标派生自 Feather（MIT）；许可全文复制到 `licenses/lucide.txt`，组件库打包时随包分发（Directory.Build.targets）。
- **路径归一**：生成时把 circle、ellipse、rect、line、polyline、polygon 和路径里的圆弧、二次曲线、H/V/S/T 全部换算成只含绝对坐标的 M、L、C、Z。坐标保留 3 位小数，用紧凑写法（`M12 2C6.48 2 …Z`）。Avalonia、WPF 直接解析成 Geometry；WinForms 不用自己实现圆弧，按命令重放成 GraphicsPath。
  - 每个 `<path>` 从原点起算：开头的相对 `m` 按绝对坐标处理，不接着前一个元素的当前点（踩过：circle-x 的两条斜线跑到了画布外）。
  - `Z` 之后直接接着画的，补一个显式的 `M`（子路径起点）：GraphicsPath 要求每个子路径有起点（踩过：calendar-fold 的边界算错）。
- **描边与填充**：Lucide 图标都按描边画（圆头、圆角，线宽取 `strokeWidths`）。28 个元素另写了 `fill="currentColor"`（12 个图标的实心点），放进 `FillPath`，既描边又填充。
- **`IconData`**（手写，三个库各一份）：`Name`、`Path`、`FillPath`。Avalonia 的 `Geometry` 第一次用到时解析并缓存；WPF 的解析后冻结，能跨线程共用；WinForms 缓存解析好的点与点类型，`CreatePath()` 每次新建一条 GraphicsPath（用完要释放）。
- 每个图标是按需创建的静态属性（C# 14 的 `field ??=`），不在类型初始化时一次建出 2000 多个对象。Lucide 改过名的旧名（289 个）是标了 `[Obsolete]` 的转发属性，提示新名字；转成 C# 名字后与新名相同的旧名（arrow-down-01 与 arrow-down-0-1 都是 `ArrowDown01`）不重复生成。

| TS 版 | Avalonia | WPF | WinForms |
|---|---|---|---|
| px（边框、焦点环） | 设备无关像素 | 设备无关像素 | `DeviceDpi` 换算 |
| rem（字号档位、容器宽度） | token 资源，作用域里重算这两组，靠 DynamicResource 刷新 | 同左 | 根窗体的 Font；`AutoScaleMode.Font` 会在字体变化时自动缩放子控件（已实测） |
| em（组件内部尺寸） | `{rune:Em 1, 0.25}`：绑定到字号，FontSize 本身会继承 | `rune:EmSize.Padding` 只设系数，控件用 coerce 计算 | Font 是环境属性，按 `Font.Size × DeviceDpi / 72 × 系数` 计算 |

- **Avalonia em**：3000 个按钮的创建，绑定比写死数值慢约 27%（576 vs 452 ms），内存基本相同。另一个写法"附加属性 + 代码按 Style 优先级写值"会压过使用方的样式（已实测），不能用。
- **WPF em**：3000 个按钮，coerce 430 ms，绑定 878 ms，写死数值 273 ms。
  - 判断"这个值是不是库给的"不能只看值来源：基础值变化时，WPF 先调 coerce、后记录新来源，回调里读到的是旧来源（已实测）。
  - 现在的判据：库自己从不直接设 Padding、圆角，只设系数。所以基础值只要不是默认值，就一定来自使用方。
- **整页缩放**：Avalonia 的 `LayoutTransformControl`、WPF 的 `LayoutTransform` 相当于浏览器缩放，但 1px 边框也会被放大，只作为大屏或无障碍的选项。
- **响应式**：
  - Avalonia 用原生 `ContainerQuery`（已实测）。
  - WPF 用 `ContainerQuery.Watch` / `ContainerQuery.Size`（`Breakpoint`）：会继承的附加属性加触发器，断点同 TS 版的 40/48/64/80rem。
  - WinForms 在 Resize 时算出断点。

## 7. 部件属性与事件

### 部件的转发与层层合并

TS 版的规则（COMPONENTS.md §2，`utils/props.ts`，被 178 个组件文件用到，是用得最多的基础设施）：

- **每个内部节点都是部件**：包装层、装饰层、轨道、图标、加载指示都有名字与 `propsXxx`，使用方能改属性、样式、无障碍、事件；部件名是公开 API。
- **部件是本库组件时，部件属性就是那个组件的全部属性**（`propsSearch?: Partial<TextFieldProps>`），里面还能带它自己的部件（`propsSearch.propsIcon`）。所以任何一层的子节点都能从最外层转发到。
- **每一层只合自己这一层**：默认（配方 + Provider 的 `styles`）< 使用方 < 受管；样式逐项覆盖，事件是使用方先执行，受管属性（role、aria 状态、tabindex）以组件为准。子组件拿到合好的属性后，再合它自己的部件，这样一层层合到底。
- Provider 的 `styles` 按组件名作用到所有实例，嵌套在别的组件里的也算。

三个平台的写法（2026-11-06 实测）：

| | Avalonia | WPF | WinForms |
|---|---|---|---|
| 部件的属性（含附加属性、无障碍） | 样式穿透模板：`rune\|TextField /template/ rune\|Icon#PART_Icon`。Setter 只设一个属性，天然逐个属性合并 | 部件样式键（`ComponentResourceKey`，模板里 `Style="{DynamicResource …}"`），Provider 把每层串成 BasedOn 链 | 部件属性对象：字段可空的记录（`IconProps`），逐字段合并 |
| 嵌套组件的部件 | 选择器继续往下写：`rune\|Select /template/ rune\|TextField#PART_Search /template/ rune\|Icon#PART_Icon` | 外层部件样式的 `Style.Resources` 里给内层组件的部件键 | 嵌套记录（`SelectProps.Search` 是 `TextFieldProps`，里面有 `Icon`），递归合并 |
| 所有实例（Provider 的 `styles`） | Provider 的 Styles 里写不带路径的选择器（`rune\|Icon`） | Provider.Styles 里写部件键 | 作用域上的部件属性 |
| 单个实例（`propsXxx`） | 控件自己的 `Styles` | 元素自己设附加属性 `Provider.Styles`（任何元素都能当一层，同 Provider 的合成） | 组件的 `PropsXxx` 属性 |
| 层层合并 | 外层作用域 < 内层作用域 < 实例的 Styles，逐个属性 | 主题 < 应用 < 外层 Provider < 内层 Provider，逐个 setter | 配方 < 外层作用域 < 内层作用域 < 实例，逐字段 |
| 部件的事件 | 组件给语义事件与可取消的前置事件（`Clearing` 带 `Cancel`）；输入级的接管用隧道事件；部件的路由事件照常冒泡 | 同左（Preview 事件） | 记录里可以放委托，使用方先执行、`Cancel` 跳过组件的处理；组件另给语义事件 |
| 受管属性 | 组件在代码里设本地值或用模板绑定，样式盖不过 | 同左 | 不进部件记录 |

实测结果：
- **Avalonia**：两层 `/template/` 只命中 Outer 里那个 Inner 的图标，单独的 Inner 不受影响；外层作用域设 StrokeWidth、内层作用域对同一路径只设 Foreground，结果两个都在；实例自己的 Styles 能覆盖作用域；`AutomationProperties.Name` 这样的附加属性也能经样式转发。
- **WPF 的附加层**（2026-11-07）：`Provider.Styles` 是附加属性，Provider（Decorator）与任何元素共用 `ProviderLayer` 的合成；元素自己这一层的类型键样式也作用到它自己（WPF 查隐式样式从元素自己的 Resources 开始）。踩过：XAML 里附加属性元素在宿主 Initialized 之后才赋值，而且先赋空字典、再往里加样式，没有通知；所以宿主 Loaded 时条目数变了就重新合成（外层变了也是在 Loaded 里发现）。
- **WPF**：部件键经 Provider 逐层合并（主题的 Bold + 外层的 Red + 内层的 FontSize=20）。外层部件样式的 `Style.Resources` 里给内层部件键，查找能定位到 Outer 里的 Inner，但会**整个替换**这个部件样式（主题的 Bold、外层的 Red 都丢了）。Provider 合成时递归进部件样式的 `Style.Resources`，嵌套的部件样式叠在外层同键的结果上（已做，测试覆盖：卡片里按钮的标签 = 主题 Bold < 这一层 Light < 嵌套的 Red）。
- **WinForms**：记录的逐字段合并在 §4 已实测过一层；嵌套记录的递归合并、事件委托的"使用方先执行"由源代码生成器生成，不手写（阶段 2）。

写组件时的约定：
- 模板里**每个内部节点都起部件名**（`PART_Root`、`PART_Icon`、`PART_Track`……），名字照 TS 的部件名，作为公开常量列出（`ButtonParts.Icon`），文档列出每个组件的部件。
- 部件是本库组件时，模板里直接放那个组件（`rune:Icon`、`rune:TextField`），不要换成平台的基础元素，否则使用方转发不到它的属性与部件。WPF 上它还得从 Control 派生，外层的隐式样式才进得去（见下）。
- 组件自己给部件的默认外观写在主题里（嵌套样式、部件样式、配方），这样才排在使用方之前；组件状态决定的属性用本地值或模板绑定，作为受管属性。
- **代码组合的控件**（子元素在代码里建，没有模板：Badge、Indicator、Separator、Card、Avatar、Empty、Progress……）的部件默认外观（2026-11-07 定并统一改过）：
  - Avalonia：ControlTheme 里写不了普通子选择器，`BindingPriority.Template` 又高于样式（使用方盖不过），所以每个组件一份 `XxxParts.axaml`（`Styles`，RuneTheme 用 `StyleInclude` 引入，排在使用方的样式之前）。选择器按逻辑树写全路径（`rune|Empty > Border > rune|Flex > rune|Icon#PART_Icon`），不碰 Content、Action 里别人的同名部件；跟着组件属性走的值用 `{Binding $parent[rune:Indicator].Palette.Solid}`。
  - WPF：写在部件样式键的主题样式里；跟着组件属性走的用祖先绑定（`{Binding Thickness, RelativeSource={RelativeSource AncestorType=rune:Separator}}`），随状态变的用祖先绑定的 DataTrigger（Progress 的色调、形状）。每个部件键都要有主题样式（Badge 以前漏了）。
  - WinForms：组件给的默认 `TextProps` / `IconProps` 在下，使用方的 `PropsXxx` 在上，逐字段合并，改部件属性时重建元素（保留文字）。
  - 仍在代码里的是受管属性：可见性、按 em 算出来的尺寸与边距、内容。测试：三个平台各有 `PartDefaultsTests` / 部件属性用例，核对"默认值在、使用方盖得过"。

### 部件的外观与事件

- **部件的外观只写在主题的嵌套样式或部件样式里，不写成模板属性**。实测：
  - Avalonia 模板属性的优先级是 Template，使用方不带伪类的 `/template/` 样式盖不过它；
  - WPF 模板属性的优先级是 TemplatedParent，任何样式都盖不过。
- **用组件当部件**（按钮里的 Icon）：
  - Avalonia：外层样式能进入模板。
  - WPF：外面的隐式样式只对 **Control 派生**的元素进入模板，FrameworkElement 派生的进不去（已实测）。需要被外层统一定制的子组件就从 Control 派生，或者走部件样式键。
- **一个元素一个组件**：XAML 里组件本身就是主元素（TS 的 `...rest` 落到主元素），使用方设在组件上的 ToolTip、AutomationProperties 不用转发。
- **WPF 里代码组合、不用模板的控件从 `CompositeControl` 派生**（`Utils/Layout`，2026-11-14）：一个根元素；继承来的 Background、BorderBrush、BorderThickness、Padding 本来靠模板才起作用，基类统一画在根元素后面、留在根元素四周。组件自己把其中某些接到了带圆角的根元素上（Alert、Banner、Stat 全部，Avatar 的底色与描边，Result 的内边距）时用 `OwnChrome` 说明，基类不重复画。Progress 有多个子元素，不在此列。
- **事件"使用方先处理、可接管"**：
  - Avalonia 和 WPF 都用隧道阶段：使用方在 Preview/Tunnel 里设 `Handled = true`，组件的内部处理就跳过（已实测）。冒泡阶段的普通订阅在组件处理之后才收到，组件标记 Handled 后就收不到了。
  - WinForms 没有路由事件，由组件提供可取消的前置事件（做组件时定）。
- **组件自己的事件**（Avalonia、WPF；2026-11-14 定）：
  - 用户动作的语义事件是**路由事件**，CLR 包装仍是 `EventHandler<T>`，参数从 RoutedEventArgs 派生。容器上能统一处理的用冒泡（`CopyButton.Copied`、勾选类的 `Toggling`、`Listbox.Picked`、`NavList.Navigate`）；浮层的开合与浮层里的动作用直接路由（`OpenChanged`、`Select.OptionCreated`、`CommandPalette.ItemInvoked`），不往上传，免得嵌套浮层（菜单里的子菜单、对话框里的下拉）的同名事件串到外层的处理里。
  - **订阅了才启用功能的事件保持普通事件**：Tag 与已选标签的 `RemoveRequested`、Alert / Banner / Tab 的 `CloseRequested`、TabList 的 `AddRequested` 与 `Reordered`、ItemList 的 `Invoked`（上游给了 onRemove、onClose 才有按钮）。路由事件在容器上订阅也算处理，组件没法知道自己这一个有没有人管。
  - **MVVM 命令**：值、开合、勾选类属性默认双向绑定；执行类的部件从 ButtonBase 派生，自带 Command（菜单项、ItemList 的项、导航项）。没有按钮可挂的执行动作补命令属性，事件之后调用、事件标记 Handled 时不调：`CommandPalette.ItemCommand`（参数是那一项）、`NavList.NavigateCommand`（参数是 Href，不能执行时不切换）。订阅了才有按钮的移除、关闭配一对命令属性：`Tag.RemoveCommand`、Tab / Alert / Banner 的 `CloseCommand`，各带 `…CommandParameter`；设了命令也算订阅（有按钮），在事件之后调用（Tag、Alert、Banner 是离场结束后）。

## 8. 动效

- 时长与缓动取 TS 版的 token（durations xs–xl，easings standard / enter / exit）。
- "减少动态效果"有两个来源：系统的"动画效果"开关（`SPI_GETCLIENTAREAANIMATION`，WPF 叫 `SystemParameters.ClientAreaAnimation`），以及作用域里把时长设成 0。

各平台的过渡：

- **Avalonia**：
  - ControlTheme 里的 `Transitions` 在真实平台上能正常插值（已实测，headless 的动画时钟几乎不走，要用真实窗口测）。
  - 时长写成 DynamicResource 能编译，但解析不出来，结果等于 0（已实测）。
  - `ITransition` 在 12 里标成了使用方代码不可实现。
  - 所以 **Transitions 集合本身做成作用域资源**：主题写 `Transitions="{DynamicResource …}"`，作用域换成另一份，空集合就表示减少动态效果（已实测）。生成器按时长档位生成这些集合。
- **WPF**：
  - `BrushTransition` 附加行为（已实测）。关键帧里的画刷会被冻结成副本，颜色动画传不过去；所以用 `SetCurrentValue` 换上一支正在播颜色动画的画刷，值来源仍是 TemplateBinding。
  - 观察 Background 用绑定到自身的附加属性，不用 `DependencyPropertyDescriptor.AddValueChanged`，后者会强引用元素造成泄漏。
- **WinForms**：
  - `Animator` 用一个共享时钟驱动所有补间，没有补间时停下（已实测）。
  - 100 个按钮只动其中 1 个，其余 99 个零失效。
- **离场（presence）**：
  - Avalonia：`Animation.RunAsync` 结束后设 `IsVisible = false`；
  - WPF：`DoubleAnimation.Completed` 里折叠；
  - 都已实测。WinForms 用 Animator 加结束回调。

## 9. 性能

实测数据：

| 场景 | Avalonia | WPF | WinForms |
|---|---|---|---|
| 3000 个"模板控件"vs"自绘控件"（创建 + 布局） | 652 vs 152 ms；分配 97 vs 28 MB | 588 vs 203 ms；分配 45 vs 16 MB | — |
| 主题里 52 个嵌套样式/触发器 vs 6 个（3000 个） | 创建慢约 37%，分配多约 48% | 无明显差别 | — |
| 1000 个按钮：每个一个 HWND vs 无窗口元素（含首次绘制） | — | — | 12–13 s vs 160–180 ms；USER 对象 +1000 vs +1 |

由此定下的规则：

- **大量重复的轻量部件用自绘**，不用模板：Icon、Badge、Tag、表格单元格、列表行。模板控件只给结构复杂、需要使用方改模板的组件。
- **WinForms 组件的本体是无窗口元素**，由少数宿主控件统一布局、绘制、失效。只有需要原生输入法或独立焦点的（文本输入）才用真的子控件。
  - WinForms 的使用方主要在设计器里拖控件，所以**每个组件另有一个单元素宿主**（`ElementControl<T>`），把一个元素包成一个控件，能放进设计器、在属性窗口里改。列表、表格、标签组这类数量大的地方直接用元素（2026-11-06 定下）。
  - 已实测这条路可行：Tab 在元素间移动、走到头交还窗体，Enter、Space、点击都能触发。
  - 无障碍上，UI Automation 能看到每个元素：名称、Button 类型、位置、焦点状态，支持 Invoke，按屏幕坐标也能命中。
  - 具体写法见 HANDOFF.md §4.1。
- **整套主题切换**（3000 个控件，亮切暗；局部切换只换其中 300 个）：

  | 平台 | 做法 | 应用级 | 局部 |
  |---|---|---|---|
  | Avalonia（headless，不含渲染） | 每个控件 4 个 DynamicResource；局部用 ThemeVariantScope | 273 ms，属性变化 9000 次 | 11 ms，900 次 |
  | WPF（离屏窗口） | 换 Application / 局部的合并字典 | 363 ms，9000 次 | 28 ms，900 次 |
  | WinForms（无窗口元素） | token 是信号，每个元素一个绘制 effect | 通知加失效 5–9 ms，失效 2700 次；重画全部约 500 ms | 通知 0.6 ms；重画约 50 ms |

  - token 全用 DynamicResource / 信号都可以接受；局部主题的开销与作用域大小成正比。
  - WinForms 的通知只占零头，时间花在重画上（每个元素约 0.17 ms，大部分是 TextRenderer 画字），实际只重画看得见的区域。
  - WinForms 原来读几个 token 就失效几次（3 个 token → 8100 次失效，18–28 ms）。现在 `Effect` 一次追踪只回调一次（第 10 节），与"读一个合成的 Computed"、"整套主题一个信号"一样都是 2700 次。
  - 测 WinForms 绘制耗时要画到 `BufferedGraphics`（双缓冲用的 DIB 段）上：画到 GDI+ 的 Bitmap 上时，TextRenderer 每次取 HDC 都要复制整张位图，3000 个元素要 31 秒，是测量方法的假象。
- 档位变化只切伪类或触发器，**不换 Style / ControlTheme / Template**：换 Style 会重新应用全部 setter，换模板会重建整棵子树。
- Avalonia 组合档位：嵌套样式的条数直接决定创建开销，照搬 TS 的 36 条组合会让创建慢到 2.4 倍，用调色板对象（第 4 节）。
- 自定义属性的元数据写准 AffectsRender / AffectsMeasure；集合用逐项通知，大列表用虚拟化。

### 组件基准（`inspect bench`，2026-11-06）

每个组件建 1000 个，量创建、首次布局（套样式与模板、测量排列）、渲染一帧、亮切暗，5 轮取中位数（第一轮预热不计）。同一时间机器上还有别的会话在跑重任务，**耗时的波动能到 ±50%**，分配与常驻内存是确定的，以它们为准；耗时只看量级与成倍的变化。

| 平台 · 组件 | 首次布局 ms | 渲染 ms | 换主题 ms | 分配 MB | 常驻 KB/个 |
|---|---|---|---|---|---|
| Avalonia · 平台 Button（对照） | 411 | 175 | 221 | 48.0 | 34.3 |
| Avalonia · Button（优化前 → 后） | 1542 → 991 | 210 → 186 | 853 → 493 | 133.5 → 115.6 | 96.0 → 83.2 |
| Avalonia · IconButton | 874 → 698 | 210 → 191 | 413 → 360 | 100.1 → 92.1 | 75.0 → 68.4 |
| Avalonia · Icon | 22 → 11 | 82 → 48 | 57 → 38 | 11.1 → 7.9 | 8.4 → 5.3 |
| WPF · 平台 Button（对照） | 1037（创建 60） | 161 | 14 | 20.4 | 5.1 |
| WPF · Button（创建 + 布局） | 约 1900 → 约 2000（噪声内） | 104 → 97 | 115 → 85 | 38.5 → 31.2 | 13.0 → 13.1 |
| WinForms · Button（元素） | 531 → 128 | 759 → 265 | 811 → 233 | 2.5 → 2.5 | 1.1 → 1.2 |
| WinForms · Text（元素） | 569 → 306 | 539 → 327 | 637 → 335 | 0.45 → 0.49 | 0.3 → 0.3 |

找到并改掉的（规则同样适用于以后的组件）：

- **Avalonia：每个实例的带伪类嵌套样式都要建激活器**，取值到颜色的映射不写成 N 条伪类样式，在代码里按 `BindingPriority.Template` 绑定主题资源：盖过继承与普通样式，使用方带伪类的样式（StyleTrigger）仍然盖得过，与原来主题里伪类样式的先后一致。Icon 的 7 条色调样式、按钮家族的 6 条色调 → 调色板样式都改成这样（Icon 每个省 3 KB）。样式档位（6 条加 5 条悬停）还是样式，它们要按调色板字段换值、带悬停。
- **Avalonia：模板里取组件根的属性用 TemplateBinding**，不用 `$parent[...]`（每个实例沿树往上找）；`{rune:Em}` 只有系数来自资源时才用多重绑定，取模板父级字号时是 TemplateBinding，取自己的字号时是单个绑定。
  自己的字号绑的是可继承的附加属性 `TextElement.FontSize`（TemplatedControl、TextBlock 的 FontSize 就是它）：原来绑 CLR 的 FontSize 属性，Border、Panel、Flex 没有它，绑定失败、值设不上
  （2026-11-16 修：FormSection 标题栏的下边距、Blockquote 的出处、NativeSelect 的圆角与列表边距、TagsInput 的最小宽度、TextField 的内层边距、AppShell 抽屉的标题栏都因此一直是 0）。
- **行高与基线（Avalonia）**：ContentPresenter 给字符串生成的 TextBlock、模板里直接写的 TextBlock 不经过 Text，用的是字体自己的行距（约 1.33），上游处处继承 1.5；
  放文字的部件要照 FieldLabel 的做法给 ContentPresenter 设 LineHeight = Leading × 字号（Card 的页头、页脚，Steps 的标题与说明已改）。
  Flex 按基线对齐时找第一基线：TextBlock 的第一行、Flex 自己量出的第一基线、只包着一个子元素的包装（AnimatedNumber、Border、ContentPresenter）往里找；都没有才按底边。
- **Avalonia：Transitions 可以共用**（过渡不存每个控件的状态，Fluent 也是一份给所有按钮），按（时长、缓动）缓存一份，不每个按钮各建三个。
- **无限动画只在可见时跑**：Avalonia 样式里的动画、WPF 的动画时钟都不管元素看不看得见，每帧照走。Avalonia 写成 `^[IsVisible=True]` 的样式；WPF 在 IsVisibleChanged 里开停。隐藏的控件不测量、不套模板，所以按钮里收起的加载指示本来就不会建出图标。
- **WPF：设 Content 时元素就初始化**（`AddLogicalChild` 里 TryFireInitialized），主题样式在这时套上，所以"创建"里包含了主题样式的开销，和"布局"要加在一起看。
- **WPF：从 BAML 加载的字典资源是延迟创建的**，还没建的资源每被一个元素的 DynamicResource 引用一次，就新建一个延迟引用、挂进按键的弱引用列表（带锁）。`RuneTheme` 加载与换主题时把 token 一次建好（几百个，几毫秒），按钮的分配少了约 20%。剩下的开销在 WPF 自己的资源查找里（每次查字典都加锁，DynamicResource 越多越慢）：主题样式里能不用 DynamicResource 的就不用。
- **WinForms：时间几乎都在 GDI 量字、画字**（TextRenderer 每次零点几毫秒）。行高按字体缓存（原来每次测量量两遍）；Text 记住上一次测量的输入与结果，按钮绘制时不再重量标签。
- **Avalonia 的 Spinner**：1000 个同时开始转的那一帧要约 2 秒（每个样式动画的启动开销）。实际页面上的加载指示是个位数，先不动；要优化就改成 Composition 动画（在渲染线程转，不占 UI 线程）。
- **测量方法**：计时前要等终结器跑完（回收、等终结器做三遍）。WPF 渲染留下的 MIL 句柄在终结器里释放，不等完会和计时并行、抢资源查找的锁，把按钮的创建拖慢几十倍。采样分析用 `dotnet-trace`（HANDOFF 的命令表），`tools/trace/callers.mjs` 按热点帧往上汇总调用链。

## 10. 细粒度更新

- **Avalonia / WPF** 本来就是属性级更新。TS 版的 `css.bind`（只改变量）对应绑定，`css.select`（就地换类）对应伪类或触发器。实测：
  - 改一个按钮的档位，Background 只变 1 次，不重建模板、不重新布局；
  - 局部换一支画刷，只有用到它的元素更新。
- **WinForms** 用信号运行时（`Reactivity/`）：
  - `Signal` 写入不同的值时，只通知依赖它的追踪者；
  - `Computed` 惰性求值并缓存；
  - `Effect` 在 OnPaint 里追踪依赖，变化时只让对应控件或区域失效；回调之后到下次追踪之前不再重复回调（同 `Computed` 的脏标记），一次写几个 token 只失效一次。
  - 实测：改 1 个按钮只失效 1 次；改主题色只有读到它的按钮重绘；1 万行的列表改一个可见行，只失效那一行的矩形；从没画过的行没有依赖，零失效。
  - 长寿的信号（主题 token）会引用追踪者，**控件释放时要释放它的 Effect / Computed**。

## 11. 热重载

| 改动 | 做法 | 状态 |
|---|---|---|
| C#（三个库都适用） | dotnet watch、Rider、VS 的 .NET 热重载；库里的 `MetadataUpdateHandler` 负责失效自己的缓存。WPF 重新合成所有 Provider；WinForms 把 `HotReload.CodeVersion` 加一，读过它的配方结果标脏并重绘 | WinForms 实测：改配方颜色约 1.6 秒生效，只有读到配方的控件重绘 |
| Avalonia axaml | HotAvalonia（MIT，Debug 构建，PrivateAssets，示例应用和库都装）；官方的热重载要授权 | 桌面窗口里实测（屏幕外的 Win32 窗口）：改应用的 axaml 或库的 ControlTheme，约 1 秒生效。写在 axaml 视图里的控件随视图重新填充，拿到新主题（实例是新的，视图里的状态会丢）；之后新建的控件也是新主题；**用代码创建、已经存在的控件不刷新**，要一个开发期钩子（重新加载后对这些控件重新套主题，ROADMAP 阶段 2）。headless 下看不到效果是 headless 的限制 |
| WPF XAML | VS 一直支持，Rider 2026.2 起支持（beta） | Rider 列出的限制里有"运行时创建的资源"，Provider 合成的样式正属于这类，改 Provider.Styles 能否生效待在 IDE 里验证 |

## 12. 测试

- **Avalonia**：`Avalonia.Headless` 的 `HeadlessUnitTestSession`，所有测试共用一个应用（`HeadlessUi`）。没用 `Avalonia.Headless.XUnit`，因为它依赖 xunit v3 的 3.x。动画、过渡要在真实平台上测。
- **WPF**：一个常驻的 STA 线程跑 Dispatcher 与 Application（`WpfUi`），窗口放在屏幕外，渲染和动画照常进行。嵌套 Provider 的测试用 XamlReader 解析 XAML，走真实的构造顺序；代码搭的树碰不到第 4 节的时序问题。
- **WinForms**：一个常驻的 STA 线程跑消息循环（`WinFormsUi`）。离屏窗口收不到 WM_PAINT，用 DrawToBitmap 代替绘制，测"失效了哪些控件"。
- 系统关闭了动画时，断言中间帧的测试跳过（`Assert.SkipUnless(Motion.Enabled, …)`）。
- 测试框架统一用 xunit.v3 + Microsoft.Testing.Platform（`global.json` 里设了 test runner）。
- **示例与 inspect**：示例按"组件/示例名"登记在各平台的 `apps/Rune.*.Examples`，文档应用与 inspect 都用这张表。inspect 把示例放进宿主（背景、文字颜色、字号取主题），输出截图、元素树、属性与来源、读屏树：
  - Avalonia：headless 平台加 Skia、HarfBuzz（`UseHeadlessDrawing = false`），`CaptureRenderedFrame` 出图，`SetRenderScaling` 做缩放；值的来源取 `GetDiagnostic` 的优先级。样式与文档应用相同（FluentTheme 给 UserControl 等平台控件模板）。
  - WPF：屏幕外的窗口，`RenderTargetBitmap` 出图；来源取 `DependencyPropertyHelper.GetValueSource`（区分主题样式、样式触发器、资源引用）。
  - WinForms：屏幕外的窗体，`DrawToBitmap` 出图；来源暂时只分本地值与默认值（`ShouldSerializeValue`），分层解析做出来后报命中的那一层。读屏树走 MSAA（与窗口层级一致）。
  - 画刷、颜色报出它对应的 token 名（Avalonia、WPF 按画刷对象查主题资源，WinForms 按颜色值查当前主题）。
  - 用它查出过的问题：WinForms 里没名字的窗口会被系统借用前面 Label 的文字当名字，装饰性图标被读成上一行的内容（IconControl 的无障碍对象只取 AccessibleName）。

## 13. 待验证

- WPF：在 Rider / VS 里改 Provider.Styles 能否 XAML 热重载（要在 IDE 里手动试）。
- Avalonia：HotAvalonia 重新加载后给代码创建的控件重新套主题的钩子怎么接（做 Button 时写，桌面窗口里验证）。
- XAML 编辑器能否补全档位包装类型的取值（VS 读 TypeConverter 的 `GetStandardValues`，Rider 待看）。
- 调色板对象在真实 Button 上的开销（探测用的是简化模板），做 Button 时复测。

## 14. 组件笔记

每个组件在各平台上的写法、与 TS 版的出入和踩过的坑，按组件记在这里（做完一个补一个）。

### 档位类型的实现

- 三个库各有 `IVariant<T>`（静态抽象成员 `Parse`、`Known`）与通用的 `VariantConverter<T>`：字符串互转，常用取值作为下拉选项。每个档位类型（`TextSize`、`TextTone`……）是 `readonly record struct`，静态字段给常用取值。
- Debug 构建里 `Parse` 遇到不在常用取值里的，写一条调试输出：写错的取值不会报错，只是没有样式。
- `TextSize` 同时接受档位名与像素数（`Size="18"`、C# 里 `Size = 24`）：`Pixels` 是像素数，`ResourceKey` 是档位对应的字号资源键（`Rune.FontSize.Lg`）。
- WinForms 没有资源：`TextSize.Pixels` 直接取 `Tokens.FontSize.*`；`Tone.ColorIn(context)` 按色调取当前主题的颜色，不写或 `inherit` 时返回宿主的 ForeColor，这时不读主题（换主题不重画）。
- Avalonia 换伪类用 `PseudoClassesExtensions.Swap(旧值, 新值)`，传的是**不带冒号**的取值（`Tone.Value`），冒号由它加（踩过：多加一层变成 `::danger`，样式全不生效）。

### Icon

- TS 的 prop `icon` 在 C# 里叫 `Data`：C# 的成员不能与所在的类同名（parity 的 `registry.mjs` 登记了）。
- **Avalonia**：`Control` 重写 `Render`（不用模板）。按 `ActualSize / 24` 缩放后画 `IconData.Geometry`（圆头圆角的 Pen），有 `FillGeometry` 的先填充。
  - 大小：`Size` 是像素数就用它；是档位名就绑定 `Rune.FontSize.*` 资源（主题与作用域换了跟着变）；都没有时用继承的 `FontSize`，即 1em。
  - 色调：ControlTheme 里 `^:danger` 这类伪类设 `Foreground`（取 TS 的 toneStyles 对应的 token）；不写或 `inherit` 时跟随外面的文字颜色。线宽的默认值在主题里取 `Rune.StrokeWidth.Md`。
  - 从右到左：Avalonia 默认让整棵子树镜像，Icon 重写 `BypassFlowDirectionPolicies` 返回 `!FlipInRtl`，普通图标抵消镜像，有方向的图标跟着翻（同 TS 的 flipInRtl）。
  - 无障碍：没有 `AutomationProperties.Name` 时是装饰，不进控件树与内容树（同 TS 的 aria-hidden）；有名称时是 Image。
  - TS 的 `verticalAlign: -0.125em`（行内图标与文字对齐）在 Avalonia 的面板布局里没有对应，放进行内元素时再处理。
- **WPF**：`Control` 重写 `OnRender`（从 Control 派生，外层的隐式样式才进得了别的组件的模板，见第 7 节）。主题样式在 `Base/Icon/Icon.xaml`，合并进 Generic.xaml：线宽是资源引用 `Rune.StrokeWidth.Md`，色调是 `Trigger Property="Tone" Value="danger"`（包装类型按值相等匹配）。
  - Control 的 Foreground、FlowDirection、FontSize 元数据不带 AffectsRender / AffectsMeasure，Icon 覆盖元数据补上。图标不可交互：Focusable、IsTabStop 都是 false。
  - 从右到左：WPF 在流向改变的祖先上镜像整棵子树，Icon 在 OnRender 里对不翻转的图标再镜像一次抵消（`CancelsMirroring`）。
  - 档位名的大小用 `SetResourceReference` 引用 `Rune.FontSize.*`。
- **WinForms**：本体是无窗口元素 `Icon : Element`，放进窗体、设计器用单元素宿主 `IconControl : ElementControl<Icon>`（决定 12）。
  - 元素层（`Elements/`）：`Element` 有 `Measure`、`Paint`、`Bounds`，属性变了调 `Invalidate` / `InvalidateMeasure` 通知宿主；`ElementContext` 带宿主的 DPI、字体、ForeColor、方向，`Theme` 用到时才读 `RuneTheme.Current`。
  - `ElementControl<T>` 在 OnPaint 里用 `Effect` 追踪，读了主题的元素换主题时才失效（实测：带色调的图标失效 1 次，跟随 ForeColor 的 0 次）；默认 AutoSize（同 Label，属性窗口里能关）；字体、Padding、DPI 变了重新量。背景色不设，跟随父控件（环境属性），父控件有背景图时可以设 Transparent。
  - 大小：档位、像素数是设备无关像素，按 `DeviceDpi / 96` 换算；不写时 1em = 字体磅值 × `DeviceDpi / 72`。
  - 绘制：GraphicsPath 按 `边长 / 24` 缩放，圆头圆角的 Pen；`PixelOffsetMode.HighQuality` 让 24 格的整数坐标落在像素边上，2 宽的线不发虚。`FlipInRtl` 且宿主 `RightToLeft` 时水平翻转（WinForms 不会自动镜像自绘内容，不需要抵消）。
  - 控件自己有 `Size`，图标的大小档位在 `IconControl` 上叫 `IconSize`（元素上仍叫 `Size`，parity 读的是元素）。
  - 设计器：属性标 `[Category]`、`[Description]`，档位用 `ShouldSerializeXxx` / `ResetXxx`；`IconData` 带 `IconDataConverter`，属性窗口按名字列出 Lucide 的现名（旧名不列），生成 `Lucide.Search`，自定义图标生成 `new IconData(…)`。
  - 无障碍：`AccessibleRole` 默认 Graphic，名字只取 AccessibleName（不让系统借用前面 Label 的文字），状态去掉 Focusable；不能获得焦点（去掉 Selectable）。WinForms 的公开 API 改不了 UIA 的 IsControlElement，装饰性图标没法像另外两个平台那样从控件树里隐藏，只是没有名字。
  - 自定义图标的路径只认绝对坐标的 M、L、C、Z（GraphicsPath 没有解析 SVG 路径的现成方法）；Avalonia、WPF 用 `Geometry.Parse`，什么写法都认。三个平台的示例都用这种写法，要支持任意 SVG 路径时在 WinForms 里补一个归一化（同 icons-gen 的 svg.mjs）。
- 描线动画 `Draw`（2026-11-14，Result 要用）：`bool?`，不写（null）的图标不建动画（WPF 只在写了时才订阅 Loaded）；挂上（WinForms 是第一次绘制）时为 true 就播，
  之后每次由 false 变为 true 再播，时长 durations.xl、缓动 enter，系统关闭动画时直接画好。TS 给每个 SVG 元素写 pathLength = 1 再按比例设虚线；
  这里的孪生文件 `IconStrokes`（Base/Icon）把路径拆成一笔一笔（每个 M 开始的子路径）、曲线展开成折线（每段 16 份），按每一笔自己的长度截出画到的部分，
  进度 0 时什么也不画（圆头的线长度为 0 时还会画出一个点）。描线中按折线画，画完换回原来的几何。

### Text 家族

- TS 是一个工厂生成五个组件（span、p、strong、em、small），只差默认值：Paragraph 的 wrap 是 pretty、Strong 的字重 semibold、Small 的字号 xs、Em 斜体。C# 里 Text 是基类，其余四个是子类。
- 新的档位与 enum：`TextWeight`（字重，避开平台的 FontWeight 重名）是包装类型；`TextAlign`（start、center、end）、`TextWrap`（wrap、nowrap、balance、pretty）是封闭集合，用 enum。balance、pretty 是浏览器的排版优化，桌面上同普通换行。
- 不写的属性继承外面：只有写了的档位才去改字号、字重、颜色、字体。
- **Avalonia**：从 TextBlock 派生，行内内容（Run、Bold、InlineUIContainer）、换行、截断、读屏都现成。
  - 档位**不写成 ControlTheme 里的嵌套样式**：文字是数量最多的组件，第 9 节量过嵌套样式多了创建明显变慢（52 条比 6 条慢约 37%）。写了的 Size、Weight、Tone、Mono 在代码里绑定主题资源（本地值，`Tone.BrushKey`、`TextWeight.ResourceKey`），清掉时解绑、回到继承。改某个色调的颜色就改主题资源。
  - 各组件的默认值写在 ControlTheme 的普通 Setter 里（`Strong` 的 `Weight=semibold`），使用方的样式能改；TextBlock 默认不换行，Text 的主题设成换行（同浏览器）。
  - Align、Wrap、Truncate、LineClamp 写了才设到 TextAlignment、TextWrapping、TextTrimming、MaxLines 上（本地值）；truncate 优先于 lineClamp，二者都没有时看 wrap。Numeric 是 OpenType 的 `tnum`（`FontFeatures`）。
  - 踩过：Size 从像素数改回档位或不写时，要先清掉像素数设的本地 FontSize。
  - 中文字体在 14px 下 semibold 与常规看不太出差别（字体本身的字重），不是组件的问题；宿主要用主题的 `Rune.Font.Sans`，否则缺字重的系统字体会回落到常规。
- **WinForms**：无窗口元素 `Text : Element`，单元素宿主 `TextControl`、`ParagraphControl`……（共同基类 `TextControlBase<T>`）。文字内容是控件的 `Text`，元素上叫 `Content`（类不能有同名成员）；字号档位在控件上叫 `TextSize`（控件有自己的 Size）。
  - 组件的默认值是元素的虚属性（`DefaultWeight` 等）：档位为 default 时取它，所以 Strong 的 `Weight` 读出来就是 semibold；设计器只在不等于组件默认值时写代码。
  - 元素的 `Measure` 带可用的大小，文字按宽度换行。宿主同 Label：AutoSize 时不限宽，要换行就设 MaximumSize 或关掉 AutoSize。
  - TextRenderer 画字。行高按 TextRenderer 量一行的高度算（比 `Font.Height` 多一两个像素），LineClamp 的高度才与多行量出来的一致。多行省略用 `WordBreak | TextBoxControl | EndEllipsis`（最后一行放不下时不画半行，在可见的最后一行加省略号）。
  - GDI+ 的字体只有常规与粗体：semibold、bold 画成粗体，light、medium 画成常规。Numeric 不用处理（界面字体的数字默认等宽，GDI 也开不了 OpenType 特性）。
  - 字体共用（`Fonts`）：同样的（字体、磅值、样式）一个 Font，元素不持有、不释放。主题的字体是一串候选，`Fonts.Installed` 取第一个装了的（Mono 用它；宿主、文档应用也用它设主题的 Sans）。
  - 只有写了 Tone、Mono 的文字才读主题，换主题时其余文字不重画（实测：带色调的失效 1 次，跟随 ForeColor 的 0 次）。
- **WPF**：从 TextBlock 派生（ROADMAP 决定 19）。档位（字号、字重、色调、等宽）是 Generic.xaml 主题样式里的触发器，在主题这一层，使用方任何样式都盖得过（第 4 节"档位的优先级"）；没写档位时继承外面，字号写成像素数时是本地值。默认值也在主题样式里（DefaultStyleKey 每个子类各设一次），`DependencyPropertyHelper` 报 DefaultStyle。
  - TextBlock 没有最大行数：LineClamp 按行高限 MaxHeight（`FontFamily.LineSpacing × FontSize`，或 LineHeight），加 Wrap 与 CharacterEllipsis，最后一行放不下的部分变成省略号。字号、字体、行高变了要重算：TextBlock 的 `OnPropertyChanged` 是密封的，改成覆盖这几个属性的元数据、加变化回调（与基类的回调合并）。
  - Numeric 是 `Typography.NumeralAlignment = Tabular`；Align 的 Start、End 对应 TextAlignment 的 Left、Right（随 FlowDirection 镜像）。
  - WPF 的字重比 GDI 全：medium、semibold 都画得出来。

#### 可聚焦的不可用（focusableWhenDisabled，2026-11-07）

- TS 用 `aria-disabled` 代替原生 `disabled`：留在 Tab 顺序里、读屏念"不可用"、点击无效、外观同不可用、悬停不变色。平台上 IsEnabled=False 一律不能聚焦，所以按钮家族的基类（ButtonBase）加 `FocusableWhenDisabled`：
  - Avalonia：重写 `IsEnabledCore`（IsEnabled || FocusableWhenDisabled），控件仍"有效可用"、能聚焦；另设伪类 `:soft-disabled`（平台每次更新有效可用状态都会按它重设 `:disabled`，占不住），主题里 `^:disabled, ^:soft-disabled` 一个样子，悬停样式加 `:not(:soft-disabled)`。
  - WPF：IsEnabled 是强制出来的，自己写了 False 就怎么都是 False；所以覆盖 IsEnabled 的强制回调，照平台的规则（外面不可用、命令不能执行时不可用）重写一遍，可聚焦的不可用时返回 True，另记只读的 `IsDisabled`。主题的不可用外观、配方的悬停（recipe-gen 加了 `IsDisabled=False` 的条件）、链接的悬停下划线都看它。
  - 两边都在 OnClick 里拦下（键盘、鼠标、Command），读屏对等体 `ButtonBaseAutomationPeer` 报不可用（Invoke 抛 ElementNotEnabled）。
  - WinForms 还没做：Control.Enabled=false 同样不能聚焦，又不能改强制；要么宿主自己记"不可用"、不碰 Enabled，等做 WinForms 的定制入口时一起定。

#### 行高（2026-11-07，取值对照查出来的）

- vue-tsx-pro 的正文是不带单位的 `line-height: 1.5`（lineHeights.normal），按系数继承：12px 的 Small 放在 14px 的段落里行高是 18。按钮、标记的根是 tight（1.25）。
- Avalonia、WPF 的 `TextBlock.LineHeight` 是像素数、按像素继承，没写时按字体的行距（约 1.33）。所以加了继承的系数 `Leading.Factor`（默认 1.5，常量由测试对 token），Text 按"系数 × 自己的字号"设行高：Avalonia 按模板优先级设（构造时与字号、系数变化时）；WPF 用属性强制，基础值来自继承或默认值时才算，构造时先强制一次（字号一直是默认值时没有变化回调），主题样式另设 `LineStackingStrategy=BlockLineHeight`，行盒就是行高（同 CSS）。
- 按钮、标记在主题里设 `Leading.Factor` = tight，里面的 Text 跟着；ContentPresenter 生成的普通 TextBlock 直接继承按钮的行高（Avalonia `{rune:Em Resource=Rune.LineHeight.Tight}`，WPF `EmSize.LineHeight`）。WPF 的 EmSize 行高同样对继承来的值重算，标记放在按钮里也是自己的 1.25 × 字号。
- 开销：1000 个 Text 多分配约 0.8 MB（+7%），常驻每个多约 0.2 KB。
- WinForms 还没做：TextRenderer 不能设行距，多行文字要自己断行才能按 1.5 排；单行的高度可以直接按 1.5em 算，做多行文字的组件时一起做。

### Kbd、Code、Mark、Blockquote（2026-11-07）

- TS 里 Kbd 是 inline-block（一个小盒子），Code、Mark 是 inline（跟着段落换行、底色贴着文字）。所以 Kbd 做成控件（放进段落时用 InlineUIContainer），Code、Mark 做成行内文字 `Span`（Avalonia、WPF）：跟着段落换行，但行内文字没有盒子，TS 的左右内边距（Code 的 space.xs、Mark 的 0.15em）与圆角画不出来，底色就是文字的底色。WinForms 的 Text 是整段画出来的，没有行内元素：Code、Mark 做成单独的元素（像 Kbd），不能嵌在一段文字里。
- 相对字号（CSS 的 `font-size: 0.85em`）：Avalonia 绑定到外面的字号再乘倍数（`FontScale`，Kbd 按视觉父元素、Code 按逻辑父元素）；WPF 由 `EmSize.Register(…, fontScale)` 把继承来的字号强制成倍数。
- Avalonia 的行内文字照样能用 ControlTheme（按类型找到主题）。示例里段落中的加粗、斜体、小字用行内的 Span，不用 Strong、Em、Small 控件：控件放进 InlineUIContainer 后行高与基线和段落对不齐。

### Link（2026-11-07）

- TS 的 Link 是行内的 `<a>`：跟着段落换行，Tab 能聚焦，读屏念链接。WPF 直接从行内的 `Hyperlink` 派生（全都现成；外链图标是行内的 InlineUIContainer，载入后挪到文字后面——XAML 里 External 先于文字赋值）。
- Avalonia 的段落里没有能点的行内文字：Link 建在 ButtonBase 上（焦点环、可聚焦的不可用、点击与命令现成），放进段落时用 InlineUIContainer，`BaselineAlignment="Bottom"` 才和段落的文字在同一条基线上；链接文字本身不跨行换行。
- WinForms 没有行内元素：LinkControl 是单独的控件（Enter 打开，空格不打开，同浏览器）。
- 三个平台都同 LinkButton：Href 是绝对地址、用系统打开，不可用时不打开；外部链接读屏名称补"在新窗口打开"。TS 的下划线粗细与偏移（textDecorationThickness、textUnderlineOffset）三个平台都用平台默认的下划线。
- 下划线的作用域（上游 ff221742 的 LinkUnderlineScope）：取值是 Link 上写的 > 所在容器给的 > 主题与 Provider 的默认（defaults.Link）> always。Avalonia、WPF 是继承的附加属性 `Link.UnderlineScope`（Avalonia 按值的优先级判断"写了"：本地值、绑定算写了，主题、Provider 的样式不算；WPF 在 Underline 不是本地值时强制成容器给的，主题的触发器照旧）；WinForms 没有继承的属性：`LinkControl.SetUnderlineScope(容器, 值)` 记在弱表里，Link 元素从宿主控件往上找最近的一层。Card 的页头给 hover（WinForms 的页头文字是画的，给的是 Extra）。ItemList、Stat、Timeline、Descriptions、Checklist 整块给 hover，做这些组件时照做。

### CodeText、List、Indicator（2026-11-07）

- **CodeText**：TS 的 text 在三个库里叫 `Source`（Text 是 TextBlock 自己的）。Avalonia、WPF 从 Text 派生，按反引号拆成 Run 与 Code 的行内文字（Code 是部件：Avalonia `PART_Code`，WPF `CodeTextParts.Code`）；WinForms 把 Text、Code 元素排成一行，不换行。
- **List**：TS 的 ul / ol 里是 li，记号是浏览器画的（outside）。桌面上列表是竖排的面板，直接子元素里的 ListItem 由列表给记号（圆点、从 Start 起的编号、或代替圆点的图标）；ListItem 的模板画记号：在起始边 1.5em 里靠末端（后面空约一个空格），文字缩进 1.5em + space.xs（同 CSS 的 ul 内边距 + li 内边距）；带图标时不缩进，图标上边距 0.2em、后面 space.sm。文字项按 `rune:Text` 排（数据模板），行高随 Leading，与记号同一条基线。
  - Avalonia：`ListBase : StackPanel`（Spacing = space 系数 × 字号，模板优先级）；WPF：`ListBase : Panel` 自己竖排（WPF 的 StackPanel 没有 Spacing），字号档位是主题样式的触发器；ListItem 的 em 尺寸在代码里按字号算（同 Badge）。
  - WinForms：`UnorderedListControl`（WinForms 自己有 ListControl）、`OrderedListControl`、自绘的 `ListItemControl`（Text 是项的文字，子控件排在文字下面，嵌套的列表就是子控件）；大小同 Label：自然宽度，不超过布局给的宽度与 MaximumSize，高度按这个宽度换行。
  - 读屏：列表、列表项；记号不在控件视图里。项是 ContentControl，项里先文字再嵌套列表时内容放一个 StackPanel（TS 里可以直接混写）。
- **Indicator**：TS 的角标绝对定位、不占位置，中心对准被标记元素的角。Avalonia、WPF 的 Indicator 大小就是内容的大小，角标是代码组合的子元素，排在角上（伸出去的部分照样画出来）；圆形内容收进宽度的 14.6%（CSS 的 margin 百分比按宽度算）。外面一圈页面底色：Avalonia 用 BoxShadow 的 spread（不占位置），WPF 用 2px 的边框（定位时算进去）。WPF 的色调是生成的触发器（recipe-gen 的只按色调的配方），设在 `BadgeBackground`、`BadgeForeground` 上，不碰继承的文字颜色。
  - WinForms 的控件画不到自己外面、也盖不住兄弟控件：角标是叠在内容上面的小控件（Region 裁成胶囊形，外沿没有抗锯齿，但那一圈是页面底色），IndicatorControl 给伸出去的一半留出位置——这是和 TS 唯一的布局差别。
  - 读屏：角标是一段文字（Avalonia、WPF 的 Border 本来不进控件树，换成内部的子类给 Text 的 peer），名称是 Label 或语言包的说明，数字不念。

### 布局：Flex、Grid、Separator、Container（2026-11-07）

- **Flex**：CSS flexbox 的子集（方向、align-items、justify-content、wrap / wrap-reverse、gap 与行列间距、inline、grow，项自己的 align-self）。三个平台都是自定义面板；分行、伸缩、分布、对齐的计算是纯计算的 `FlexLayout`，三个库各一份、除命名空间外逐字相同（`tools/twins` 核对，CI 里跑）——三个库不共享代码（ROADMAP 决定 2），一致性靠检查。
  - 收缩按基础尺寸加权（flex-shrink: 1）。最多缩到多小：CSS 是内容的最小宽度（最长的词）；Avalonia、WPF 的测量结果不超过给的尺寸、量不出这个值，按写了的宽度或 MinWidth（同 min-width: 0，文字换行或截断）；WinForms 的想要大小不受给的宽度限制，按几乎为 0 的宽度取，得到真正的最小内容宽度。
  - 基线对齐：直接的文字取第一行的基线（Avalonia 的 TextLayout，WPF 按字体度量，WinForms 是元素的 `Baseline`），其余按底边（CSS 给没有基线的项合成的基线）。
  - grow：Avalonia、WPF 是附加属性 `Flex.Grow`（Flex 自己的 Grow 是同一个属性，放进外层 Flex 时生效）；WinForms 是 IExtenderProvider（同 FlowLayoutPanel 的 FlowBreak），设计器里是"flex1 上的 Grow"。align-self 同样（`Flex.AlignSelf`；WinForms 的组件自带的用 `IFlexItem`，竖向 Separator 用它随行高拉伸）。
  - WinForms 的 stretch 只拉伸自动大小的子控件（关掉 AutoSize 的有自己的大小，同 CSS 写了高度的项）。
- **Grid**：CSS grid 的子集（等宽列、minItemWidth 自动换列 + auto-fit 收起空列、balanced 均衡换列（最后一行最多空一格，上游 b0baadf5）、行列间距、逐行 / 逐列放置与 dense 回填、跨列跨行、align / justify-items）。计算是孪生文件 `GridLayout`。`Length` 类型认 CSS 长度（px、em、rem，rem 按浏览器的 16px）。WinForms 的跨度既能用 GridItemControl 包一层（同 TS），也能用 GridControl 的扩展属性 `SetSpan` / `SetRowSpan`（同 TableLayoutPanel）。
- **Separator**：自绘的线（DESIGN §9 的轻量组件），带文字时文字与两段线是代码组合的子元素；两段线都叫 PART_Line（代码建的元素不登记名字，重名没关系），一条部件样式同时作用于两段（同 TS 的 propsLine）。
- **Container**：Avalonia 从 Control 派生（Decorator 的 Padding 是 Thickness，和 TS 的 padding 档位撞名），WPF 从 Decorator 派生（没有 Padding）；WinForms 是 `ContainerPanel`（WinForms 自己有 ContainerControl），重写 DisplayRectangle 成居中的一栏，停靠、锚定都在里面；属性叫 ContainerSize、InlinePadding（Size、Padding 被控件占了）。WPF 原来的容器查询档位改名 `Breakpoint`（它是断点，不是容器宽度）。
- 撞名：Rune 的 Grid、Separator 和 Avalonia、WPF 自带的同名。XAML 里有前缀不受影响；C# 里同时引用两个命名空间时写全名（同 Button）。

### Toolbar、Prose、StatusBadge、Skeleton（2026-11-07）

- **漫游焦点**（TS 的 rovingFocus）：每个库一个内部的 `RovingFocus`，Toolbar 先用，以后的 Tabs、Menu、ToggleGroup 同样用。Avalonia、WPF 管项的 `KeyboardNavigation.IsTabStop`，方向键在 KeyDown 里处理；WinForms 管项的 TabStop，方向键在工具栏的 ProcessDialogKey 里处理（按钮不处理方向键，WinForms 把它交给父控件）。另一个方向的方向键吞掉、不动焦点：平台默认会用方向键在控件之间移动，TS 里浏览器什么都不做。项是按钮与链接（TS 的 items 选择器不移植）。
- **Prose**：TS 的 measure 叫 `LimitLineLength`（Measure 是布局的方法）。70ch 按字体里"0"的宽度算；WPF 强制 MaxWidth（使用方写了 MaxWidth 时不动）。WinForms 画不出 relaxed 行高（TextRenderer 不能设行距）。
- **StatusMap**：纯数据，三个库各一份孪生文件。StatusBadge 的圆点默认开着：改依赖属性的默认值不触发变化，Badge 收不到，所以构造时按模板优先级（Avalonia）/ 当前值（WPF）设，使用方写的 Dot 盖得过。
- **Skeleton**：自绘（底色与光带都是 9% 的正文色），包着的内容照常排版但不可见、不可用、对读屏隐藏。扫光：Avalonia 用代码里的 Animation 驱动私有的 Phase 属性；WPF 用关键帧动画（可见、系统没关动画时）；WinForms 读 FrameClock（只在画的时候扫）。WinForms 的骨架大小跟着子控件，子控件保持自己的大小（反过来撑它会互相影响）。

### Card、Avatar、Empty（2026-11-07）

- **Card**：Avalonia、WPF 都是代码组合的子元素（页头里的标题与 Extra 是换行的 Flex，页脚是靠结束边的 Flex，正文在骨架与内容之间换），不从 TemplatedControl / Control 派生——它们的 Padding 是 Thickness，和 TS 的 padding 档位撞名（同 Container）。WPF 的阴影单独一层（DropShadowEffect 套在正文上会连文字一起投影），正文按圆角裁剪。加载：挂上时已经在加载就立即显示骨架，之后的切换走 DelayedLoading 防闪烁；出现过骨架之后互换时淡入。读屏的项状态是"加载中"（WinForms 是 Busy 状态）。WinForms 的 CardControl 自己画表面与页头文字，子控件是正文；Header 是文字，Extra、Footer 各是一个控件；TS 的 padding 叫 CardPadding。
- **Avatar**：TS 的 name、src 叫 DisplayName、Source（Name 是控件名）；Source 是平台的图片对象，WPF 等 BitmapSource 下载完、失败时退回后备内容。首字母算法 `AvatarInitials` 是孪生文件。WPF 的 soft / solid × 色调是 recipe-gen 生成的触发器，WinForms 直接用 BadgeRecipe 的这两档。
- **AvatarGroup**（2026-11-14）：组里的头像往前叠 0.5em（按头像自己的字号），各带一圈页面底色的描边（TS 的 box-shadow，borderWidths.sm）把相邻的分开，后面的盖在前面的上面；
  超过 Max 的折叠成"+N"（和头像一样大的圆，fill 底、正文色、xs / md 的字号），读屏念"还有 N 人"（Lang.AvatarMore），Total 给了时按它算 N。
  Avalonia、WPF：Children 放头像，组里的排版层把显示的每一项包进一个画描边的外圈（Decorator，描边画在外面、不占位置）；读屏里组的子元素直接是头像与"+N"
  （Avalonia 的 NoneAutomationPeer 仍会出现在原始树里，所以由组的 peer 直接列出）。"+N"是 Border 的子类：Avalonia 用 StyleKeyOverride 让部件选择器 `Border#PART_More` 照样匹配。
  WinForms：叠放的头像要按顺序盖住前一个、描边要画到头像外面，子控件做不到，所以 AvatarGroupControl 的 `Avatars` 是画出来的 Avatar 元素；
  描边是在每个头像下面先填一圈页面底色。读屏的子项是各个头像（名称是名字）与"+N"。
- **Empty**：Avalonia、WPF 代码组合；WinForms 画图标、标题、说明，子控件与 Action 排在下面。图标默认 Inbox，设成 null 不显示（TS 的 icon={false}）。
- **UserButton**（2026-11-08）：TS 的 name、src 同 Avatar 叫 DisplayName、Source；`indicator={false}` 是 ShowIndicator=False（Indicator 只放图标）。Avalonia、WPF 建在 ButtonBase 上，内容是代码组合的 Flex（头像、名字与说明、箭头）；
  胶囊、整行、compact 三种形态：Avalonia 在代码里按 em 设内边距与圆角，WPF 是主题样式的触发器（只读的 IsDescribed）。名字只在整行时 grow：Flex 量尺寸时 grow 的子元素会占满可用宽度，胶囊就不按内容的宽了。
  WinForms 是按钮内核上的元素（ghost、neutral 正是正文色、悬停 fillHover），内核为它加了 DefaultRadius（同 DefaultVariant）与起始边、结束边可以不同的内边距；整行时量出宿主给的宽度（同 Button 的 FullWidth）。读屏名称是名字，说明是帮助文字 / 说明（compact 时没有）。
- 上游用例归属：behavior.mjs 把用例归到外层 describe 写的组件（display.test.tsx 里的 Card、Avatar），也认 it.each。

### Collapse（2026-11-07）

- 高度过渡照 TS（durations.md、easings.standard）：一个 0–1 的"展开比例"驱动想要的高度 = 内容高度 × 比例，内容一直按完整高度排、裁掉露不出来的部分（TS 用 grid-template-rows 0fr ↔ 1fr）。Avalonia 用 `Animation.RunAsync`，WPF 用 `DoubleAnimation`（FillBehavior.Stop，先写终值），WinForms 用 `Animator` 补间一个信号、每帧让父容器重排。没挂上（Avalonia 无 VisualRoot、WPF 未 Loaded、WinForms 无父容器或句柄）时直接到位。
- 完全收起（过渡结束）后内容隐藏：Avalonia `IsVisible = false`、WPF `Visibility.Collapsed`、WinForms 子控件 `Visible = false`（同 Card 的正文），等价于 TS 的 inert——不能 Tab 聚焦、读屏读不到；收起的过程中仍可见。
- 页内搜索命中时展开（`hidden="until-found"`、beforematch）是浏览器的能力，登记为不适用。lazy：Avalonia、WPF 第一次展开才把内容放进内容层；WinForms 不要（隐藏的子控件本来就不建句柄、不参与布局），parity 加了按平台的 `PLATFORM_PROPS_NOT_PORTED`。
- 测试：Avalonia 12 的无头平台推不动动画时钟（连普通的 Opacity 动画也在第 3 帧停住，`ForceRenderTimerTick` 也不行），所以 Avalonia 用作用域里时长 token 设 0 测终态、另测"收起时立即仍可见"；WPF、WinForms 的动画在测试里能真实走完。
- Collapsible（触发器 + Collapse）的触发器 API（TS 是渲染函数，拿到 aria-expanded、aria-controls 与点击）和 Accordion 一起定；文档页的示例要等 Field、TextField、NumberField、Select。

### Progress（2026-11-07）

- 取值是 `decimal?`（TS 的 number、bigint、Decimal 都能精确表示）：按原值夹在 0–Max 之间比较，`ValueText` 拿到原值；比例与读屏的数值用 double。百分比按语言包的 `Code` 格式化（`P0`）。
- 结构同 TS：条形 = 可选的标签行（0.93em，标签在起始边、数值在结束边，间距 space.sm）+ 轨道（space.xs × 1.5 粗，圆角裁剪）里的进度与目标线；环形 = 5em 的环（按 100 × 100 画布：线宽 10、半径 45，从正上方顺时针）+ 中间的数值（0.93em、半粗）+ 下方的标签（0.93em、次要色）。环形按内容宽（TS 的 inline-grid）：Avalonia 用模板优先级、WPF 用主题触发器把水平对齐设成靠左。
- 条形占满一行：不限宽时只取标签行的宽度；放进横向 Flex 时给 Grow（TS 里 width:100% 的 flex 项缩到剩下的空间，效果相同）。
- 进度比例在 durations.md 内过渡；不确定进度条形 1.4 秒、环形 1 秒一轮，系统关闭动画时变慢到 3 秒（TS 的"它本身就是信息，不随 durations 变 0"）。WinForms 的不确定动画读 FrameClock（同 Spinner），只有在画的控件驱动时钟。
- 读屏：整个 Progress 是进度条（TS 有标签行时外层不是 progressbar，桌面上合成一个元素，里面的文字对读屏隐藏），名称是 AutomationProperties.Name 或 Label，取值文字在 Value 模式里；Avalonia、WPF 另给 RangeValue（最小、最大、当前），WinForms 的自定义控件给不了。状态：Avalonia 伪类 `:indeterminate`、`:complete`，WPF、WinForms 的 `State`。
- 部件：Indicator（条形的进度，Border）、Header、Label、ValueLabel；propsRoot 不移植（外层就是 Progress 自己）。WinForms 有 PropsLabel、PropsValueLabel（新的 `TextProps` 记录），propsIndicator、propsHeader 等部件记录（自绘的部分没有元素）。

### Switch（2026-11-07）

- 色调实底（TS 的 `toneFillStyles`：`--tone-fill` 与其上的 `--tone-on-fill`）三个平台各一份映射：neutral 反色表面、accent 品牌色、状态色取文字色（黄、绿的实色在表面上不到 3:1）。Avalonia 是画刷资源键（`ToneFill.Keys`），WinForms 是 `ToneFill.Colors(tone, theme.Colors)`；以后的 Checkbox、Radio 共用。
- 尺寸都是 em：轨道 2.25 × 1.25、滑块 0.875（离轨道两端 0.1875）、轨道与文字间距 space.sm；轨道里有内容时宽度取两份中较宽的加滑块一侧 1.375、另一侧 0.5（切换时宽度不跳）；轨道与第一行文字垂直居中，高度不低于 24px（WCAG 2.5.8）。滑块在 durations.sm + 标准缓动内滑到另一端，从右到左时整体镜像。
- 行为：空格松开时切换、整块可点；只读时能聚焦、不能切换；`IsLoading` 时不能切换，滑块里转圈（`DelayedLoading` 防闪烁，读屏的“忙”立即生效）。切换前发可取消的 `Toggling`（带要切到的值），受控用法“等接口再写回”拦下它、接口回来再写 `IsChecked`（TS 的受控 `onUpdate:_checked`）。
- Avalonia、WPF：代码组合（Control 派生，不用平台 ToggleButton），轨道、两份轨道内容、滑块（内含 Spinner）、焦点环、文字都是部件元素，默认外观在主题里（Avalonia 的 `SwitchParts.axaml`，WPF 的部件样式键，WPF 多了 Track、FocusRing 两个键：没有按名字的选择器）。读屏是按钮 + Toggle 模式（同 WinUI 的 ToggleSwitch），切换中的项状态是"加载中"。轨道只在出错时画描边：Border 的底色只铺在描边里面，透明描边会让关着的轨道小一圈。WPF 的滑块阴影用 shadows.xs（WPF 生成的 sm 是给卡片的大模糊，放在小滑块上成了光晕）。
- 测试：Avalonia 的无头平台推不动动画，滑块位置用时长 token 设 0 测终态（同 Collapse）。
- 上游的原生 input 原来不继承字号（em 按浏览器默认的 13.33px 算），已报给上游并修好（9f7be887）；我们一直按控件自己的字号算。
- WinForms：
  - 元素 `Switch` + 宿主 `SwitchControl`；轨道、滑块、焦点环、轨道里的内容是画的。轨道色随滑块位置在 borderStrong 与实底之间插值（同一个 `Animator` 信号，等于 TS 的 background-color 过渡）；滑块的投影用主题的 shadows.sm（`Drawing.Shadow`，几圈半透明填充近似模糊）；不可用时每个颜色按 opacities.disabled 混进宿主的背景色（GDI 的文字不认 alpha，混色等价于整体半透明）。
  - 焦点环贴着轨道（外扩 4、宽 2），画在宿主默认的 4 像素 Padding 里，只在 `ShowFocusCues` 时画。
  - 轨道里的内容是 `SwitchContent`（文字或图标，字符串与 `IconData` 都能直接赋值：`CheckedContent = Lucide.Moon`），不另分 CheckedText / CheckedIcon；设计器里按文字编辑。图标取 `PropsContentText` 的字号（默认 xs）与色调。
  - 部件：`PropsLabel`、`PropsContentText`（`TextProps`）、`PropsSpinner`（`IconProps`）；propsControl、propsThumb、propsContent 是画出来的，没有元素，缺口同 Progress，等部件记录（`Sx`）一起定；propsRoot 不移植（外层就是 Switch）。
  - 读屏：复选按钮（`AccessibleRole.CheckButton`），名称是 AccessibleName 或文字，状态选中 / 未选中（切换时发 StateChange）、切换中带 Busy、只读带 ReadOnly；轨道里的内容不进无障碍树。MSAA 没有“必填”“校验未通过”的状态，`IsRequired`、`IsInvalid` 只影响外观。
  - 文字的行高按字体算（不是 TS 的 1.5em），在 1.5em 的第一行里垂直居中，多行时上下各留一份。

### Checkbox、Radio 与勾选类的共用部分（2026-11-07）

- 三个平台都抽了勾选类的基类 `ChoiceBase`（Switch、Checkbox、Radio 共用，同上游的 choice-styles）：控件部件在起始边、文字与说明一列、第一行 1.5em、整块可点、高度不低于 24px；点击、空格松开切换；可取消的 `Toggling`（参数改名 `ChoiceTogglingEventArgs`，Switch 原来的 `SwitchTogglingEventArgs` 去掉）；只读、出错、色调、字号、说明由派生类按上游的 props 公开（Radio 的来自单选组）。部件：文字 `ChoiceBaseParts.Label`、说明 `ChoiceBaseParts.Description`（muted、0.875em），Switch 原来的 `SwitchParts.Label` 并进来。
- Checkbox：方框 1.125em、radii.sm、2px borderStrong 描边、表面底色；勾上或部分勾选时描边与底色都是色调的实底，对勾 / 横线 0.875em、描线 3、实底上的颜色；出错时描边是危险色（排在勾选之后）。不勾时标记用透明度 0 隐藏（仍占位，同上游的 visibility: hidden）。读屏是复选框 + Toggle 模式，部分勾选是 Indeterminate（WinForms 是 Mixed）。上游的对勾描线动画（Icon 的 draw）还没做。
- Radio：圆 1.125em，圆点 0.45em 在 durations.sm 内从 0 放大（加入组时按组的值直接到位，初次显示不播）。读屏是单选按钮 + SelectionItem 模式（组没有实现 Selection 模式）。色调、只读、出错随组；字号：Avalonia 绑组的 Size，WPF 从组继承字号（组的 Size 是触发器），WinForms 取组的 TextSize。
- 组（CheckboxGroup、RadioGroup）：Avalonia、WPF 是 ItemsControl（直接写的项是自己的容器，`ItemsSource` 生成项；ItemsPanel 是 Flex：竖排 gap xs，横排换行、列距 lg、行距 xs；模板把 Padding 给 ItemsPresenter），WinForms 是 `FlexControl` 派生的 `ChoiceGroupControl`（`ItemsSource` 生成的子控件放在直接放的后面）。选项是 `ListboxOption`（三个库的孪生文件，以后 Select、Listbox 共用），任意数据给 `ToOption`。项沿树往上找最近的组（WPF 在 Loaded 时登记，先可视树后逻辑树），所以项可以套在组里的布局容器中。复选组勾选、取消时换一个新的列表（追加在末尾或去掉）。上游的 `name`（表单字段名）不移植。
- 单选组的键盘同原生 radio：整组一个 Tab 位置，停在选中项（没有选中时是第一个可用项）；方向键在组内移动并选中，到头从另一端继续，跳过不可用的，从右到左时左右反过来；只读时方向键不动。没有用 `RovingFocus`：Tab 停靠点跟着选中项而不是上次聚焦的项，移动时还要选中。
- 取值核对：`styles.mjs` 对 checkbox 页（checkbox、items、switch、radio）Avalonia、WPF 都是 0 差异。inspect 把 Checkbox、Radio、Switch 报成上游的 `choice` 部件（方框、圆、轨道是 input，对勾、圆点、滑块是 indicator，组是 choice/group）；比对工具把"透明或与底色同色的描边"与"不画描边"算作一样，原生 input 的前景色不比，方框、圆、滑块比高度。
- WinForms：元素 `Checkbox`、`Radio` + 宿主 `CheckboxControl`、`RadioControl`（共用的宿主是 `ChoiceControl<T>`）；方框、圆、圆点、焦点环是画的。Radio 的文字只能是字符串（元素不能放任意内容）：示例里单选的价格放在同一行的另一个控件里。每项四周留 4 像素给焦点环，所以组里各项的间距比上游大一些（同 Switch）。读屏没有"必填""校验未通过"的状态。
- 示例：checkbox 页五个示例都有了（Field 做好后换掉了先前的标签、说明上下摆放的替代，补了 validation）。
- 卡片外观（上游 554b7246，2026-11-08）：Checkbox、Radio 的 `Card`（bool?，不写时跟随组）与组的 `Card`、`MinItemWidth`（默认 14em），Checkbox、Radio 的 `End`（结束边的内容，WinForms 是文字加 `PropsEnd`）。卡片：内边距 space.md、xs 描边、md 圆角、surface 底，悬停 borderStrong；勾选时描边是色调的实底、底色是色调 8% 混进 surface（三个平台都在 sRGB 里插值，8% 时与 oklab 差不到一个色阶），卡片占满宽度。组：竖排一列 gap sm；横排按最小宽度 auto-fill（Grid 加了内部的 `AutoFill`：空列不收起），列距 lg、行距 xs——上游卡片的 `gap: sm` 是简写，被排列方向的 columnGap、rowGap 长写盖过。Avalonia、WPF 在 Render / OnRender 里画卡片（画刷是绑到主题的私有属性），组在卡片外观时把 ItemsPanel 换成 Grid；WPF 加了只读的 `IsEffectivelyCard`、`HasEnd` 给主题触发器撑开宽度。WinForms 元素画卡片（半像素偏移，1 像素描边不糊），宿主在卡片时不留画焦点环的 Padding（焦点环贴着方框，在卡片的内边距里）；组横排时用孪生的 GridLayout 算列数，每项定成列宽，竖排的组改成拉满宽度（同上游的 flex 列，结束边的内容才贴得到结束边）。示例：新的 cards，radio 改用 Description 与 End（Avalonia、WPF 6.8%、WinForms 1.6%：上游"谁在什么时候改了什么"差一点放不下、折成两行，我们一行放下了，卡片矮一行）。

### Field（2026-11-07）

- 组成：标签（`FieldLabel`：三个平台都自带 Label，这里改名；WinForms 是 `FieldLabelControl`）、控件（Content）、说明、错误（警示图标 + 文字）。WinForms 的字段是 `FieldControl`。标签 md、中等字重、行高 tight，必填时文字后有 0.7em 的星号（dangerText，贴着第一行顶边，对读屏隐藏）；说明 sm、muted；错误 sm、dangerText，图标上下各让 0.25em。竖排间距 space.xs。说明、错误是文字（上游可以是任意内容）。
- 状态交给里面的控件：出错（有错误且 IsInvalid 不是 false；只写 IsInvalid 时只标红不显示错误）、必填、只读；控件上自己写了的优先（Avalonia 用 `IsSet`，WPF 看值的来源不是默认值，WinForms 存成可空）。不可用写平台的 IsEnabled，里面一起不可用、标签变淡（上游"控件上写 disabled=false 优先"在桌面上做不到：不可用沿树继承）。错误、说明是控件读屏的帮助文字（TS 的 aria-describedby）。
- 控件从哪里知道字段：Avalonia、WPF 是一个继承的附加属性（字段把自己设上去，里面的元素都读得到，变了时通知控件）；WinForms 沿 Parent 链找。接入字段的控件实现内部接口 `IFieldControl`（勾选类、复选组、单选组；以后的输入框照样接）。复选组、单选组里的项不接字段，由组接。
- 第一个控件认领标签（同上游的 claim）：字段按逻辑树深度优先找第一个接入的控件（不进到控件里面），它没有自己的名称时以标签为读屏名称，点标签聚焦它（是一组时聚焦组里的 Tab 停靠项或第一个能聚焦的）；其余控件留着自己的名字。
- 标签在起始边（`LabelPlacement = start`）：标签一列（`LabelWidth`，默认 8em；文字默认贴着控件 end 对齐），列距 space.md，控件、说明、错误一列；标签的第一行基线对上控件第一行文字的基线（Avalonia、WPF 取控件可视树里第一个显示着的 TextBlock 的第一行；WinForms 取控件报的基线，否则最上面的子控件的）。`IsLabelHidden` 时标签只给读屏、不占标签列。
- WPF 的部件样式要看"实际的"出错、只读（自己的、组的、字段的），勾选类加了只读的 `IsEffectivelyInvalid`、`IsEffectivelyReadOnly`（Avalonia 是 `:invalid`、`:readonly` 伪类）。
- 取值核对：上游的 Field、Label 没有部件标记，inspect 把它们的部件按标签记（星号是 @svg）；checkbox 页（含 validation）Avalonia、WPF 0 差异。表单页（Form 的 states、layout 等）的示例要等 TextField、Select、NumberField、Form。
- 没做：表单（Form、useForm：`name`、`valueProp`、按名字取错误与约束、提交时焦点移到第一个出错的控件）、字段布局的统一设置（Form、FormSection 的 labelPlacement、readonly、disabled）。WinForms 的 propsLabel（标签是控件）、propsControl（控件列没有元素）。

### CopyButton（2026-11-07）

- 外观同 IconButton（ghost、中性色），图标固定是复制 / 对勾（上游没有 icon prop）。`Value` 是要复制的文字；上游的"value 给函数"拆成 `ValueFactory`（`Func<Task<string>>`，点击时才取，等待时 IsLoading、重复点击不再取）。`Timeout` 是 TimeSpan（默认 2 秒）；`onCopied` 是事件 `Copied`（`CopiedEventArgs.Success`、`Error`），取值与写剪贴板的异常都收在这里。
- 剪贴板：Avalonia 是 TopLevel 的 Clipboard（headless 测试里是假的剪贴板）；WPF `Clipboard.SetText`；WinForms `Clipboard.SetDataObject(text, true, 5, 100)`（剪贴板被占用时重试）。WPF、WinForms 的测试写真实的系统剪贴板，测完放回原来的文字。
- 换成对勾：Avalonia 是主题里 `^:copied /template/ rune|Icon#PART_Icon` 换图形（Icon 属性不动）；WPF 是样式触发器 `IsCopied` 换 Icon；WinForms 元素的 `IsCopied` 换 Icon。弹出动画从 0.5 倍、透明到原样（durations.md、enter）：Avalonia 用 Transitions（关键帧动画没有 TransformOperations 的插值器）、WPF 用 DoubleAnimation、WinForms 是 Animator 推一个进度信号，PaintContent 里缩放、淡入。
- 复原用 Task.Delay 再回到 UI 线程（headless 的调度器不走 DispatcherTimer），再次复制作废上一次的复原。
- 播报"已复制"：WPF 发 UIA 通知事件（RaiseNotificationEvent），WinForms 发 `RaiseAutomationNotification`；Avalonia 没有通知事件，按钮是礼貌的实时区域，复制后项状态（ItemStatus）是"已复制"（WPF 也报项状态）。
- 测试的等待：Avalonia `HeadlessUi.Until` / `Pump`，WPF `WpfUi.Until`（三处测试都要等异步结果，收进了测试的基础设施）。
- 示例：上游的 CopyButton 在 icon-button 页的 states（还要 Tooltip、focusableWhenDisabled）与 presets（还要 TextField）里，这两个示例等这些组件做好再补。

### Tabs（2026-11-07；2026-11-14 补齐可关闭、新建、排序与标签栏）

- 照上游拆成四层：Tabs（状态：Value、Orientation、Activation、Variant、Size、Ripple）> TabList（一排标签）> Tab；TabPanel 与标签按 Value 对应。
  成员按"逻辑树上最近的那组 Tabs"认领（不进到里面另一组）；没有值时选第一个标签，只显示当前标签的面板（看不见的面板不排版、不绘制——上游的 lazy 不移植）。
  档位类型 `TabsOrientation`、`TabsActivation`、`TabsVariant` 是孪生文件（`Navigation/Tabs/TabsVariants.cs`）。
- 外观：标签中等字重、textMuted（悬停与选中 text）、控件高度、左右 space.md、sm 圆角；line 时标签底边留 2 像素、列表画 borderMuted 分隔线与 2 像素的品牌色指示条
  （跟着当前标签滑过去，durations.md、standard，第一次直接到位）；soft 时列表 fill 底、xs 内边距、md 圆角，宽度按标签，选中的标签 surface 底；不可用的标签半透明。
- 键盘：漫游焦点（整组一个 Tab 停靠点，方向键跳过不可用项，到两端后从另一端继续）；automatic 时焦点到哪个切到哪个，manual 时 Enter / 空格才切换。
  点击先发 Click（Avalonia、WPF；WinForms 是 TabClick），处理里标记 Handled / Cancel 可以拦下切换（上游的 preventDefault）。
- **Avalonia**：Tabs 从 Flex 派生，TabList 是组合控件（见下面的标签栏）。面板在横排时不能 Grow（不限高的容器里会往下撑满）。
- **WPF**：同样的结构，指示条用 DoubleAnimation；标签的外观在 Tabs.xaml 里按只读的 Variant、Orientation、IsSelected 触发。合成的按键事件不改
  InputManager 的"最近的输入设备"，自动切换改在 TabList 处理完方向键之后看焦点在哪个标签上。
- **WinForms**：平台自带 TabControl、TabPage，组件库的叫 TabsControl、TabListControl、TabItemControl（标签，里面放文字或子控件）、TabPanelControl。
  FlexControl 只量自动大小的子控件（关掉 AutoSize 的按自己的大小）：示例里的固定宽度列关掉 AutoSize。没有波纹。
- **可关闭、新建、排序、标签栏（2026-11-14）**：Tab 订阅了 CloseRequested（TS 的 onClose）才有关闭按钮——xs、圆形的 X，叠在结束端（标签结束端多留 1.25em），
  不能聚焦、读屏看不到（键盘、读屏用标签上的 Delete，AcceleratorKey / KeyboardShortcut 报出来），鼠标中键也关。使用方移除标签；关掉的是当前标签时
  TabList 选它后面（没有时前面）的一个，关掉的标签有焦点时焦点交给接着选中的标签（上游这件事在 Tabs 的注销里，我们在 TabList 看子元素的增减）。
  横排时 TabList 是一个标签栏：[往前滚][列表][往后滚][全部标签页菜单][新建]，间距 space.xs、竖直居中，line 的分隔线画在整个栏上；列表按内容宽，
  放不下时收缩、横向滚动（滚动条藏起来），出现滚动按钮（只给指针用，到头的那个不可用，一次滚 80%），还能滚的那头 1.5em 渐隐；当前标签变了滚进来（留 16 像素）。
  ShowAllMenu 是按显示顺序列出标签的 Menu（MenuRadioGroup，选一项切过去）。AddRequested（onAdd）订阅了才有新建按钮。
  Reordered（onReorder）订阅了才能排序：拖动越过别的标签的中线就换位（门槛 4 像素，拖动中半透明），Alt + ←→ 挪一位并让读屏念新位置（语言包的 TabMoved）。
  和上游不同，TabList 自己挪好标签再报告新顺序（桌面上标签多是直接写的子元素，没有使用方的数组可以重排）。竖排时只有列表。
  - Avalonia：TabList 改成组合控件（Children 转给里面的 Flex），列表在内部的 ScrollArea 子类里（inspect 不把它当组件）；指示条是列表层里的 Border（部件 Indicator），
    渐隐是滚动区域的 OpacityMask；关闭按钮放在 Tab 模板的 PART_CloseHost 里。按下关闭按钮不挪焦点：焦点管理器在按下的源元素上挪焦点，按钮在隧道阶段先拦下、自己认点击。
    读屏的实时区域 `LiveRegion`（Utils/Accessibility）是看不见的一段文字，改了就发 LiveRegionChanged。
  - WPF：结构同 Avalonia（FrameworkElement 托管一个 Border）；集合没有挪动，排序是拿下再放回，列表在这期间不把它当关闭；拿下标签时可视子元素的槽位还空着，
    关掉后的另选、交焦点延后一拍。Alt 的状态合成不了，测试经过 OnReorderKey。
  - WinForms：TabListControl 横排时自己排（LayoutEngine 换成标签栏的排列），竖排仍是 Flex；栏里的按钮、标签里的关闭按钮不参加 Flex 的排列（FlexControl.SetOverlay）。
    列表外面的标签部分用窗口区域裁掉；子控件各画在自己的窗口上，没有渐隐。排序用 SetChildIndex（不换窗口，焦点、指针都不丢）。
    拿掉有焦点的标签时，焦点在 OnControlRemoved 之前就没了：子控件集合的 Remove 先记下。读屏的报告是 RaiseAutomationNotification。
    顺带修了：只写 Text 的 TabItemControl 原来不显示文字（Flex 不画 Text），现在里面放一个 TextControl。
- 示例：workspace（多页签，首页不能关）、line、soft、vertical（设置页）。

### Table（2026-11-07，静态表格）

- 照上游一个部分一个组件：Table > TableCaption、TableHead / TableBody / TableFoot > TableRow > TableCell / TableHeaderCell。Table 的 Density、Variant、Striped；
  单元格的 Align、Nowrap；表头单元格的 ColumnWidth（TS 的 width：控件自己有 Width）与 Scope。档位类型 `TableDensity`、`TableVariant`、`TableAlign` 是孪生文件（`Table/TableVariants.cs`）。
- 排版不交给平台的表格面板：单元格逻辑上属于各自的行，可视上由表格里的一个网格统一排——列宽照 CSS 的 table-layout: auto（表格宽 100%：放得下时按各列
  不换行的宽度比例分多出来的宽度；放不下时在最小宽度与最大宽度之间插值；表头单元格不换行；写了 ColumnWidth 的列定宽），行高取各格最高的。
  行底色（表头 fill；斑马纹 fill；表体指针下的行 fillHover）与分隔线（表头下 border、表体行下 borderMuted、表尾上 border）由网格（WinForms 是行）画，
  上下边吸附到设备像素：浏览器画表格也吸附，行高带小数（表头里放了徽标的那一行 35.49）时分隔线不发虚。
- 单元格的外观：text、行高 normal；表头 textMuted、sm、中等字重；表体里的行标题中等字重；表尾加粗（行标题也照表尾）。上下内边距按密度、左右 space.md，都按单元格自己的字号。
- **Avalonia**：Table 是 Control，里面外框 Border → 只挂可视子元素的竖排容器（标题、横向 ScrollArea → 网格）。标题与滚动区域要进 LogicalChildren，不然没有主题。
  ScrollArea 给内容的宽度是无限的：表格在量的时候把自己的宽度交给网格分列。外观在 Table.axaml 里按 :head、:foot、:row-header 伪类。
- **WPF**：同样的结构（FrameworkElement，AddLogicalChild / AddVisualChild），单元格按只读的 SectionKind 触发外观（Table.xaml）。分隔线用 GuidelineSet 吸附。
- **WinForms**：TableControl、TableCaptionControl、TableHead/Body/FootControl、TableRowControl、TableCellControl、TableHeaderCellControl，都是子控件，位置由 TableControl 排；
  单元格里是 Text（按行高 normal 竖直居中画）或子控件（横排、竖直居中）。行透明（先画出表格的圆角外框），底色按表格内框的圆角裁。
  GDI+ 默认像素中心在整数坐标上，整数坐标的 2 像素边线会压在两行之间发虚：外框与行用 PixelOffsetMode.Half。单元格占满整列、内边距是它的 Padding（同 td 的盒子）。
  最小宽度也放不下时表格比给的宽，外面套 ScrollAreaControl 滚动（上游表格的外层自己就是滚动区域）。
- 表头吸顶与固定列（上游 9a57a058 的 stickyHeader、sticky，CSS 的 position: sticky）：Avalonia、WPF 的 Table 加 ScrollAxis（propsRoot 的 axis），
  竖排容器把标题以下的高度都给滚动区域，限了高就是网格竖着滚。滚动时不重新排版，只给固定的格平移滚动的偏移（不超出表格）；固定的表头、固定的列、两者相交的角
  各有一层底，画对应行的底色盖住滚到下面的格（sticky 的格在 CSS 里继承行的底色），固定列那几层在结束边画 borderMuted 的竖线。叠放：Avalonia 用 ZIndex，
  WPF 的自绘元素没有 ZIndex，按层的先后重新挂可视子元素。吸顶的表头盖着的地方不高亮下面的表体行。WinForms 跟着外面 ScrollAreaControl 的偏移挪表头这个部分
  与固定的格：子控件序号小的叠在上面，前面的列本来就在后面的列上面，只有表头要提到最上；透明的格只透出父控件（行）的底色，不透出兄弟控件，正好盖住滚到下面的格。
  Avalonia 的行线吸附不能用 Math.Round：它是银行家舍入，行高带 .5 时一条线的两边差出 2 像素。示例在 ScrollArea 页（both）。
- 像素对照（plans）：Avalonia 0.08% / 0.12%、WPF 0.05% / 0.38%、WinForms 0.32% / 0.41%。剩下的主要是表头那一行：上游是文字加行内的徽标（vertical-align: middle），
  行盒撑到 21.99，我们的 Flex 是 21，下面的行线差半个像素。示例：plans；invoice 要等 ToggleGroup；column-table 在 ColumnTable 一节（仅 Avalonia）。

### ItemList（2026-11-14）

- 照上游两个组件：ItemList（列表的设置：Divided、Hoverable、Density、Inset）与 ItemListItem（Start、Title、Description、End、Content、Align）。
  档位类型 `ItemListDensity`、`ItemListAlign` 是孪生文件（`Display/ItemList/ItemListVariants.cs`）。列表给里面的链接悬停才有下划线（Link 的下划线作用域）。
- 整条可点：网页是标题里一个伸展盖住整条的链接或按钮；桌面上整条就是一个控件。Avalonia、WPF 的 ItemListItem 从 ButtonBase 派生（键盘、读屏的 Invoke、
  加载中现成），写了 Href（绝对地址，系统打开）、Command 或订阅了 Invoked（TS 的 onSelect，订阅时记下，同 Tag 的 RemoveRequested）才可点、能聚焦；
  不可点时读屏里是列表项，可点时是链接或按钮。结束边的按钮自己处理按下，点它不触发这一条。选中（IsSelected）的项状态是 DesktopMessages 的"当前"
  （网页的 aria-current 由读屏自己念，桌面要组件给）。不可用只对可点的条目有外观（同上游 data-disabled 只在可点时有）。不出波纹（上游 ripple: false）。
- 焦点环叠在条目的边上：上游 outline 宽 2、偏移 -1px，不像按钮外扩 4。Avalonia、WPF 的 ButtonBase 把外扩距离做成可覆盖的 FocusRingOutset。
- 内边距：上下按疏密（compact xs、standard sm），两侧按 Inset，不写时悬停着底色、整条可点的条目留 sm；都随字号，在代码里按列表的设置算
  （Avalonia 按模板优先级设 Padding，WPF 设 EmSize.Padding 的系数，WinForms 按像素设 Padding）。最后一条没有分隔线：Avalonia 用 `:nth-last-child(1)`，
  WPF 由列表算出 HasDivider 给触发器，WinForms 由列表算出、在下内边距里留出线宽自己画。
- 结束边那一格总在（TS 没有 end 时也放一个空的 span）：标题与说明后面总有 space.sm。说明的行高按它自己的 xs 字号算（18 而不是 21）：
  Avalonia 在部件上按自己的字号设，WPF 的行高按像素继承，代码里重算。
- **WinForms**：ItemListControl（竖排的 Flex）、ItemListItemControl（横排的 Flex：起始边一格、标题与说明一栏、结束边一格，格跟着里面的控件变大小）。
  点起始边、标题、说明同点这一条（转发子控件的点击），结束边不转发。子控件不能半透明：不可用时文字换成 textDisabled。WinForms 没有 Command，可点的是 Href 或 Invoked。
- 取值对照（todos、notifications、selectable）：Avalonia 只差列表总高 0.75（布局取整），WPF 一致（todos 的链接在 WPF 是 Text 里的行内 Hyperlink，结构上多一层 Text）。
  Avalonia 的 Link 行高原来是 NaN（放进段落时不带段落的行高），改成按自己的字号算 normal：条目标题、卡片页头里的链接同上游 21。
  示例：todos（WinForms 的标题是文字，链接写成整条的 Href）、notifications、selectable。

### DeadlineBadge 与工作日历（2026-11-14）

- 工作日算法是孪生文件 `Utils/Data/Workdays.cs`（上游 utils/data/workdays.ts）：`WorkCalendar`（周末、节假日区间、调休补班；`Weekdays` 只按周末）、
  `WorkdaysBetween`（不含起点、含终点，反向为负）、`AddWorkdays`、`DeadlineOf`（截止日或开始日加工作日；是否过期按日期比）。日期用 DateOnly。
- 工作日历在作用域上（上游 createUi / Provider 的 workCalendar）：Avalonia、WPF 是继承的附加属性 `Provider.WorkCalendar`，WinForms 是
  `ProviderControl.WorkCalendar`（`WorkCalendarOf` 沿外层找），都没写时只按周末。
- DeadlineBadge 从 Badge 派生（WinForms 从 BadgeControl），色调与文字按状态；截止日（TS 的 title）是悬停提示，也接在读屏名称后面
  （TS 另放一段 VisuallyHidden）。WinForms 没有给单个控件的提示，徽标自带一个 TooltipProvider。截止日、开始日都没给时不显示。
  Avalonia 按模板优先级设、WPF 设当前值：使用方写的盖得过。

### Menu 与 ContextMenu（2026-11-14）

- 照上游的复合组件：Menu（Trigger、Items、IsOpen、Placement、Size、Ripple）里放 MenuItem（Tone、Click / Command：TS 的 onAction）、MenuLinkItem（Href 绝对地址，系统打开）、
  MenuCheckboxItem（IsChecked）、MenuRadioGroup（Value）/ MenuRadioItem、MenuGroup（Label）、MenuSub（Label，子菜单）、Separator；ContextMenu 是区域加从指针或焦点弹出的菜单。
  档位类型 `MenuItemTone` 是孪生文件（`Overlay/Menu/MenuVariants.cs`）。WinForms 的类名带 Control 后缀，文字是 Text，项加进 Items / Controls。
- 弹出层不另写：Avalonia、WPF 的 Menu 里组合一个 Popover 的子类，Popover 开放几处内部钩子（面板内边距、打开时聚焦哪个元素、触发器的按键、面板的按键、
  关上但不还焦点、以别的元素为锚点打开、面板的读屏类型）；WinForms 同 PopoverControl 用 FloatingSurface。面板外观就是弹出框的面板，不留内边距（内容层 space.xs），至少 12em。
- 键盘：触发器上 ↓ / ↑ 打开并聚焦第一项 / 最后一项；菜单里 ↑↓ 循环、Home / End、按字母跳（漫游焦点 RovingFocus 加了类型搜索，规则同 Listbox 的 Typeahead；
  WinForms 没有漫游焦点，由一列菜单项 MenuList 处理），Esc 关上回到触发器，Tab 关上、焦点从触发器往后走。WPF 的按键里没有字符，类型搜索接 TextInput。
- 菜单项的快捷键（Shortcut，HotkeyInput 把平台按键换成快捷键的键名）在窗口里随时有效：Avalonia 在窗口上隧道接，WPF 预览，WinForms 用消息过滤器（同 CommandPalette）。
  读屏从 AcceleratorKey / KeyboardShortcut 念。复选、单选项是能切换的菜单项（Avalonia 另一个带 Toggle 的对等体：对等体只能按实现的接口提供模式）。
- 子菜单：→ / Enter / 空格打开并聚焦第一项，← / Esc 只关这一层；指针停 100 毫秒打开，离开后 400 毫秒内移进子菜单不关（上游的安全三角简化成宽限时间）；
  子菜单里执行一项整串关闭；外面的菜单关了子菜单跟着关（Avalonia 离开可视树、WPF Unloaded、WinForms 跟着藏起来）。WinForms 的浮层窗口激活即拿焦点：
  父菜单失去激活时先看激活的是不是自己的子菜单（沿 FloatingForm.Origin 找回来），子菜单因点了外面关时再问父菜单要不要一起关。
- 触发器的读屏（TS 交给触发器的 aria-haspopup、aria-expanded）：Popover 在触发器上记展开状态，按钮家族的对等体据此报展开 / 收起（Avalonia 的对等体总带这个模式，
  不是触发器时是 LeafNode；WPF 只在触发器上有；WinForms 是 HasPopup 与 Expanded / Collapsed 状态）。弹出框的触发器同样受益。
- 右键菜单：Avalonia 接 ContextRequested，WPF 接 ContextMenuOpening，WinForms 接 WM_CONTEXTMENU；指针时在指针处（平台弹出层的 Pointer / MousePoint），
  键盘时在获得焦点的元素下方；关上后焦点还给打开前的元素。名称默认取语言包的 ContextMenuLabel。
- WPF 的坑：`new FrameworkPropertyMetadata(null, (d, _) => x = y)` 选中 (PropertyChangedCallback, CoerceValueCallback) 重载，有返回值的 lambda 成了强制回调，属性设不上；
  回调写成语句体。WPF 的内容呈现器不给内容逻辑父元素：菜单把滚动区域挂成自己的逻辑子元素，菜单项沿逻辑树找所在的菜单。
- 示例：actions、description、checkable、account、zoom、context-menu（WinForms 的文件卡片只有文字）。WPF、WinForms 的弹出层是单独的窗口，静态截图拍不到。

### NavList（2026-11-07）

- 照上游：NavList > NavItem / NavGroup（可展开）/ NavSection（带小标题、一直展开）。桌面上没有路由：NavList 的 `CurrentHref` 是当前地址（TS 路由适配的 isActive
  按它判断：地址相同的项是当前页，NavItem 的 `Active` 可以直接指定），点击先发项自己的 Click，再发 NavList 的 `Navigate`（TS 交给路由的 navigate），
  没有被处理就把 CurrentHref 改成那一项的地址。含当前页的分组自动展开（离开时不收起）。当前页读屏：Avalonia、WPF 的项状态是"当前页"，WinForms 是选中的链接。
- 导航行（NavItem 与分组标题共用，TS 的 itemStyles.row 加 navListStyles.row）：控件高度、上下 space.xs、左右 space.md、md 圆角、图标与文字隔 space.sm，
  中等字重，textMuted / 悬停 text 与 fillHover / 当前页 accentSoft、accentText。`Badge` 是计数角标：展开时是文字后面的实心徽标，紧凑时是图标上的小圆点（Indicator）。
- `Compact`：项只剩图标、居中，名称在提示里（右边、200 毫秒）；小节标题只剩图标，上面一条 borderMuted 的细线；分组的子列表不缩进。
  没做：紧凑时分组悬停弹出子项（TS 用 HoverCard，等它）；AppShell 的侧栏收成图标栏时自动紧凑（等 AppShell）。
- **Avalonia**：导航行是 ButtonBase 派生的 `NavRow`（模板里图标套在 Indicator 里、文字、尾部内容、徽标、箭头），分组标题 `NavGroupHeader`。
  分组、小节是 Control，里面的标题行与列表是代码组合的（Items 转进内部列表，逻辑父是列表）；部件名 NavRowParts、NavGroupParts、NavSectionParts。
  ControlTheme 里不能写后代选择器：小节的部件样式在 NavListParts.axaml（Styles）。
- **WPF**：同样的结构，部件是样式键。`new FrameworkPropertyMetadata(null, (d, _) => x.P = y)` 会选中 (PropertyChangedCallback, CoerceValueCallback) 的重载
  （赋值表达式有返回值，lambda 能当强制回调），属性被强制成旧值：属性回调里只调 void 方法。显式部件样式替掉了隐式样式，部件是 Text、NavRow 这类有主题样式的要 BasedOn。
- **WinForms**：NavListControl、NavItemControl、NavGroupControl（Items 是内部列表的子控件）、NavSectionControl；导航行是 FlexControl（图标、文字、徽标是子控件），
  提示用 NavListControl 自带的 TooltipProvider。内边距、控件高度按字号：从窗体继承来的字体到得晚，字体或 DPI 变了重新同步。分组的箭头换图形（GDI 的图标不转）。
- 像素对照（groups、sections、compact）：Avalonia、WPF ≤0.1%；WinForms groups、sections ≤0.2%，compact 2.6%（Switch 那一行四周的焦点环留白）。示例：toc 等 TableOfContents。
- 做的时候查出 Flex 的一处问题（Avalonia、WPF）：分到的正好是自然尺寸时，第二遍仍按这个有限值量子元素，布局系统之后按这个旧约束重量，子元素长高报不上来
  （嵌套的竖排、卡片矮几像素）。没有伸缩的项改成按不限量，摆的尺寸等于内容尺寸时不重分。

### TableOfContents（2026-11-07）

- `Items` 是 `HeadingEntry(Id, Label, Level)`（TS 的 HeadingEntry；Id 是标题控件的 Name——WPF 的 Name 只能是标识符，示例里用下划线）；`ScrollRoot` 是正文所在的
  滚动区域（Avalonia、WPF 是 ScrollViewer，ScrollArea 本来就是；WinForms 是 ScrollAreaControl，为此补了 `ScrollChanged` 事件）。标题默认取语言包，同时是导航的名称。
- 滚动高亮照 TS 的 useScrollSpy：当前节 = 顶边已经越过视口顶边（+1）的最后一节；滚到底时取视口里的最后一节。滚动之后、滚动区域排版之后都重算
  （Avalonia 挂 LayoutUpdated，WPF 的 ScrollChanged 本来就带范围与视口的变化，加载后再算一次；WinForms 的 ScrollChanged 在每次摆放内容后发）。
  点目录项把那一节滚到视口顶上（TS 的页内锚点）。当前节的链接 accentText，读屏的项状态是"当前位置"（WinForms 是选中的链接）。
- 外观：标题 sm、加粗、下面 space.sm；列表左边一条 border 细线；链接 sm、行高 snug、左边按级别缩进（2 级一档，每深一级多一档，每档 0.75em）；
  指示条 2 像素宽、圆角、品牌色，盖住细线，对准当前链接，第一次直接到位、之后滑过去（durations.md、standard）。
- 像素对照（nav-list 页的 toc）：Avalonia、WPF 0.01%；WinForms 0.9%–1.0%（GDI 的文字换行宽度与行高）。

### Breadcrumbs（2026-11-14）

- `Items` 是 `BreadcrumbItem(Label, Href, Icon)`（TS 的 BreadcrumbItem；key 是 Vue 的列表标识，不要）；`Href` 同 NavList 是字符串，链接上按相对或绝对地址转成 Uri。
  `Separator` 只收图标（TS 是任意节点：同一个控件不能放在多处，每处要新建一个；示例里的分隔符也只是图标），不写时是右向 chevron、从右到左时翻转。
- `MaxItems`：超过 max(MaxItems, 3) 项时留第一项与最后两项，中间换成菜单按钮（sm 的 IconButton，名称取语言包），被折叠的各级在菜单里：有地址的是链接项，
  没有地址的交给导航，没人处理时不可用。
- 应用内的层级（TS 的 onNavigate）：Avalonia、WPF 是冒泡的路由事件 `Navigate` 加 `NavigateCommand`（参数是那一级），WinForms 是 CLR 事件；
  订阅了事件（或写了命令）时没有地址的各级才是 link 样式的按钮——事件的 add / remove 访问器记着订阅数并重建。当前一级的读屏状态是"当前位置"，否则"当前页"
  （Avalonia、WPF 的 ItemStatus；WinForms 没有 aria-current，写在 AccessibleDescription 上）。名称没写时取语言包（面包屑）。
- 外观：整条 sm 字号、横排换行、项与项、图标与文字之间 space.xs；链接次要色、悬停下划线；当前页正文色、中等字重（GDI 只有常规与粗体，WinForms 画成常规）；
  分隔符次要色。WPF 的 Link 是行内元素（Hyperlink），放在 Text 里，图标是行内控件，与文字之间一个空格（约 0.25em）；WinForms 的链接只有文字，图标放在旁边，同链接是次要色。
- 部件：Avalonia、WPF 有 List、Item、CollapsedItem、Link、Button、Menu、Label、Current、Separator、Expand；WinForms 没有部件属性（同 TableOfContents，记在 parity 的欠账里）。

### Steps（2026-11-14）

- 照上游：`Items`（`StepItem(Title)` 加 Description、Icon、Status、Href、IsDisabled）、`Current`（Avalonia、WPF 默认双向绑定；WinForms 发 `CurrentChanged`）、
  `Orientation`、`Size`（WinForms 叫 TextSize）、`Navigable`。`StepsOrientation`、`StepState`、`StepItem` 与推状态的规则（`StepRules`：写了 Status 用它，
  否则之前已完成、当前、之后未开始；已完成或出错、又没标不可用的才能点回）是孪生文件 `Navigation/Steps/StepsVariants.cs`。
- 可点回的步骤（有地址或 Navigable）是按钮：点了写回 Current，有地址时打开链接；悬停 fillHover 底、sm 圆角。读屏：Avalonia、WPF 每步的项状态是这一步的状态
  （当前步骤、已完成、未开始、出错，取语言包的 stepState），WinForms 写在说明上。
- 外观：圆圈直径 2 × 0.93em、xs 边框、加粗，按状态换色（已完成品牌色底、对勾；当前品牌色边；未开始 border 边、次要色；出错 danger 边、dangerSoft 底、警示图标）；
  文字区上面留 0.3em、标题中等字重、说明 0.93em 次要色。横排各步等宽（TS 的 flex: 1 1 0，我们的 Grid 等宽列，间距 sm），竖排间距 lg。
- 连接线不是元素，由 Steps 按圆圈的位置自己画：横排从圆圈与文字的右边隔 sm 延伸到这一格的右边、对齐圆圈中线；竖排从圆圈下面隔 xs 连到下一步的圆圈上面 xs；
  这一步已完成时品牌色。WinForms 的子控件是各自的窗口，会盖住父级画的东西，所以每步按自然宽度放、不撑满一格，线画在空出来的地方。
  圆圈在 WinForms 是一个元素（StepIndicator：圆、边框、对勾 / 警示图标 / 图标 / 序号），WPF 的状态色由附加属性 `Steps.State` 上的样式触发器给。

### Pagination、Pager（2026-11-14）

- 页码序列是孪生文件 `Navigation/Pagination/PaginationItems.cs`（TS 的 paginationItems，同 MUI：首尾各 boundaries 页、当前页左右各 siblings 页，
  中间省略号，序列长度固定；省略号是 null）；`PageCount(pageCount, total, pageSize)` 算总页数。
- Pagination：`Page`（Avalonia、WPF 默认双向绑定；WinForms 发 `PageChanged`）、PageCount、Total、PageSize、Siblings、Boundaries、ShowTotal、
  `Href`（Func&lt;int, string&gt;：给了时页码是 LinkButton、上一页下一页是 IconLinkButton）、Size（WinForms 叫 TextSize，默认 sm）；不可用用平台的
  IsEnabled / Enabled。Page 超出范围时显示时夹回去。当前页 soft 品牌色、读屏"当前页"（WinForms 写在说明上），其余中性 ghost；
  页码按钮至少一个控件高度宽、左右内边距 xs（WinForms 为此给 ButtonSx 加了 `MinWidth`：按钮的底色按元素量出的盒子画，控件的最小尺寸撑不宽它），
  省略号同宽、次要色。
- Pager：`Previous`、`Next` 是 `PagerLink(Href, Label)`，两张链接卡片（提示一行带箭头、次要色 sm，标题 accentText 中等字重，border 边框、md 圆角，
  悬停边框品牌色），每张至少 14rem、平分，放不下时换行；只有下一篇时前面空一格。WPF 的卡片外观在它自己的默认样式里：EmSize 的内边距系数只盖
  默认样式（主题）里的值，部件样式 BasedOn 了 ButtonBase 的样式时 Padding 来源成了"样式"，系数就不生效。
- WinForms 的可点一块（Steps 可点回的步骤、Pager 的卡片）共用内部的 `PressableFlex`（Utils/Layout）：能聚焦，点击、Enter、空格发 Click，
  子控件的点击、悬停转给它，默认悬停 fillHover 底、sm 圆角、键盘聚焦画焦点环，子类换圆角与底色、边框的画法。
- PageSizeSelect 是 Select 的子类（外观同 Select，默认 sm）：`PageSize` 与 Select 的字符串 Value 互相同步，用户换了条数发 `PageSizeChanged`
  （TS 的 onSizeChange），选项文字取语言包（"20 条/页"）。值那一格按最长的选项定宽（TS 把所有选项叠在同一格），换条数时宽窄不变。
- PageJump："跳至 [ ] 页"，输入框是没有加减按钮、4.5em 宽的 NumberField，回车或失焦后跳过去（四舍五入、夹在 1–PageCount）并清空；
  读屏念输入框的名称（"跳到第几页"），前后两段文字只是视觉提示。
- PaginationBar：总数在起始边，页码、每页条数、跳页一组在结束边，换到下一行时也靠右（TS 的 margin-inline-start: auto）；换条数回第 1 页。
  `TotalText`（Func&lt;PageRange, string&gt;，TS 的 showTotal 给函数）自己写总数；ShowTotal、ShowSizeSelect、ShowJump 关掉不要的部分。
  按自己的宽度取舍（TS 的容器查询）：窄于 36em 不显示跳页，窄于 24em 总数单独一行（em 按字号档位与 DPI 算）。WinForms 在 OnSizeChanged 里
  判断，变了时把改子控件挪到这一轮排版之后（BeginInvoke），排版中途改子控件会再触发排版。
- 踩过：WinForms 的容器重写 GetPreferredSize 后丢了基类"先把给的大小夹到 MaximumSize 再量"的一步，限了最大宽度的卡片按不限宽量出一行的高度，
  宽度被夹窄后内容换行、被裁掉（20rem 卡片里的分页栏）；改在三个容器共用的 PreferredSizeCache 里夹。

### ButtonGroup（2026-11-14）

- 相连的一组照上游拆成两半：组只说方向（`Attached`，孪生文件 `Utils/Style/AttachedVariants.cs`：AttachedOrientation、AttachedCorners、
  `AttachedRules.Squared` 第几个去掉哪几个角、`Overlap` 叠一条边框），控件自己收圆角。以后的 ControlGroup（输入框与按钮连成一条）共用这一套。
- 组的外观（Variant、Tone、Size / WinForms 的 TextSize、Radius，都可空）是按钮的默认值，按钮自己写的优先；没写 Tone 时组里的按钮都是 accent
  （上游 useButtonAppearance 的 fallbackTone：图标按钮在组里也是，拆分按钮的两半同色）。交叉轴拉齐（align-items: stretch），悬停的浮到上面、聚焦的再高一层。
- **Avalonia**：`ButtonGroup : Panel`，外观写在模板优先级上（主题之上、本地值之下），记下返回的 IDisposable，换外观、移出组时只撤自己写的；
  `Attached.Corners` 在 ButtonBase 的 CornerRadius 强制回调里收角。焦点环的直角仍是直角（之前按"圆角 + 外扩"会给 0 圆角也加上弧）。
- **WPF**：没有模板优先级，ButtonCore 的四个属性加强制回调：值来自默认值、默认样式（主题）时换成组的。WPF 先调强制、后记新来源，
  刚写的本地值在强制里看不到：要盖的时候排一次 Send 优先级的重新强制（用集合防止反复排）。收角放在 EmSize.CoerceCornerRadius（文字输入、Select 也走它）；
  波纹的裁剪改成四个角各自的圆角（`Attached.RoundedRect`）。拉伸用 SetCurrentValue，不占本地值。
- **WinForms**：`ButtonGroupControl`，ButtonCore 的档位按"自己写的 → 所在组 → Provider 默认 → 组件默认"取（组给的不算 Explicit，设计器不序列化）。
  按钮四周给焦点环留的边距在相邻一侧去掉、邻居紧贴着，那一侧的焦点环收进按钮里面；子控件是窗口，浮到上面要调 z 序，排列顺序另外记。
  从右到左时 WinForms 不镜像绘制，左右两侧的角与边距对调。
- 做的时候查出两个平台的按钮底色没铺到边框下面（CSS 是 background-clip: border-box）：WPF 的 Border 只在边框里面填底色，透明边框的实底、浅色底按钮
  四边各窄 1 像素，组里两个实底之间露出一条缝——按钮家族、ItemList、NavList、Tabs 的模板把底色拆成单独一层（`PART_Fill`）；WinForms 的 ButtonCore
  按描边的路径填底色，窄半个边宽，改成底色铺满、描边居中画在里面，并按 ControlShell 的做法用 PixelOffsetMode.Half（2 像素的边框之前糊成 3 像素）。

### ControlGroup（2026-11-14）

- 与 ButtonGroup 共用相连的一组：三个平台都抽出了 `AttachedGroup`（WinForms 是 `AttachedGroupControl`）——排列、叠边框、交叉轴拉齐、写角、
  悬停与聚焦浮上来、读屏的组；派生类只说哪些控件分剩余宽度（ControlGroup 的输入框，TS 的 attached 横排 flex-grow: 1）与自己的默认值。
- 收角接到所有外壳上：Avalonia 的 `Attached.SquareCornersOf<T>()`（ButtonBase、TextInputBase、Select 的静态构造里调用）；WPF 本来就走
  EmSize.CoerceCornerRadius；WinForms 的输入框、选择框由 ControlShell 画，角记在 `Attached` 里（按钮家族记在 ButtonCore 上）。
- 默认占满一行（`Inline` 时按内容宽）；放在 Field 里时组是 Field 的控件（实现 IFieldControl，Field 找控件时不再往里走）：标签、说明给组的读屏名称与帮助文字，
  里面的控件各写名称。点标签聚焦组里第一个控件——WinForms 的 FieldControl 原来按 Controls（z 序）找，组会把悬停、聚焦的那个调到最上面，改成按 Tab 顺序。
- WinForms 的按钮在组里与输入框等高：不留焦点环的边距；焦点环按宿主留的边距决定画在外面还是收进按钮里面（ButtonGroup 相邻一侧、ControlGroup 四周）。
- 做的时候补上 WinForms 文字输入的按键事件：焦点在里面的 TextBox 上，外壳的 KeyDown、KeyUp、KeyPress 收不到（回车搜索写不了），改成转给外壳。
- TS 里组隔开了 Field 的出错状态（只把不可用、只读交下去）；这里组里的控件仍按所在 Field 显示出错。

### NumberField（2026-11-14）

- 值是 `decimal?`（TS 的 valueType 在 number 与 bigint 之间选，C# 的 decimal 十进制精确，金额、大整数都不经过浮点数），空着时 null；
  Avalonia、WPF 默认双向绑定，WinForms 发 `ValueChanged`。Min、Max、Step（默认 1）、BigStep（默认 Step × 10）、Format（NumericFormat）、
  Icon、Prefix、Suffix、Stepper（默认 true，只读时不显示）、Clearable（清空写回 null）。外壳同 TextField（共用 TextInputBase / TextInputControl）。
- 规则是孪生文件 `Input/NumberField/NumberRules.cs`：TryParse 认当前语言的小数点与千分位、全角数字与全角句点；Snap 从 Min（或 0）按步长
  四舍五入（远离零）再夹到范围；有 Scale（或百分比）时显示与编辑都按缩放后的单位，写回时换回值本身的单位。
- 输入时是草稿：失焦或回车时解析、对齐、夹住后写回，解析不了时恢复原值；文字没改过时不写（只是聚焦再离开，不该把值取整）。
  没在编辑时按格式显示，编辑时去掉千分位。↑ / ↓、Page Up / Page Down、Home / End 是加减（spinbutton 的约定），不挪光标。
- 加减按钮（ghost、中性、圆形，不在 Tab 顺序里，名称取语言包）到头时不可用；按住连续加减：先走一步，停 400ms 后每 60ms 一步（同 React Aria）；
  指针按下已走过一步，随后的 Click 不再走，读屏、键盘触发的 Click 照常走。读屏里是 SpinButton（Avalonia 的 Spinner、WPF 的 RangeValue 模式）。
- NumericFormat 加了 `Unit` 样式（TS 的 Intl 单位格式）：常用单位按语言取符号或文字（°C、kg、米 / m、天 / day），桌面没有 Intl 的完整单位表。
- 文字输入的外壳给子类留了口子：`ClearText()` 可重写（NumberField 清空时写回 null）、读屏对象可继承（TextInputPeer / FieldAccessibleObject）。
- TS 的 name（表单字段名）是浏览器的概念，没做。

### ToggleGroup（2026-11-07）

- 照上游：ToggleGroup（SelectionMode、Value / Values、IsRequired、IsReadOnly、Orientation、Size、Ripple）> ToggleItem（Value、Icon、文字）。TS 的 `_value`
  按选择方式是字符串或数组，C# 拆成单选的 `Value` 与多选的 `Values`（同平台的 SelectedItem / SelectedItems）。选择规则（`Selection.Toggle`，TS 的 toggleSelection）
  与 `SelectionMode` 是孪生文件 `Utils/Data/Selection.cs`（以后 Listbox、Select 共用），`ToggleGroupOrientation` 是 `Input/ToggleGroup/ToggleGroupVariants.cs`。
- 外观：组 fill 底、md 圆角、四周与项之间 space.xs；项同按钮——没选中是中性色的 ghost，选中时品牌色：单选时底色是组画的滑块（accentSoft 底、accentBorder 描边，
  切换时滑过去），多选时项自己是 surface。只有图标的项是方的。滑块的边吸附到设备像素（WPF 用 GuidelineSet，Avalonia 按到根的偏移取整）。
- 焦点：整组一个 Tab 停靠点（选中项；漫游焦点加了 `Preferred`：还没聚焦过时优先的项），方向键在组内移动；放进 Toolbar 时不另管焦点（交给工具栏）。
  放进 Field 时以标签为名称，沿用必填、只读（不可用由平台的 IsEnabled 继承）。读屏里每项是切换按钮（Avalonia、WPF 的 Toggle 模式；WinForms 是按下的按钮）。
- **Avalonia**：ToggleItem 从 Button 派生，外观（样式、色调、字号、波纹）写在模板优先级上：主题的默认值之上、使用方的样式之下。Button 模板里空的文字位
  在只有图标时要藏掉，不然多一个间距、图标偏离中心。平台没有 Rect 的过渡：滑块用一个插值过渡（InterpolatingTransitionBase）。
- **WPF**：外观在主题样式里按只读的 IsSelected、IsSliding、IsGroupReadOnly 触发；滑块用 RectAnimation。
- **WinForms**：ToggleGroupControl、ToggleItemControl（ButtonControl 加 ToggleItem 元素）；WinForms 的按钮四周各留了焦点环的位置，分段控件因此偏大
  （同 button 页，WinForms 焦点环留白那项统一处理）。
- 像素对照：Avalonia segmented 0.26%、vertical 0.13%，WPF 0.30% / 0.11%；multiple 两个平台约 0.7%–2.7%：卡片在 0.75em 的间距之后落在半像素上，
  浏览器画盒子时把边吸附到整像素，我们按小数画、边线发虚（DOCSITE 记着这类）。做的时候查出 WPF 的 FieldLabel 设了行高却没按块排行，标签按字体的自然行高撑高了 1 像素多，已修。

### TextField 与 CharacterCount（2026-11-07）

- TextField 从平台的文字输入派生（Avalonia、WPF 是 TextBox；WinForms 的 TextFieldControl 是外壳里放无边框的 TextBox）：值是 `Text`（TS 的 _value），
  只读、长度上限、撤销、输入法都是平台的。外壳（vue-tsx-pro 的 inputRecipe）：控件高度、左右 space.md、xs 描边（悬停 borderStrong）、md 圆角、surface 底，
  图标与文字隔 space.sm；soft 是 fill 底、没有描边；只读 borderMuted 与 fill 底；出错 danger 描边与焦点环；里面有焦点时 2 像素的焦点环压住边框。
  `Variant`（ControlVariant，孪生文件 `Input/Control/ControlVariants.cs`，以后 Textarea、Select 共用）、`Size`、`Radius`、`Icon`、`IconEnd`、`Prefix`、`Suffix`、
  `Clearable`、`ShowCount`、`Type`（TextFieldType，孪生文件；桌面上只有 password 不同）、`IsInvalid`、`IsRequired`。放进 Field 时以标签为名称，沿用出错、只读。
- 密码：Avalonia 用 TextBox 的 PasswordChar / RevealPassword；WPF 的 TextBox 不能显示圆点，模板里叠一个 PasswordBox（password 且没显示原文时代替文字输入，
  与 Text 双向同步）；WinForms 是 UseSystemPasswordChar。显示密码的按钮名称不变（"显示密码"），按下切换显示。
- CharacterCount：显示"12 / 100"、超出上限时 dangerText；读屏的名称是完整的句子（语言包的 characterCount），看得见的字数不另分部件。
- **Avalonia**：TextBox 的模板只要求 PART_TextPresenter；单行没有放 ScrollViewer（测试只加载 RuneTheme，平台的 ScrollViewer 没有模板），长文字不跟着光标滚动，
  以后换成 ScrollArea。框里的按钮点击不把焦点交给输入框。
- **WinForms**：平台的 TextBox 不能透明，底色跟着外壳的状态换；默认的读屏名称会取 Text（输入的值），改成只认 AccessibleName 与字段的标签。
  放在 FieldControl 这类不是 FlexControl 的容器里时要自己并上最小高度。
- 外壳收进 `TextInputBase`（WinForms 是 `TextInputControl`，放在 `Input/Control`，对应上游的 control.tsx）：外观档位、占位文字、清空、字数、字段的状态、
  读屏名称、焦点环都在这里，TextField 与 Textarea 只写自己的部件；主题是共用的 `Rune.TextInput`（Avalonia 的 ControlTheme、WPF 的 Style），两个组件 BasedOn 它、只写模板。
- **format**（TS 的 text-format）：孪生文件 `TextFormat`（`Display` / `Parse` / `DisplayPosition`，现成的 Phone、IdCard、BankCard，`Grouped` 写别的）。
  TextField 的值是 `Value`（TS 的 _value，Avalonia、WPF 默认双向绑定；WinForms 有 ValueChanged）：没有格式时同 Text，有格式时输入框里分组显示、Value 是原文。
  输入后重新分组、光标放回原来那个字符后面：Avalonia、WPF 投递到这次输入处理完之后再做（TextBox 改完文字还会再挪光标，在 TextChanged 里读到的光标是旧的）；
  WinForms 的 Edit 控件在 EN_CHANGE 时光标已经到位，直接做。TS 的 inputmode（手机上的键盘）桌面没有。
- **Textarea**：Rows（默认 3）、MaxRows、Fixed、Clearable（贴第一行）、ShowCount（贴右下角）。高度照上游实际的效果：至少 Rows 行加文字区上下各 space.xs
  （上游是 rows 属性加自动增高），至多 MaxRows 行（含内边距，上游的 max-height 是 border-box），超过后出现滚动条。踩过：Avalonia 的 TextBox 每次测量都把
  PART_ScrollViewer 的最小、最大高度改回自己的 MinLines、MaxLines，行数的高度要设在外面一层（`TextareaParts.Rows`）；WPF 的行高用 TextBlock.LineHeight
  （BlockLineHeight）；WinForms 的 TextBox 改不了行距，行高是 Edit 控件的（tmHeight，用 TextRenderer 量，不是 Font.GetHeight），滚动条是系统的。
  拖动右下角调整高度（TS 的 fixed 时 resize: vertical）没做。
- 没做：form 示例（要 Form）；formats 示例里的校验（要 Form 的 schema）；Field 的说明是纯文字，TS 的 formats 示例在说明里用 Code 显示值。

### AppShell（2026-11-07）

- 照上游：顶栏（3rem，surfaceTranslucent、下边 border）、品牌格（有侧栏时宽屏下与侧栏同宽 16rem，结束边的竖线接上侧栏，顶栏内容与正文起始边对齐）、
  导航（`Nav`）、内容（`Header`）、全屏按钮；主体是侧栏（收起成 3.5rem 的图标栏，底部收起栏）、正文（space.xl 内边距）与页脚、右侧栏（14rem）。
  断点按外壳自己的宽度（TS 按视口）：lg 以上侧栏常驻、更窄收进抽屉（顶栏最前面出现菜单按钮），xl 以上显示右侧栏，md 以下不显示全屏按钮。
  正文那一列是外壳自己的滚动区域（`MainScroll`，TS 是整页滚动），本页目录的 ScrollRoot 给它。文案取页面组合的文案表 `PatternsMessages`（lang-gen 从上游
  patterns/messages.ts 生成），窗口按钮的文案是桌面专有的 `DesktopMessages`（孪生文件）。
- 收起：外壳在侧栏上设 NavList 的紧凑范围（TS 的 NavCompactContext：Avalonia、WPF 是继承的附加属性 `NavList.CompactScope`，WinForms 是外壳设的
  `ScopeCompact`），品牌只剩标志（产品名仍是读屏名称）。AppShellBrand 是没有底色的按钮（TS 的 href 换成 Click / Command）。
- 全屏：隐藏顶栏和侧栏、右上角的退出按钮、Esc（在窗口一级监听，焦点不在外壳里也算）；外壳是窗口的内容时窗口也全屏（Avalonia 的 WindowState.FullScreen，
  WPF、WinForms 是无边框再最大化），退出时还原；从别处退出了窗口全屏，外壳也退出。
- 窄时的抽屉先用外壳自己的面板（遮罩 + 起始边 xs 宽的面板，标题"导航"与关闭按钮），侧栏的内容在常驻侧栏与抽屉之间搬（一个控件只能有一个父级）；
  抽屉里的导航换了当前页就关上。Drawer 做好后换成它。
- **顶栏兼作标题栏（桌面特有，用户 2026-11-07 定）**：外壳是窗口的内容（WinForms：铺满窗体）时 `ExtendsContentIntoTitleBar`（默认开）去掉系统的标题栏，
  顶栏的空白处拖动、双击最大化、右键系统菜单，结束边是自绘的 `WindowCaptionButton`（46 像素宽、与顶栏等高，悬停 fillHover、关闭悬停 danger）。
  系统的缩放边框、贴边都保留；最大化按钮对系统报 HTMAXBUTTON，Windows 11 悬停时照样弹出贴靠布局——它因此收不到客户区的鼠标，悬停、按下从非客户区的消息转过来。
  命中规则三个平台一样：从命中的元素往上，最大化按钮是 HTMAXBUTTON，别的窗口按钮与能聚焦的控件是客户区，都不是就是标题栏（顶栏里的提示文字也能拖）。
  - **Avalonia 12**：`WindowDecorations = BorderOnly` + `ExtendClientAreaToDecorationsHint`，元素标 `WindowDecorationProperties.ElementRole`
    （顶栏 TitleBar，里面能聚焦的控件 User，三个按钮 Minimize / Maximize / CloseButton；平台从命中的元素往上找第一个有角色的）。Full 时平台会自己画标题栏，所以用 BorderOnly。
  - **WPF**：WindowChrome（CaptionHeight 0、保留缩放边框、1 像素玻璃框留住阴影）之后再挂 HwndSource 钩子——后挂的先收到消息，WM_NCHITTEST 在这里判断；
    最大化时窗口超出显示器一圈，内容按工作区（全屏时按整个显示器）缩回来。
  - **WinForms**：NativeWindow 子类化窗体：WM_NCCALCSIZE 把客户区上边挪回窗体顶端（最大化时让出边框厚度），WM_NCHITTEST 时顶上一条是缩放边框。
    踩过：Control 默认可选中（Selectable），外壳里的容器要关掉，不然顶栏全是客户区；`Form.ClientSize` 仍按系统标题栏换算窗体大小，实际客户区多出标题栏的高度；
    Form.DrawToBitmap 用 WM_PRINT 画非客户区，会画出经典主题的标题栏（屏幕上没有，核对要截屏）。WinForms 的子控件不能半透明：顶栏的半透明底直接合成，
    抽屉的遮罩画在打开时外壳的截图上；各格自己排、自己画，没有部件属性（PLATFORM_PROPS_NOT_PORTED）。
- 示例：fullscreen（整页示例：`Example.Framed`，inspect 不留内边距，同上游的 iframe）；window 是桌面特有的，在新窗口打开外壳。admin 要等 Stat，mixed 要等 NavBar。

### Listbox 与列表状态（2026-11-07）

- 数据层是孪生文件 `ListState`（TS 的 list-state）：选项按分组排好（没有分组的在前，组按第一次出现的顺序）、高亮项、已选项（`Selection.Toggle`）、
  移动跳过不可用项、到头停住、Page Up / Down 一页 10 项、类型搜索（`Typeahead`：连续输入拼成一个词，500 毫秒后重来，反复按同一个字母轮换；
  时间由调用方给，不依赖平台的计时器）。平台的按键先换成 `ListKey`，`ListState.Handle` 处理。`ListboxOption` 加了 Icon 与 Group。
  Select、Combobox 以后用同一个状态，焦点在触发按钮或搜索框上，把按键转给列表。
- Listbox：ItemsSource（ListboxOption、字符串，或任意数据加 ToOption）、SelectionMode、Value / Values（同 ToggleGroup）、IsRequired（跟着 Field）、
  IsLoading（防闪烁的骨架行）、Empty、Picked（点击、Enter、空格，再选已选项也发）。选项一行照 itemStyles：控件高度、上下 space.xs、左右 space.sm、
  sm 圆角，高亮 fillHover，已选 accentText 加行尾的勾，不可用半透明；组名 sm、textMuted、medium。没写高度时最高 20em。
  读屏：列表（能否多选、是否必选）+ 列表项（选中、不可用、组名作说明）；桌面没有 aria-activedescendant，高亮项是 WinForms 子节点的 Focused 状态，
  Avalonia、WPF 的选项不可聚焦（焦点留在列表上）。
- **Avalonia**：代码组合的 Control：ScrollArea 里一个 ItemsControl（VirtualizingStackPanel），数据模板按行的种类建组名或 `ListboxItem`，回收复用时按
  DataContext 重画。踩过：测试只加载 RuneTheme，平台的 ItemsControl 没有模板（ListboxParts.axaml 给它一个只有 ItemsPresenter 的模板）；
  `TextBlock.LineHeight` 不往下继承（选项文字的行高在代码里设）；`:disabled` 由平台按 IsEnabled 管，不可用的选项设 IsEnabled = false；
  初始化器里 ItemsSource 可能先于 ToOption 设，进了逻辑树才建选项。
- **WPF**：虚拟化要求 ScrollViewer 在 ItemsControl 自己的模板里：行的容器（`ListboxParts.Rows` 的样式）模板是 ScrollArea（CanContentScroll）
  包 ItemsPresenter；容器是 Decorator，按行的种类放组名（Text，行高系数只对 Text 生效）或 ListboxItem。合成的鼠标事件拿不到位置：选项按"按下过、
  再松开"判断点击。
- **WinForms**：`ListboxControl` 自己画行（`ListboxView` 放在 ScrollAreaControl 里，只画与剪切区相交的行，用 Text、Icon 元素），没有子控件；
  量好每行的高度（有说明的行更高）。不可用的文字、图标混进底色。没有自定义选项内容与部件属性（PLATFORM_PROPS_NOT_PORTED）。
- 示例：multiple、single。上游 multiple 示例的 `height: 16rem` 落在 content-box 上（列表实际高 16rem 加上下 space.xs），桌面上 Height 是整个控件，
  所以矮 7 像素（Avalonia 2.6%，WPF 3.0%）；remote 要等远程选项（load）。

### CodeBlock（2026-11-07）

- 标题栏（surfaceMuted、等宽 0.85em、下边分隔线）、代码区（等宽 0.9em、行高 normal、上下 0.75em 左右 1em，ScrollArea 横向滚动，Wrap 时换行）、
  右上角的复制按钮（有标题时进标题栏）、按行显示（行号 codeComment、强调 fill、新增 codeInserted、删除 codeDeleted，底色铺满整行）、
  超过 MaxLines 折叠（展开按钮 surfaceMuted、sm 字号、箭头展开后翻转）、Flush 去掉外框。代码总是从左到右；关掉字体连字（Cascadia Code 把 => 画成 ⇒）。
- 高亮器：`CodeHighlighter` 委托（代码、语言 → 高亮段），默认 `CodeHighlighting.Builtin`：按正则扫描 TS / JS、C#、XML / XAML / HTML、JSON、CSS、SQL、bash，
  种类到颜色的映射照上游的 Shiki 主题（标点用注释色）。`CodeHighlighting`（分行、行号写法 `ParseLines`、内置高亮器）是孪生文件。异步：先纯文本，
  同一批属性变化合并成一次（投递到界面线程之后再取），慢结果不覆盖新结果，失败时保留纯文本。
- **Avalonia**：代码组合控件（同 Card），部件是子元素（`CodeBlockParts` 名字），颜色在 CodeBlockParts.axaml；em 尺寸在代码里按各层的字号算——Border 没有字号属性，
  主题里的 `{rune:Em}` 当时在它上面不起作用（踩过：内边距、行高全是 0；2026-11-16 起 Em 绑继承的字号，Border 上也能用）。代码用 SelectableTextBlock（可选中）。
- **WPF**：同样组合，部件样式键（`CodeBlockParts`）；FrameworkElement 自己有 Language（界面文字的语言），代码的语言叫 `CodeLanguage`。部件样式 BasedOn 别的组件的
  主题样式时要把那个字典合并进来（StaticResource 只认本字典与合并进来的）。行号的读屏节点自己说不是控件、不是内容（WPF 没有 AccessibilityView）。
  踩过：`Grid` 在 Rune.Wpf 里是组件库的流式网格，平台的要写全名。
- **WinForms**：`CodeBlockControl` 自己画外框与标题栏，代码区是画出来的 `CodeView`（逐段着色的 GDI 文字），放进 ScrollAreaControl；复制按钮是 CopyButtonControl。
  GDI 关不掉 OpenType 特性：装了 Cascadia Mono（同一套字形、不带连字）时换成它。标题栏至少放得下复制按钮。代码不能选中（复制按钮复制全部），没有部件属性。
- 示例：Code 页的 block、options、flush、diff；group、preview 要等 Tabs 与示例卡片（docs-kit）。

### ScrollArea（2026-11-07）

- 浮层滚动条：滚动仍是平台的，滚动条浮在内容上方、出现消失不占位。粗 0.5em + 两侧各 2 像素，滑块 textMuted 半透明（悬停、拖动 75%），至少 20 像素长；
  两个方向时各自让出右下角。`Axis`（y / x / both）换成平台滚动条的 Auto / Disabled；`Type`（hover / scroll / always）、`HideDelay`（600 毫秒）决定显示时机；
  内容超出时可以 Tab 聚焦（`IsViewportFocusable`，上游的 focusable：平台控件自己有 Focusable），键盘聚焦时里侧 2 像素的焦点环。档位类型 `ScrollAreaAxis`、
  `ScrollbarType` 是孪生文件（`Layout/ScrollArea/ScrollAreaVariants.cs`）。
- **Avalonia**：从 ScrollViewer 派生，自己的 ControlTheme：视口占满，两条 ScrollBar 叠在上面（`Rune.ScrollArea.Bar`：Track、透明的翻页按钮、圆条 Thumb）；
  `:active` 伪类时显示并接收指针，淡入淡出是代码里挂的 Transitions（durations.md、standard，系统关闭动画时不挂）。隐藏的等待用 Task.Delay（headless 不走 DispatcherTimer）。
- **WPF**：从 ScrollViewer 派生，Generic.xaml 里的模板同样叠放；轨道、滑块是部件样式（`ScrollAreaParts.Scrollbar`、`Thumb`），翻页、拖动走 ScrollBar 的命令与 Scroll 事件；
  显示状态是只读的 `IsScrollbarVisible`，淡入淡出用 DoubleAnimation。
- **WinForms**：`ScrollAreaControl` 放一个内容控件（`Content`），自己算内容的大小与滚动位置（纵向：宽同区域、高按内容要的；横向反过来）；滚轮、键盘、拖动滑块、
  内容里的控件得到焦点时滚到看得见。滑块是叠在内容上的子控件（Region 圆角，颜色按底色混出半透明的效果），焦点环是四条细子控件——WinForms 的子控件不能半透明、
  画在区域自己身上会被内容盖住。出入：没有淡入淡出；点击轨道的空白处不翻页（空白处是内容）。区域本身透明（卡片里显示卡片的表面色）。
- 示例：vertical、horizontal、types（封面的渐变按上游的 oklch 换算成 sRGB）；both（宽表格、表头与行号固定）等 Table。
- behavior.mjs 多了 `PLATFORM_BEHAVIOR_NOT_APPLICABLE`（只在某个平台不适用的用例）。

### Tooltip（2026-11-07）

- 第一个浮层。上游是包着触发器的组件（trigger 渲染函数），桌面上沿用平台的写法：Avalonia 的 `ToolTip.Tip`、WPF 的 `ToolTip`、WinForms 的扩展组件
  `TooltipProvider`（同平台的 ToolTip 组件，控件多出 Tooltip、TooltipPlacement、TooltipArrow）。组件库补上外观和平台没有的行为；位置是三个平台共用的
  档位类型 `FloatingPlacement`（12 种，`Utils/Floating`，以后 Popover、Menu、Select 也用）。
- 外观：反色底、sm 字号、sm 圆角（em 系数随主题）、内边距 0.25em / 0.5em、最宽 20em，淡入 durations.md、enter。箭头是 0.625em 的方块转 45°，尖角圆角 2，
  跨在朝向触发器的边上、对准触发器中心（离两端至少一个箭头宽）；面板与触发器隔 offset（6）加箭头伸出的 0.7 倍边长。
- **Avalonia**：RuneTheme 给平台的 ToolTip 换了 ControlTheme（应用里所有提示都一样）。平台已有：停留后显示、刚关掉时直接显示（BetweenShowDelay）、
  按下隐藏、指针能移到提示上、同时只一个、ShowOnDisabled。组件库补：有提示的元素用模板优先级写 ShowDelay 500、BetweenShowDelay 300、VerticalOffset 0
  （平台的附加属性注册在 Control 上，改不了 Control 自己的默认值；本地值照样盖过）；Tab / 方向键聚焦立即显示、失焦与 Esc 隐藏；提示文字写进 HelpText
  （模板优先级）。位置在 ToolTipOpening 时按 `rune:Tooltip.Placement` 与书写方向换成 PlacementMode。与触发器的距离做成模板根 `TooltipLayout` 在主轴两侧
  留的透明边：平台弹出层的偏移量翻转时不跟着反向，透明边不受影响。箭头在 ToolTip 的 Loaded 时按提示与触发器在屏幕上的实际位置定边（可能翻转过）。
- **WPF**：RuneTheme 合并 `Overlay/Tooltip/Tooltip.xaml`（平台 ToolTip 的隐式样式，不走 DefaultStyleKey）。平台已有：键盘聚焦显示（按 InitialShowDelay）、
  指针移到提示上不消失（safe area）、文字提示是帮助文字。组件库补：InitialShowDelay 500、BetweenShowDelay 300（覆盖 FrameworkElement 的元数据）、Esc 隐藏、
  12 种位置——`Placement=Custom` 加定位回调（样式里绑定 PlacementTarget，转换器按触发器生成回调），回调给要的那边与对面两个候选，平台挑整个在屏幕里的；
  回调的尺寸是设备像素，按触发器的实际宽度换算。面板与箭头由 `TooltipChrome` 按字号画，有箭头时四周留透明边（弹出窗口外画不出东西）。
  踩过：样式里 `{Binding PlacementTarget.(rune:Tooltip.Arrow)}` 运行时按调用方的程序集解析，找不到类型（PathError）；xmlns 写上程序集又编译不过——改成 TooltipChrome 自己从触发器读。
- **WinForms**：平台的提示窗口是系统画的矩形，画不出圆角与箭头；`LayeredWindow`（`Utils/Floating`，Popover 也用）是不抢焦点的置顶分层窗口，整张预乘 ARGB 位图用 UpdateLayeredWindow 贴上，淡入改整体不透明度。
  文字用 GDI（TextRenderer，同别的元素）画在不透明底上再贴进面板：直接画到透明位图上 GDI 会把文字像素的不透明度写成 0。时机、键盘（消息过滤器记最近的输入是不是键盘，Esc 吃掉）、
  翻转与贴边（与屏幕边隔 8）都是组件库做的；提示文字写进控件的 AccessibleDescription（使用方写了的不动）。提示只能是文字（快捷键写在文字里）。
- 测试：Avalonia 的 headless 弹出层默认放进窗口模板的覆盖层，测试的窗口没有模板——测试平台开 `OverlayPopups = false` 建成独立窗口；headless 送按键时会清掉焦点，Esc 之后的焦点不核对。
  WPF、WinForms 的位置测试用屏幕上的窗口（弹出层会被挪进屏幕）。上游没有 Tooltip 的单元测试，不标 [Upstream]。
- 没做：HoverCard（同一套悬停时机，做浮层时一起）；Popover 页的 hover-card、timing 示例；tooltip 示例里"更多格式"的菜单（等 Menu）。
  WPF 键盘聚焦按平台的 InitialShowDelay 才显示（上游立即）；Avalonia 的离场没有淡出（上游也只有 Chromium 有）。

### Popover（2026-11-07）

- 非模态弹出框：点触发器开关，点外面、Esc 关闭；打开时焦点进第一个能聚焦的元素（没有时到面板上），关上后回到触发器（点外面关的不抢回焦点）。
  `OpenChanged` 带原因（`OpenChangeReason`：Trigger、Outside、Escape、Select、Focus；上游的 onOpenChange，parity 里通用改名），同上游只报告用户的操作：代码里（父级、绑定）改 IsOpen 时不发。
  浮层开着时点触发器：平台先按"点了外面"关掉，紧接着这次点击又会打开——刚关掉 300 毫秒内的触发器点击不再打开。
  面板 surfaceRaised、xs 描边、lg 圆角、lg 阴影、space.md 内边距；位置 `Placement`（默认 bottom-start）、`Offset`（6，有箭头时加箭头伸出的 0.7 倍边长）、`Arrow`。
  读屏：浮层是对话框（上游的 role="dialog"），以触发器为名称。lazy、role 不移植（内容是控件实例；角色固定）。
- **Avalonia**：平台的 Popup（IsLightDismissEnabled，点外面那一下不传给下面）；位置换成 PlacementMode、箭头定位与 Tooltip 共用 `FloatingPopup`（`Utils/Floating`）。
  面板的 em 尺寸（圆角、内边距、箭头）在代码里按字号算，部件样式在 PopoverParts.axaml。
- **WPF**：Popup（StaysOpen=false、透明、Custom 定位回调同 Tooltip），四周留 16 的透明边给阴影与箭头；阴影画在面板后面单独一层（DropShadowEffect 放在面板上
  会让里面的文字走软件渲染）。面板是带读屏节点的 Border 子类（Border 没有 AutomationPeer）。焦点：`MoveFocus(First)` 在 Border 上不往里找，自己在可视树里找第一个
  可聚焦的元素。踩过（又一次）：`new FrameworkPropertyMetadata(null, (d, _) => x.Content = …)` 选中了强制回调的重载，Content 永远是 null。
- **WinForms**：浮层里要放可交互的子控件，整张位图贴到分层窗口的做法不够：两层窗口。底下 `LayeredWindow` 画阴影、面板、描边与箭头（逐像素透明，
  阴影是遮罩 + 三遍盒式模糊近似高斯：`Drawing.Shadow(Bitmap…)`，原来一圈圈的近似在 60 像素的模糊上出条纹；面板以外的点击穿透 HTTRANSPARENT）；
  上面 `FloatingForm` 是普通的无边框窗口，只盖内边距以内（space.md 大于圆角，直角不露出来）。内容窗口激活、拿焦点（宿主窗体标题栏会变成非激活），
  失去激活即"点了外面"。内容不在宿主的 Parent 链上：`ProviderControl.Of` 遇到 FloatingForm 接着从弹出它的控件往外找，作用域变了也通知进去。
  控件本身的大小就是触发器的大小（基线也转给触发器）。没有箭头的部件属性。宿主窗口移动时浮层不跟着走（上游滚动时跟着；桌面上点窗口标题栏就关了）。
- 示例：popover（分享、筛选）；像素只比关着的状态（Avalonia 0.26%，WPF 0.07%，WinForms 1.0%）。

### Tag（2026-11-07）

- 外观同 Badge：三个平台都从 Badge 派生（WinForms 是 `TagControl : BadgeControl`），Badge 多了"文字后面的部件"（Avalonia、WPF 的 protected `Trailing`，
  WinForms 元素的 `TrailingEm`），有它时末端内边距收成 0.25em。上游给了 onRemove 才有移除按钮：桌面上是 `RemoveRequested` 事件，有订阅才放按钮
  （自定义 add / remove 访问器）。移除按钮是圆形 CloseButton：字号与颜色同标签（本地值盖过按钮自己的字号档位与 ghost 的前景色），宽、最小高 1.5em，
  上下外边距 -0.25em；读屏名称"移除 + 文字"，`RemoveLabel` 能改。
- 离场（上游 useDismissible）：淡出、缩到 0.8（durations.sm、exit）后才发 RemoveRequested；布局之后还在界面上就淡入复原。焦点原来在移除按钮上、
  标签被移除了时交给同一容器（最近的 Panel：Flex、项面板）里下一个、没有就上一个能 Tab 聚焦的元素（上游 focusAfterRemoval）。
- **Avalonia**：出现程度是私有属性，动画改 Opacity 与缩放；离场的"结束"用同样时长的 Task.Delay 计（headless 的动画时钟不走，
  `ForceRenderTimerTick` 也推不动），到点停掉动画、留下终值。
- **WPF**：DoubleAnimation（FillBehavior.Stop，先写终值）的 Completed。移除按钮的部件样式 BasedOn CloseButton 的主题样式，Badge.xaml 合并了 Button.xaml。
- **WinForms**：移除按钮是 CloseButtonControl 子控件（四周画焦点环的 Padding 超出标签的部分被裁掉）。子控件不能半透明：没有离场动画，点了就请求移除。
  没有 propsRemove。
- 顺手修的：WinForms 按钮禁用时文字不变淡——GDI 的 TextRenderer 不认颜色里的不透明度，改成把前景色按 disabled 不透明度混进主题的表面色。
- 示例：Badge 页的 tag、tag-removable。剩下的差异主要来自字重：上游字体栈以 `system-ui` 开头（Windows 上的 Chrome 有真正的 500），
  我们的字体 token 从 "Segoe UI" 起，medium 落回常规，文字窄一点、横排的标签一路往后错开（DOCSITE"看过的原因"）。
- 可选中（上游 0f69e5c5，2026-11-08）：`IsSelected`（bool?，Avalonia、WPF 默认双向绑定；WinForms 发 `IsSelectedChanged`）。不是 null 时整个标签是切换按钮（读屏是按钮、按下状态），点击、Enter 按下、空格松开切换，键盘聚焦画 2px 的焦点环；不带移除按钮。没写的色调、字号用 accent、sm，没选中时没写的样式用 surface，选中时一律 solid、前面一个对勾。Avalonia 回退值按模板优先级设，选中的 solid 与对勾按动画优先级（盖过写了的样式与图标，同上游）；WPF 是 Tag 样式里 IsSelected 的触发器（写了的照用）；WinForms 是 Badge 元素内部的 `Selected` 决定取值。示例：Badge 页的 tag-selectable（Avalonia 0.52%–0.79%、WPF 0.51%–0.78%、WinForms 1.37%–1.87%）。

### Alert（2026-11-14）

- 一行排开图标、文字区（标题中等字重、说明是正文色、额外内容）、操作、关闭按钮，间距 space.sm，内边距 space.md，行高 normal，每种样式都留 1 像素的边框位置。
  颜色 = 样式（`AlertVariant`：soft、surface、outline；上游不提供实底）× 色调（默认 info），同 Badge 的色表：Avalonia 是调色板（Resolved.Palette）按模板优先级设到
  Alert 自己的 Background、BorderBrush、Foreground 上；WPF 是 recipe-gen 生成的 AlertRecipe.g.xaml；WinForms 直接用 BadgeRecipe。档位类型 `AlertVariant`、`AlertLive`、
  `BannerVariant` 是孪生文件（`Feedback/Alert/AlertVariants.cs`）。
- 图标不写时按色调选（info / neutral / accent 是 Info，success 是 CircleCheck，warning、danger 是感叹号的三角与圆），写 null 不显示（"写没写"按本地值判断：
  Avalonia 的 IsSet、WPF 的 ReadLocalValue、WinForms 记一个标记）。IsLoading 时图标换成加载指示，读屏的状态文字是"加载中"（WinForms 是 Busy 状态）。
- 说明：Avalonia、WPF 有 Description 与内容（Content），没写 Description 时内容就是说明，两个都写时内容在说明下面（同上游 description / children）；
  WinForms 的说明是画出来的文字，直接加的子控件是说明下面的额外内容（文字里放不了链接）。
- Live（off / polite / assertive，上游的 role status / alert）：Avalonia 是实时区域（AutomationProperties.LiveSetting，内容变了平台发 LiveRegionChanged）；
  WPF 设 LiveSetting 并在内容变了时自己发 LiveRegionChanged；WinForms 发自动化通知（assertive 是 ImportantMostRecent）。读屏的名称是标题。
- 关闭：订阅了 CloseRequested 才有关闭按钮（小一档：sm / md 的字号，上下各收 space.xs），悬停底色是文字色的 14%（状态层）。离场同 Tag：
  三个平台把 Tag 的"离场后请使用方移除、焦点交给相邻元素"挪进了共用的 `Dismissal`（Utils/Motion）；Avalonia、WPF 淡出（Alert 只改透明度），
  WinForms 的子控件不能半透明，直接请使用方移除。
- WPF 的 Control 自己有 Padding、BorderThickness：边框写在主题里，内边距随字号用 SetCurrentValue 写当前值（使用方的本地值、样式盖得过），里面的 Border 绑过去；
  inspect 也按 Alert 自己报。不用 EmSize.Padding：它的登记会覆盖 FontSize 的元数据、换掉字号的继承。代码建的部件要有 `Name = "PART_X"`，inspect 才配得上部件。
  根 Border 的 TextElement.FontSize 绑到控件的 FontSize：文档应用与 inspect 的宿主里，说明的 TextBlock 没从控件继承到字号（测试里继承正常，原因没查清），绑上就稳了。
- 示例：tones、status、variants、banner（Banner 在同一页）。Avalonia、WPF 与文档站的取值对照没有差异。

### Gauge（2026-11-14）

- 几何与读法是孪生文件 `GaugeGeometry`：画布 200 × 108（TS 的 viewBox 0 -4 200 108），圆心 (100, 100)，数值弧半径 80、线宽 14，外圈区间半径 94、线宽 4，
  目标刻度从 70 到 98、线宽 2.5；比例 0 在左、1 在右，经过上方；数值弧的色调是写了的、否则值所在的区间（超出最后一段取最后一段）、没有区间时 accent；
  读屏的取值文字是数值（单位写成后缀）与"目标 …"（Lang.ProgressTarget）。区间是 `GaugeBand(To, Tone)`，值是 decimal。
- 宽 10em，刻度盘高 = 宽 × 108 / 200；轨道 fill，数值弧与区间用色调的实色（中性是 borderStrong，同 StatusDot 的取色），目标刻度正文色；
  中间的数值 1.5em、半粗、行高 1 压在底边上；两端 0.75em、次要色；名称 sm、次要色。读屏是只读的进度条（范围值夹在 Min–Max，取值文字在 Value 里），名称是 Label。
- Avalonia 自绘弧（换主题重画）；WPF 把 200 × 108 的 Canvas 放进 Viewbox，弧是资源引用颜色的 Path（线宽跟着缩，同 SVG）；WinForms 是元素（GaugeControl 宿主），
  用 GDI 的 DrawArc（角度从 x 轴顺时针量，左端 180°），TS 的 size 叫 TextSize，部件属性 PropsValue、PropsLabel。
- 上游把数值弧与中间的数值都标成 data-part="value"（[#6](https://github.com/kenconnet666/vue-tsx-pro/issues/6)），取值对照里这几行是配对错位；我们的部件只有 Value、Label。
- 示例在 Sparkline 页（gauge、gauge-sizes）。

### ProportionBar（2026-11-14）

- 结构同 TS：横条（胶囊，sm 8 像素、md 12 像素，段间空 borderWidths.sm，数量为 0 的段不画）在上，图例在下（space.sm，换行，列间距 lg、行间距 xs，sm 的字号）：
  色点（StatusDot md）、名称、数量（半粗、等宽数字）、补充。段的颜色同 StatusDot 的色调实色（StatusDot 的取色抽成了内部的 BrushKey / ToneColor）或任意颜色；
  没写色调的按 accent、info、success、warning、danger、neutral 的顺序取（孪生文件 ProportionBarVariants 里的 Proportions，连同读屏名称的拼法）。
- TS 的 aria-label 叫 Label；段是 `ProportionSegment` 记录（Avalonia、WPF 的 Color 是画刷，WinForms 是 Color）；ValueText、LegendExtra 是函数，Avalonia、WPF 可以返回控件，
  WinForms 返回文字（补充画成次要色、xs）；WinForms 的 size 叫 BarSize（不是字号档位）。
- 横条读屏是一张图，名称带上各段的数量。Avalonia 渲染时查色调资源、换主题时重画；WPF 的每段是资源引用的 Border（换主题跟着变）；WinForms 全是画的，读屏的子项是横条与图例各项。
- 示例在 Sparkline 页（proportion-bar），取值对照与文档站一致。

### Sparkline（2026-11-14）

- 几何是孪生文件 `SparklineGeometry`（Display/Sparkline）：横坐标按位置（折线 0 到 n-1、柱每个占一格），纵坐标 0（上）到 100（下），空值处断开，柱从 0 起、0.7 格宽，
  最后一个值的位置；读屏的一句话也在这里（Lang.SparklineSummary，值按 NumericFormat 格式化，Label 在前，没有值时是 Lang.EmptyValue）。
  各平台按控件的宽高把这套坐标拉伸着画（TS 的 preserveAspectRatio="none"），线宽不随拉伸变（vector-effect: non-scaling-stroke）。
- 颜色是色调的实底（ToneFill，默认 accent）：折线 1.5 像素、圆头；面积 15%；柱 85%，标出最后一个值时前面 40%、最后一根实色；最后一个点 6 像素，外面一圈表面色。
  高度默认 2em（控件自己的 Height 盖过它；TS 的 height 是 CSS 长度）；数据是 `IReadOnlyList<decimal?>`，纵向范围 Domain 是 (double? Min, double? Max)。
- WinForms：Sparkline 是元素、SparklineControl 是单元素宿主；控件画不到自己外面，标出最后一个点时结束边与上下各让出点的半径加一圈（TS 的点有一半伸到图外）。
- 示例：Sparkline 页的 cards（in-table 2026-11-16 随 ColumnTable 补上，仅 Avalonia）与 Stat 页的 live（每 2 秒推一个新值）。

### Stat、StatGroup（2026-11-14）

- 结构同 TS：第一行是图标框（1.75em、accentSoft 底，按 sm 的字号）与指标名（sm、次要色）、结束边是变化量（xs：箭头、空格宽、带符号的百分比、说明），放不下时变化量换到下一行；
  第二行是数值（xxl、粗体、行高 tight，AnimatedNumber）与单位（xs、次要色，按基线对齐）；再下面是说明、内容（Content）、底栏（上方一条 borderMuted 的线），行间 space.xs。
  TS 的 value 是数字或字符串：这里数字是 Value（decimal），字符串是 Text。变化量 Change 是 decimal?（不写没有），色调按方向与 InvertChange（持平是 muted）。
- 读屏顺序：Avalonia、WPF 用私有的 StatLayout 面板，子元素按 TS 的 DOM 顺序（指标名、数值、变化量……），排版时把变化量放到第一行的结束边（TS 用 CSS order）；
  读屏里是一组，名称是指标名（指标名一行不另念），加载中状态是"加载中"。WinForms 的名称是指标名、描述是数值、单位与变化量，加载中带 Busy。
- 加载：骨架防闪烁（DelayedLoading），出现过骨架之后换回数值时数值与变化量淡入（同 Card）；WinForms 的子控件不能半透明，直接互换。
- StatGroup 是 Grid（MinItemWidth 9rem、行间距 md、没有栏间距）：组里的 Stat 左右各空 md、起始边一条竖线；Avalonia、WPF 照 TS 往起始方向挪一条线宽、组裁掉每行第一个的线，
  WPF 的竖线与内边距写在 Stat 自己的 BorderThickness、Padding 上（同 Result），WinForms 不在一行开头时才画线。
- WinForms：指标名、图标、单位、说明、字符串值是画的元素，数字与变化量是 AnimatedNumberControl 子控件（它实现了 IBaseline，单位按它的基线对齐），内容、底栏是使用方的控件。
- 示例：basic、change、group、loading、live。取值对照：数值一行比文档站高约 4 像素（数字与单位按基线对齐时，桌面的行高分配与 CSS 不同）。

### AnimatedNumber、NumericFormat（2026-11-14）

- `NumericFormat`（Utils/Data，孪生文件）是 TS 的 NumericFormat：Intl.NumberFormat 的 style（普通、百分比、金额）、最少 / 最多小数位、signDisplay、千位分隔，
  加上 Scale（先除以它）与 Suffix（不换行的空格接单位）。值是 decimal：TS 的 number、bigint、Decimal 在 C# 里都放得进 decimal，十进制精确。
  照 Intl 的规则：取舍四舍五入远离 0；小数位默认普通数字 0–3、百分比 0、金额按币种（大多 2，日元、韩元 0），只写一头时另一头收拢；
  百分号紧跟数字，币种符号在前（本地区的币种用文化的符号，别的币种用 US$、CN¥ 这类写法，不认识的写代码），负号在最前面。语言按 Lang.Code 取 CultureInfo。
- AnimatedNumber：值是 decimal，过渡的中间帧按 double 算、按终值的小数位（decimal.Scale）取整，停下时按原值格式化。时长默认 durations.lg、缓动 standard
  （TS 的 duration 档位在这里是 TimeSpan?，不写取主题）；From 给了时挂上（WinForms 第一次画出来）就从它滚过去。Trend 按正负设色调（TS 的 data-trend 是 Direction）。
  读屏的名称是前缀、终值与后缀（TS 是隐藏的终值文字与 aria-hidden 的滚动数字），propsLabel 不移植。
  Avalonia 用 Animation 驱动私有的 Current 属性，WPF 用 DoubleAnimation（FillBehavior.Stop），WinForms 用 Animator 补间 Signal、在绘制里取当前值；
  WinForms 的前后缀是画的文字（PropsPrefix、PropsValue、PropsSuffix），前面的图标写 PrefixIcon，宽度按终值的文字量（等宽数字，滚动时不跳）。
- 示例在 Stat 页（animated-number）；Stat、StatGroup 接着做。

### Descriptions、ValueChange（2026-11-14）

- Descriptions：项是 `DescriptionItem`（Label、Value、Span、Copyable、CopyText；TS 的 items 数组），Avalonia、WPF 的 Items 是内容属性，XAML 里直接写项。
  栏数 `DescriptionsColumns`（数字或 auto，孪生文件 DescriptionsVariants，含排列 `DescriptionsLayout`）：固定栏数时列表窄于 32em 排成一列（TS 的容器查询），
  auto 时 floor(宽度 / MinItemWidth)，在 1 到 MaxColumns 之间；跨列按算出的栏数封顶。行内排法不按栏分。
- Avalonia、WPF：列表是 Grid（栏间距 xl、行间距 md；边框样式没有间距）或换行的 Flex（行内），每项是 [标签][值]：横排按基线对齐、值占满剩下的宽度，竖排上下 xs。
  边框样式同 TS：每格画上、起始边的线并往回挪一条线宽，外框由列表画（Avalonia 按圆角裁，WPF 在 ArrangeOverride 里设圆角的 Clip）。
  读屏里每项是一组，名称是标签（标签不再单独念）。值是文字时放进 Text（会换行），控件原样放；重建时先把使用方的控件从旧的格子里拆下来。
  里面的链接悬停才有下划线（Link.SetUnderlineScope）。
- WinForms：标题、标签、文字的值是画的元素（PropsTitle、PropsLabel），控件的值与复制按钮是子控件；TS 的 layout 叫 ItemLayout（Control 自己有 Layout 事件）。
  横排时按基线对齐：值是控件时用 FieldControl.FirstBaseline 找控件里第一行文字的基线。边框样式里每格画上、起始边的分隔线（不在第一行、第一列时），
  外框由列表画，碰到圆角的格子按圆角裁——裁剪从 CardControl 抽成了共用的 `CornerClip`（Elements）。格子不写 Font，跟着控件的字体。
- ValueChange：原值次要色、删除线，箭头次要色、竖直居中，新值正文色、中等字重，间距 xs，放不下换行。读屏念"原值 改为 新值"（语言包的 FieldCompareTo；
  TS 是 del、ins 与给读屏的隐藏文字），没有原值时只念新值。WinForms 是画的（PropsBefore、PropsArrow、PropsAfter；Text 元素加了 Strikethrough）。
  示例在 FieldCompare 页（value-change，FieldCompare 本身还没做）。
- 取值对照：Descriptions 的 basic、layouts 与文档站一致；auto 示例 TS 用可以拖宽的区域，桌面用窗口宽度，宽区域的高度不同。

### Result（2026-11-14）

- 结构同 TS：一列居中的 [图标（4em、strokeWidths.sm、色调的颜色，下面多空 space.sm）、状态码（2.5em 粗体、次要色、等宽数字、行高 1）、标题（半粗、xl / md、行高 tight）、
  说明（最宽 36em、次要色）、内容、操作（上面多空 space.md）]，项之间 space.sm，上下 space.xl、左右 space.lg。示例在 Empty 页（result、status）。
- 色调用 StatusTone（默认 success；上游只列了 success、danger、warning、info 四种，其余照样能用），图标按色调选时同 Alert 的 IconFor；
  `ResultStatus`（403、404、500，孪生文件）给出图标（ShieldX、FileQuestionMark、ServerCrash）、色调与语言包的标题、说明（Lang.ResultStatus），显式写的优先。
- 状态码对读屏是重复的：Avalonia 设 AccessibilityView.Raw，WPF 的 Result 自己的 peer 从子元素里去掉它，WinForms 的是画的、读屏里本来就没有（读屏的名称是标题、描述是说明）。
- WPF 的 TextBlock 没有字距，状态码不收紧字距（tight）；内边距同 Alert 写成 Result 自己的 Padding（SetCurrentValue）。
- WinForms：图标、状态码、标题、说明是画的元素（PropsIcon、PropsCode、PropsTitle、PropsDescription），直接加的子控件是内容，Action 是操作；GDI 的行高是字体自己的，
  状态码只占 2.5em 高、字形在里面竖直居中。图标只在色调、状态、图标、PropsIcon 变了时重建，描线只在第一次画出来之前或 Draw 由 false 变为 true 时播，改文字不重画。
  inspect 的截图先画一遍再等动画时钟停下（Animator.IsRunning），取动画播完的样子。
- 取值对照：Avalonia、WPF 只差内容里的行内代码（桌面要放进一段文字里）带来的约 2 像素高度；按钮的差异是工具把切换项当按钮配对的错位。

### Banner（2026-11-14）

- 结构同 TS：一行居中的 Flex（换行，间距 space.sm、行距 space.xs，上下 space.sm、左右 space.lg）里排图标、文字、操作（中等字重）；文字是一段：
  标题（中等字重）后跟 0.5em 的空隙再接正文，放不下时在文字里换行。关闭按钮不参与居中，叠在结束边（space.sm）竖直居中，有它时结束端多留 2em。
  没有圆角，留 1 像素的透明边框（同上游的 statusToneSx）。颜色 = 样式（`BannerVariant`：soft 默认、solid 实底）× 色调（默认 info），同 Badge 的色表；
  图标的默认值、Live、关闭与离场同 Alert（共用 Alert 的 IconFor 与 `Dismissal`）。
- Avalonia、WPF：TextBlock 的 Inlines 是 Run（标题，PART_Title）+ InlineUIContainer 空隙 + Run（字符串内容）或 InlineUIContainer（控件内容），文字整段居中、跟着换行；
  WPF 的关闭按钮用负的外边距伸到边框外的内边距里（Banner 自己的 Padding 结束端多了 2em）。
- WinForms：图标、标题、正文是画的元素，操作与关闭按钮是子控件；标题与正文一行放得下时并排，放不下时标题在上、正文在下各自居中
  （GDI 画不了一段里混排两种字重的折行）。实底上的操作要跟着横幅的文字色：BannerControl 的 ForeColor 设成文字色，色调写 inherit 的链接随之变色。
- 卡片里贴边放横幅（内边距 none）时，WinForms 的子控件会盖住卡片的圆角：CardControl 给碰到圆角的子控件按边框里面的圆角设窗口区域（TS 的 overflow: hidden），
  只动自己裁过的；inspect 的截图改用 WM_PRINT（不带 PRF_CHILDREN）逐层画，按每层的区域裁，和窗口系统的裁剪一致。
- 示例：Alert 页的 banner（实底维护通知带链接、试用提醒带按钮、只读提示，关掉的可以重新显示）。三个平台各有两条上游用例。

### Select、SelectedTags（2026-11-07）

- 下拉选择（select-only combobox）：外壳同输入框（Avalonia、WPF 照 Rune.TextInput 写了一份 Select 的主题；WinForms 抽出 `ControlShell`，TextInputControl 与 SelectControl 共用），
  触发框就是控件自己，焦点一直在它上面（可搜索时进搜索框）。弹出层贴着外壳下方、至少与它同宽（放不下时翻到上方），面板同 Popover（surfaceRaised、描边、lg 圆角、lg 阴影），
  没有内边距；里面是不可聚焦的 Listbox（Select 把按键转给它的 `ListState`），可搜索、可新建时顶部一行搜索框（下边 borderMuted）。
- 值：单选 `Value`（空字符串算没有选中，清空写回 null）、多选 `Values`（清空写回空列表）；已选项的文字取自全部数据（被过滤掉的也在），按数据建一次值 → 文字的表
  （上游的性能用例：三万个全选在 1 秒内挂载）。多选的已选项是外壳里一排 `SelectedTags`（sm、soft 的 Tag，移除按钮不能聚焦、按下不抢焦点；读屏看不到这一排，
  下拉的值是全部已选）；`MaxTagCount` 是 `TagLimit`（数字或 responsive：一行按宽度放，其余收进"+N"）。
- 搜索与新建：孪生文件 `SelectSearch`（忽略大小写与重音的匹配、过滤、"新建"项 `SelectCreation`、`TagLimit` 与它的 XAML 转换器）。新建报告 `OptionCreated`
  （WinForms 的 Control 自己有 Created）。打开时清空搜索词、高亮已选项；输入搜索词高亮第一个匹配的。
- 开关的原因：`OpenChangeReason` 加了 Select（单选选中后关闭）、Focus（Tab 离开）；同上游只报告用户的操作，代码里改 IsOpen 不报告（Popover 一起改了）。
- 初始化器里 ItemsSource 可能先于 ToOption、Values 先于 MaxTagCount 设：WPF 等加载、WinForms 等句柄建好再把数据交给列表、再建标签（Avalonia 的列表进了逻辑树才建，
  标签那一排在模板里）；取已选文字时认不出的数据先跳过。
- **Avalonia**：TemplatedControl，弹出层是平台 Popup（点外面关、点在外壳上那一下不再打开），弹出层里的事件沿宿主冒泡上来，按下、按键、文字输入要排除。
- **WPF**：Control，Popup + Custom 定位回调、阴影单独一层；列表的值用绑定到私有属性接回来（DependencyPropertyDescriptor 会泄漏）；EmSize.Register 已经覆盖了 FontSize 的元数据，
  字号变化在 OnPropertyChanged 里接。框内的小按钮（清空、显示密码）收成 1.5em（`TextInputBase.FitInnerButton`，TextField 原来是完整的控件高度）。
- **WinForms**：`SelectControl : FlexControl`；弹出层用 `FloatingSurface`（Popover 的两层窗口抽出来共用）的不激活模式：内容窗口 WS_EX_NOACTIVATE，点外面由消息过滤器发现，
  宿主失去激活、移动时也关；可搜索时切到激活模式（搜索框要键盘）。内容窗口没有内边距：按面板的圆角裁成圆角（Region）。列表 `Embedded`（不能聚焦）。
  踩过：ListboxView 画的时候给元素换内容会回调失效，无限重画（屏幕外的测试窗口收不到 WM_PAINT，一直没发现）——绘制期间不再响应元素的失效通知；
  Values 先于 MaxTagCount 设时一下子建了三万个标签控件、句柄耗尽。框内小按钮同样收成 1.5em（`ControlShell.FitInnerButton`）。没有自定义选项内容。
- **远程选项（2026-11-14）**：`Load`（TS 的 load）是 `OptionsLoader(搜索词, 第几页, CancellationToken) → Task<OptionsPage>`，`OptionsPage(Items, Next)`，不分页时 `OptionsPage.All(数据)`。
  状态在孪生文件 `RemoteOptions`（Input/Select）：弹出层打开时立即按当前搜索词请求，搜索词停下 300 毫秒再请求，新的请求取消旧的、只采用最后一次；关上时取消；
  第一次结果之前用 ItemsSource（编辑页放上已选的那一项）。有 Load 时搜索交给服务端，不在本地过滤。分页：列表离末尾不到 5 行（按行高折算滚动余量）时带着 Next 取下一页，
  接在后面、按值去重，骨架少两行、接在行下面（Listbox 的 IsLoadingMore、Reached，内部的）。每次换一个新的列表：列表按引用判断数据变没变。
  失败时列表的空状态是语言包的"加载失败，请重试"（上游有错误码文案时用它，我们不内置请求客户端，没有错误码）。已选项的文字记着见过的选项，搜索换掉了结果触发框照样有字。
  继续执行回到界面的同步上下文（三个平台都有），所以状态机本身不碰平台类型。
- 没做：renderValue（触发框是控件自己）、表单的 name、placement。
- 示例：basic、tags、custom、searchable、paged、appearance（Avalonia 0.12%–1.55%，WPF 0.10%–1.53%，WinForms 4%–5.8%）。

### Dialog（2026-11-07）

- 模态对话框（上游 Dialog 与内核 ModalCore）：标题行（H2 lg、关闭按钮）、说明（muted，读屏的帮助文字）、正文（ScrollArea，内容长时只滚正文）、页脚（Footer，
  靠结束边；是布局面板时占满一行；写了 Confirm 时后面是"取消 / 确认"）。面板 surfaceRaised、xs 描边、xl 圆角、xl 阴影，宽度档位 `DialogSize`（容器档位加 full，默认 xs），
  不超出窗口、四周至少 1em；遮罩 overlay。确认是 `Func<Task<bool>>`（进行中加载、关不掉；false 或失败保持打开），`BeforeClose` 拿到原因、false 时不关。
  `OpenChangeReason` 加了 Close、Confirm。焦点：同原生 dialog 按顺序第一个能聚焦的元素（有关闭按钮时就是它），Tab 在面板里循环，关上后还给打开前的元素。
- 对话框写在哪里都行：自己不可见、不参与排版（同关着的原生 dialog 是 display: none；不然放在 Flex 里多算一个间距），打开时用一个铺满窗口客户区的弹出层放遮罩与居中的面板
  （窗口变大小时跟着）。没有用窗口的覆盖层：headless 测试的窗口没有模板，也就没有 OverlayLayer。
- **WPF**：同样是铺满窗口客户区的 Popup（Center 定位到窗口的根元素，窗口变大小时重新定位），阴影单独一层；面板是带读屏节点（窗口类型、本地化"对话框"）的 Border 子类。
- **WinForms**：`DialogControl`（宽度档位叫 `DialogSize`：控件自己有 Size）。遮罩是铺满宿主窗体客户区的半透明分层窗口（点它即点了遮罩），面板用 `FloatingSurface`
  的激活模式（加了 xl 档：xl 圆角、xl 阴影），内容窗口按圆角裁掉；打开期间用 Win32 的 EnableWindow 禁用宿主（同 ShowDialog；改 WinForms 的 Enabled 会让里面的控件按不可用重画），
  宿主移动、变大小时跟着。不接 FloatingSurface 的"点了外面"（切到别的程序也会触发），只认遮罩上的点击。踩过：窗口显示之前子控件的 Visible 一律是 false、
  输入框没建句柄时量出的高度偏小——有没有说明、页脚按属性判断，显示之后再量一次高度；ScrollAreaControl 不认 Padding，正文的内边距放在里面一层。
- 可以拖动（上游 237f97d2，2026-11-08）：`Draggable` 时标题行是把手（移动指针），从标题行上的按钮、输入框按下不拖；位移在排版时加到面板上、限制在窗口里，
  每次打开回到正中。Avalonia 用指针捕获，WPF 用鼠标捕获（测试里鼠标位置造不出来，直接调内部的 `DragTo`）；WinForms 的面板是单独的窗口，按住标题或标题行的空白，
  按屏幕上的光标位移重新摆面板。示例：draggable（付款信息上游用 Descriptions，还没有，先排成标签、值两列）。
- **命令式打开与 AlertDialog（2026-11-14）**：上游的 useDialog 是在 setup 里拿到 open(options) → 句柄；桌面上就是对话框实例自己：`await new Dialog { … }.ShowAsync(owner)`
  在 owner 所在的窗口打开，得到关上的原因；开着时改属性就是句柄的 update，`Close()` 是 close（原因 imperative，不问 BeforeClose）。owner 离开界面时跟着关上。
  不在界面里的对话框要借 owner 的上下文：Avalonia 把 owner 设成逻辑父元素（ISetLogicalParent，owner 的 LogicalChildren 里没有它，同平台 Popup 挂 PopupRoot 的做法），
  主题、Provider、继承的字号都沿逻辑树来；WPF 挂到 owner 所在窗口的装饰层（AdornerLayer，在可视树里），owner 一路上的资源字典按由远到近合并进来、继承的语言包与工作日历照抄
  （装饰层是窗口内容的兄弟，不是 owner 的祖先）；WinForms 的作用域查找经内部接口 `IAnchored` 接着从 owner 找，字体借 owner 的。关上后都还回去。
  AlertDialog（WinForms 的 AlertDialogControl）是 Dialog 的子类：没有关闭按钮、点遮罩不关、宽度默认 xs、没写 Confirm 也有确认按钮，打开时焦点在取消上（Dialog 开放了
  一个内部钩子：先聚焦哪个元素）。`AlertDialog.ConfirmAsync(owner, ConfirmOptions)` 是 useConfirm，得到是否确认；带 `ConfirmReason` 的重载多一个填理由的 Textarea
  （焦点先进输入框，必填没填时字段报语言包的"必填"、不关），得到去掉首尾空白的理由，取消时 null。ConfirmOptions.Confirm 拿到理由，结果为 false 或失败时保持打开。
  读屏：上游是 role="alertdialog"，桌面的读屏没有这一类型，仍是对话框。
- 没做：进出场动画。
- behavior.mjs：describe 不是纯组件名（'Dialog 的 onOpenChange'）时，用例名不以组件名开头的归到 describe 开头的组件名（原来退回文件名）。这样多归出 19 条已做组件的上游用例
  （Badge、Button、Card、Icon 等的属性落点、Provider 默认值），原来被算成"组件还没做"，待逐条对应或登记不适用。
- 示例：dialog（编辑资料，确认时校验与等待）、alert-dialog（删除确认、ConfirmAsync、填理由）、long-content（宽度档位、长正文、占满一行的页脚）、imperative（发票列表里命令式打开，记下关闭的原因）、blocking（导入进度，不能误关）、draggable。

### CommandPalette（2026-11-07）

- 快捷键或触发按钮打开的搜索对话框：sm 宽、停在窗口上部 12% 的 Dialog（Dialog 加了三个内部开关：标题行只给读屏 `HeaderHidden`、`TopFraction`、正文不留内边距
  `BodyFlush`），里面一行搜索框（lg 字号、上下 space.sm、左右 space.md、下边一条 border）与不可聚焦的 Listbox（最高 min(24rem, 窗口高度一半)：Listbox 加了内部的
  `ScrollMaxHeight` 换掉自己滚动时的 20em）。焦点一直在搜索框，↑↓、Page Up / Down 移动高亮，Enter 执行；Home / End、空格留给输入。面板不记选择：
  执行就是选中，`ItemInvoked` 报告那一项、面板关闭。默认按文字与说明过滤（`SelectSearch.Matches`），远程搜索时 Filter 一律返回 true、读 `InputText` 取数据、给 IsLoading。
  每次打开清空输入；输入、结果变了高亮第一个。搜索框是"只要输入、不要外壳"的 TextField（内部的 `Bare`：没有边框、底色、左右内边距与焦点环，高度照旧）。
- 快捷键：孪生文件 `Hotkey`（解析 Mod+K 这类写法，Mod 在 Windows 上是 Ctrl；显示 Ctrl+K、读屏 Control+K）。Avalonia 在 TopLevel、WPF 在 Window 上隧道接 KeyDown，
  WinForms 用应用的消息过滤器（焦点在所在窗体或面板里时），所以输入框里也有效。打开的原因是新加的 `OpenChangeReason.Imperative`，执行是 Select。
- 默认触发按钮画成输入框外壳：surface、neutral 的按钮，放大镜、占位文字（textMuted，占满）、快捷键的 Kbd，至少 12rem；名称是占位文字，快捷键给读屏的快捷键属性。
  `Trigger` 换成自己的控件（里面按钮的 Click 打开，同 Popover），null 时只用快捷键。窄屏只留图标的断点不做。
- **Avalonia**：列表在弹出层里，进了逻辑树才建选项：打开之后（Loaded 优先级）再高亮第一个。
- **WPF**：快捷键的修饰键状态造不出来，测试直接调内部的 `OnHotkey`。默认触发按钮的 textMuted 是本地值（按钮配方里样式 × 色调的触发器会盖过部件样式的 Setter）。
  Inspect 工具补了 DispatcherSynchronizationContext（同应用里的 Application），异步取数据的示例 await 之后才回得到界面线程。
- **WinForms**：`CommandPaletteControl`；Button 元素加了内部的 `Trailing`（贴结束边的 Kbd，有它时内容靠开始边）与 `MutedForeground`，ButtonControl 加了读屏的
  `KeyboardShortcut`。DialogControl 加了 `Relayout`：过滤后只重新摆面板，不重画遮罩。选项是画出来的，没有 ItemTemplate；没有匹配时的说明是 `EmptyText`；
  InputText 变了发 `InputTextChanged`。顺带修了 ElementControl 的 GetPreferredSize 不认 MinimumSize（平台 Control 本来会认）。
- 不转交对话框的属性（上游没声明的属性落到 Dialog 上）：面板里的对话框是固定的一种。
- 示例：commands、remote、custom（Avalonia 0.02%–0.50%，WPF 0.03%–0.46%，WinForms 0.14%–0.97%；commands 少了"macOS 上 ⌘+J"半句）。

### Spinner

- 第一个带部件的组件（`propsIcon`），三个平台按 §7 的写法各落地一次：
  - **Avalonia**：TemplatedControl，模板里只有 `rune:Icon#PART_Icon`（`SpinnerParts.Icon` 是部件名常量）。图标、从 Spinner 转来的大小（`{Binding $parent[rune:Spinner].Size}`）、旋转动画都写在 ControlTheme 的嵌套样式 `^ /template/ rune|Icon#PART_Icon` 里，使用方的 `/template/` 样式逐个属性盖过它（测试覆盖）。旋转用样式动画（`RotateTransform.Angle`，Avalonia 自动补上 RenderTransform）。Avalonia 还没有"系统关闭动画"的开关，暂时不减速。
  - **WPF**：Control，模板里的图标套部件样式键 `SpinnerParts.Icon`（ComponentResourceKey）；主题的部件样式给图标与大小（`{Binding Size, RelativeSource={RelativeSource TemplatedParent}}`），Provider.Styles 里同键的样式叠在上面（测试覆盖）。旋转在 OnApplyTemplate 里开（DoubleAnimation 无限循环），系统关闭动画时转慢一半（1.6 秒一圈，同 TS 的 _reducedMotion）。RenderTargetBitmap 截到的是动画的第一帧。
  - **WinForms**：元素 `Spinner` 里有一个 Icon 元素；部件属性记录 `IconProps`（字段可空），Spinner 给的在下、`PropsIcon` 在上逐字段合并（`Merge`），合并结果套到新建的 Icon 上（没写的字段回到 Icon 自己的默认）。记录先手写，第三个用到部件记录的组件出现时再做源代码生成器。
- **逐帧的时钟（WinForms）**：一直在动的东西读 `FrameClock.Shared.Now`，订阅只管一帧（`ReactiveSource.NotifySubscribersOnce`）：不再重画的（隐藏、离屏）自然退出，没人订阅时定时器停下（测试覆盖）。Animator 的补间用于"从 A 到 B"，FrameClock 用于"一直转"。
- **无障碍**：有 Label 时是进度条（不确定进度），名字是 Label；没有时 Avalonia、WPF 不进控件树，WinForms 是没有名字的 Animation。TS 是 role="status"，桌面上没有对应的"状态"角色，用进度条（读屏会念"忙"）。peer 的 *Core 方法里不能再调公开的 GetName（WPF 报递归调用），直接读 Label。

### Badge

- 第一个自绘、带配方（样式 × 色调 24 条组合）与默认值（`useDefaults("Badge")`）的组件。新的档位类型：`StatusVariant`（solid、soft、surface、outline）、`StatusTone`（六种状态色调）、`Radius`（none–full），三个库都有。
- 颜色与按钮同一张调色板表（`tools/token-gen/palette.mjs`，vue-tsx-pro 0.11 的 `statusToneCompound`）：非中性色调按 `{色调}`、`{色调}Contrast`、`{色调}Soft`、`{色调}Text`、`{色调}Border` 的规律取 token，中性用反色、填充、表面等另一组。Avalonia 取调色板对象的字段（代码里按样式档位选），WPF 是 recipe-gen 生成的 `BadgeRecipe.g.xaml`（24 条 MultiTrigger，主题样式 BasedOn 它），WinForms 是生成的 `BadgeRecipe.Colors`。
- 自绘（DESIGN §9），部件按 ROADMAP 决定 20：底色、描边、圆角自己画；图标、圆点、文字是代码里组合的子元素。
  - **Avalonia**：子元素加进 LogicalChildren、VisualChildren；部件名 `PART_Icon`、`PART_Dot`（`BadgeParts`），样式用子选择器 `rune|Badge > rune|Icon#PART_Icon`（测试覆盖）。
  - **WPF**：AddVisualChild、AddLogicalChild，覆盖 VisualChildrenCount、GetVisualChild、LogicalChildren；图标、圆点在代码里 `SetResourceReference(StyleProperty, BadgeParts.Icon)`，Provider 的部件样式照常叠上去（测试覆盖）。Control 的 Background、BorderBrush 元数据补上 AffectsRender。
  - **WinForms**：元素里组合 Icon、Text 元素，画的时候换成 Badge 的环境（`context with { Font, ForeColor }`）；图标部件 `PropsIcon`（IconProps），圆点是画的，`PropsDot`（`DotProps`：颜色、大小）。
- 颜色是受管的（档位决定、本地值），换颜色走档位、Provider 的默认值或主题 token；默认值（soft、neutral、xs、sm）在主题里（Avalonia、WPF）或元素的默认（WinForms），Provider 的样式能改。
- 尺寸都是 em，按 Badge 自己的字号算：内边距 0.5em、间距 0.25em、最小高度 1.75em、圆点 0.5em；圆角取主题的 radii 系数，full 按高度的一半画成胶囊；描边 1 像素，没有描边色时也占位，各样式等高。
- 读屏：Badge 本身不是控件元素，读屏直接读里面的文字（Avalonia、WPF）；WinForms 是静态文字，名字是 Text。
- 踩过：WPF 里使用方的隐式 `rune:Text` 样式也会套到 Badge 里面的 Text 上（示例里改成带键的样式）。Avalonia 的子选择器 `WrapPanel > rune|Text` 只选直接子元素，不受影响。

### Button 家族（ButtonBase、Button、IconButton）

- **分层照 TS 的 ButtonBase 与 button-core**（`Base/Button/` 一个文件夹）：
  - `ButtonBase`（Avalonia、WPF）：没有外观的按钮，波纹、焦点环、颜色过渡、加载中拦下点击；菜单项、标签页这类"长得不像按钮的按钮"以后建在它上面。模板里的 `PART_Ripples`、`PART_FocusRing` 按名字找到，换模板时照着放。
  - `ButtonCore`（抽象）：外观（样式、色调、字号档位、圆角、图标、调色板）与加载指示防闪烁。Avalonia 的共用主题 `Rune.ButtonCore`（BasedOn ButtonBase 的主题）、WPF 的生成配方样式（TargetType 是 ButtonCore）放调色板、样式档位与状态，Button、IconButton 的主题 BasedOn 它，只写自己的默认值与模板。
  - `Button`：文字、后置图标、占满宽度；`IconButton`：方形（宽度取控件高度，左右内边距 0），默认 ghost、中性色，读屏名称写 `AutomationProperties.Name`（WinForms 是 AccessibleName；parity 把 TS 的 aria-label 对到它）。
  - WinForms：元素 `ButtonCore` 画外框、波纹、加载指示、焦点环并算内边距，子类只排内容（`MeasureContent`、`PaintContent`）；宿主的输入、IButtonControl、无障碍在 `ButtonControlBase<T>`，`ButtonControl`、`IconButtonControl` 只加自己的属性。WinForms 的 ButtonBase 等多元素宿主（任意内容）再做。
  - Avalonia 的 BasedOn 主题：派生主题的普通 setter 排在基主题的嵌套样式之后，会盖过基主题 `^:link` 这类状态样式；所以共有的默认值都写在 `Rune.ButtonCore`，派生主题只写不与状态样式冲突的（IconButton 的左右内边距本来就是 0）。
- 外观 = 样式（solid、soft、surface、outline、ghost、link，`ButtonVariant`）× 色调（`StatusTone`）× 字号档位 × 圆角；内部尺寸都是 em：内边距 1em × 0.25em、最小高度取控件高度、图标与文字间距 0.5em、圆角取主题的 radii 系数。链接样式只去掉左右内边距与最小高度，上下 0.25em 保留（同 TS 的配方）。
- 控件高度（vue-tsx-pro 0.11 的 `sizes.control`）：`max(round(系数 × 字号 ÷ 4) × 4, 24)`，xs–xl 依次 24 / 28 / 32 / 36 / 40；系数在 xs 档是 2、其余档 2.25。每个平台一处 `ControlHeight`：Avalonia、WPF 的系数是继承的附加属性（写了字号档位的组件设它，里面没写档位的控件跟着外面，同 TS 里 CSS 变量的继承），Avalonia 写 `{rune:ControlHeight}`，WPF 写 `rune:ControlHeight.MinHeight` / `Width`（经 EmSize 的 coerce）；WinForms 没有继承的作用域，按组件自己的档位取。常量与 token 由测试核对，上游改了 token 测试会失败。
- **颜色都从一张调色板表来**（`tools/token-gen/palette.mjs`：每种色调九个字段，中性与其他色调的不规则收在表里）：
  - Avalonia：token-gen 生成每套主题六份 `TonePalette`（`Rune.Palette.Accent`……）；色调伪类设 Palette，6 条样式档位绑定字段，加 5 条悬停（决定 11），36 种组合只写 17 条嵌套样式。
  - WPF：`tools/recipe-gen` 把配方表展开成主题样式的 MultiTrigger（36 条组合 + 30 条悬停，ButtonRecipe.g.xaml），Button 的主题样式 BasedOn 它；使用方任何 setter 都能盖过。
  - WinForms：recipe-gen 同时生成 `ButtonRecipe.Colors(样式, 色调, 悬停, 主题颜色)`。
  - 中性实底的悬停色在 TS 里是 `color-mix(in oklab, inverseSurface 85%, background)`，token-gen 按主题在 oklab 里算好，作为派生 token `inverseSurfaceHover` 补进 colors 组。
- **效果照 TS**：
  - 波纹（`Ripple`）：从按下的位置扩散（键盘 Enter、空格从中心），半径到最远的角；颜色是文字色的 22%；扩散 durations.lg + 标准缓动，松开、移出、失去焦点时淡出 durations.md + 离场缓动；裁在圆角里；系统关闭动画时直接盖满、直接消失。全局开关 `Ripple.Enabled`，组件的 Ripple 属性单独覆盖。
  - 颜色过渡：底色（Avalonia 还有描边、文字）在 durations.sm 内过渡（Avalonia 的 BrushTransition、WPF 的 rune:BrushTransition、WinForms 的 Animator）。
  - 焦点环：只在键盘聚焦时（:focus-visible）；2 像素、外扩 2 像素，圆角跟着按钮外扩。Avalonia 用 :focus-visible 伪类；WPF 看 InputManager 的最近输入设备是不是键盘（不用 FocusVisualStyle，它取不到按钮的圆角）；WinForms 用 ShowFocusCues，并默认留 4 像素 Padding（控件画不出自己的边界）。
  - 加载：点击立即无效（Command 也不执行），指示防闪烁（`DelayedLoading`：200 毫秒后才出现，出现了至少 500 毫秒）；内容透明保留宽度，旋转指示盖在中间。DelayedLoading 用 Task.Delay 再回 UI 线程，不用平台的计时器（Avalonia headless 测试的调度器不走计时器）。
  - 链接样式悬停下划线；不可用时整体 opacities.disabled。
- 从平台的 Button 派生（Avalonia、WPF）：Click、Command、键盘、读屏的 Invoke 都现成。WinForms 的 ButtonControl 实现 IButtonControl（能当 AcceptButton），读屏是 PushButton，加载中状态带 Busy。
- 踩过：
  - Avalonia 的 `Rune.BorderWidth.*` 资源是 double，Thickness 属性不能直接引用（运行时类型转换异常），描边宽度写字面量。
  - WPF 的 Border 不会把过大的圆角缩回来，radii.full 画成椭圆；Button 把圆角限制在高度的一半，尺寸变了重新算。
  - WPF 没有 IsMouseOverChanged 事件，悬停相关的代码放在 OnMouseEnter / OnMouseLeave。
- 链接按钮（LinkButton、IconLinkButton）：桌面没有站内地址，Href 是绝对地址，Click、Command 之后用系统打开（Avalonia 的 TopLevel.Launcher，WPF、WinForms 的 Shell）；读屏里是超链接，外部链接（External）没写 IconEnd 时显示外链图标、名称后补语言包的"在新窗口打开"。打开链接走可重写的 Navigate（测试里记下地址）。WinForms 的链接行为在宿主基类里按元素的 ILink 处理，文字按钮的宿主属性在 ButtonControl<T>（ButtonControl、LinkButtonControl 共用）。
- 缺口：focusableWhenDisabled（平台上不可用的控件不能聚焦，要另做"可聚焦的不可用"状态）；WinForms 的 propsContent（内容层不是元素）；ButtonBase 的示例（自定义外观的卡片要等 Flex）；type（submit、reset）不移植，登记在 parity 的 PROPS_NOT_PORTED。

### Embed、Video（2026-11-14，可选包 Rune.*.Web）

- 共用外壳 WebShell（WinForms 是 WebShellControl）：定比例的框（Ratio，null 时不定比例）→ 外框 → 骨架与网页内核叠在一起。第一次导航结束（成功或失败）才撤骨架，换地址回到骨架。
- 内核是原生子窗口，盖在框架自己画的内容上面：Avalonia、WPF 加载完之前把内核藏起来（WPF 是 Collapsed，HwndHost 照样建窗口、照样加载）；WinForms 隐藏的子控件不建窗口、不加载，只能让骨架（同级、z 序在上）盖住它。
- 圆角：Avalonia、WPF 裁不到原生窗口，边角是直的；WinForms 用 CornerClip 给内核设窗口区域，按外框圆角裁掉四角（锯齿边，没有抗锯齿）。
- Video 是网页内核里的 video 元素：页面由孪生文件 VideoPage 生成，写到临时文件夹再打开（直接给 HTML 字符串时页面不能读本地文件），一个实例一个文件，卸下时删掉。
- 自绘控制条（ControlBar=Custom）也在页面里：VideoPage 按 VideoChrome（主题颜色、字号、倍速、语言包）写进 CSS 变量与脚本的配置；主题、语言包换了不重载页面（重载会丢播放进度），下次加载时才跟上。
- 内核的用户数据放在 `%LocalAppData%\Rune\WebView2`：WebView2 默认放在程序旁边，装在 Program Files 里写不进去。
- 踩过的坑：
  - Avalonia 的 NativeWebView 导航到 data: 地址时不发 NavigationCompleted（页面照常显示），Embed 把 HTML 的 data: 地址解出来改用 NavigateToString。
  - Avalonia 在 Windows 上建原生子窗口要应用清单声明支持 Windows 10（supportedOS），不然报 "Unable to create child window"：Docs 应用加了 app.manifest，使用方的应用同样要有（Avalonia 的模板自带）。
  - WPF 的 WebShell 里叠骨架与内核用 `System.Windows.Controls.Grid`：在 `Rune.Wpf` 命名空间里 `Grid` 是 Rune 自己的响应式网格，按内容排高度，内核的高度会是 0。
  - WebView2 包不分宿主，WPF、WinForms 的程序集都引用：纯 WinForms 项目报 MSB3277（WindowsBase 版本冲突），根目录的 Directory.Build.targets 在解析引用前去掉 WPF 那个。

### Watermark（2026-11-14）

- 平铺同上游 canvas 的那一格：两行两列的单元斜对角各一份，铺开后相邻两行错开半格；单元裁在自己里面，整层再裁在水印的范围里（网页背景不会画出元素）。计算是孪生文件 WatermarkTiles，三个平台各自画（每次画都逐块画，不先画一格再平铺：区域不大，省掉缓存与高 DPI 的放大）。
- 水印层要盖在内容上面、不挡点击、读屏不念：
  - Avalonia：Decorator 里多一个视觉子元素，ZIndex 最大（Decorator 换内容时新的子元素加在最后，靠顺序会被盖住）、不参与命中、AccessibilityView=Raw。整个窗口时挪进窗口的 OverlayLayer，浮层像 Canvas 不拉伸子元素，宽高跟着浮层设。
  - WPF：Decorator 里多一个视觉子元素；整个窗口时画在窗口内容的 AdornerLayer 里。
  - WinForms：子控件各有窗口，容器画不到它们上面，只能另开一个分层窗口（LayeredWindow 的常驻形态：属于窗体、不置顶、WS_EX_TRANSPARENT 点击全穿透），盖住控件露出来的部分（与各层父控件的客户区求交），盯着自己与各层父控件的移动、缩放、显示隐藏。inspect 的控件截图里没有它。
- 字号、间隙、图片大小是固定的像素（同上游，不跟着外面的字号）；字体用主题的 sans。多行文字用换行分开（TS 的 content 数组）。
- 踩过的坑：WinForms 的 Form 处理窗口位置变化时按自己记着的 Bounds 改回去，分层窗口显示着时只靠 UpdateLayeredWindow 挪不动，要先设 Bounds（LayeredWindow.Present 里做了，对话框遮罩跟着宿主移动也靠它）。

### LoadingBar（2026-11-14）

- 控制器 LoadingBarController 是孪生文件（上游的 createLoadingBar）：计时用 TimeProvider（测试里换成 FakeTimeProvider 快进），回调回到建它的线程上（界面线程上建，绑定直接能用）；每次开始、结束换一代，已经投递出去的旧回调不做。
- 显示：Avalonia、WPF 盖在内容上（放进窗口根部的 Panel / Grid、排在内容后面，默认贴顶边、不挡点击），带 accent 色的光；WinForms 盖不到子控件上，停靠在顶边占一条，也画不出控件外面的光。
- 进度只往前过渡：归零发生在淡出之后，直接到位，不倒着走回去。读屏里是进度条，空闲时 Avalonia 是 Raw、WPF 不算控件、WinForms 是 Invisible。

### Marquee（2026-11-14）

- 上游把内容复制一份接在后面、纯 CSS 平移；桌面上控件不能出现在两处：Avalonia、WPF 的复制是 VisualBrush 画出来的那一份（点不到、读屏里没有，正好是上游的 inert + aria-hidden），WinForms 是内容的位图快照，画在自己的背景上（内容变了大小时重拍，内容自己的动画不会跟着动）。
- 逐帧推进（Avalonia 的 RequestAnimationFrame、WPF 的 CompositionTarget.Rendering、WinForms 15ms 的计时器），位移 = 走过的距离对一份长度取余（孪生文件 MarqueeVariants 的 MarqueeMotion）；暂停、悬停、焦点在里面时不累加，接着走不跳。WinForms 的指针停在子控件上时自己收不到鼠标事件，每帧看指针的位置。
- 系统关闭动画时不复制、不走，改成能聚焦、能手动滚动的区域（Avalonia、WPF 套 ScrollArea，WinForms 用 AutoScroll）。WinForms 盖不了子控件，没有两端渐隐。

### HoverCard（2026-11-14）

- 开关时机是孪生文件 HoverIntent / HoverGroup（上游的 useHoverIntent、createHoverGroup）：停留 delay 后开、离开 closeDelay 后关、移到卡片上不关、同组只开一个、刚关掉一个（300 毫秒内）时下一个直接开；计时用 TimeProvider，测试里快进。
- 三个平台都是 Popover 的子类：Popover 留了扩展点（点触发器、打开后聚焦与否、按字号的尺寸、WinForms 的面板最宽与开关回调）。卡片打开时焦点不动、点外面不关。
- 焦点离开才关：Avalonia 在 headless 里弹出层的窗口会清掉主窗口的焦点，焦点"没有了"不算移走，落到别的元素上才关。
- WinForms：卡片窗口不激活（不抢宿主窗体的激活），所以键盘不能 Tab 进卡片；子控件吞掉鼠标进出事件，指针在触发器、卡片、外面每 50 毫秒看一次，变了再告诉 HoverIntent。
- 顺手：Avalonia 的 Link 量完写上文字第一行的基线（放进 InlineUIContainer 时和正文对齐），Popover 把触发器的基线转给自己。

### ActionBar（2026-11-14）

- 上游靠 CSS 的 position: sticky 贴着滚动区域的底边。Avalonia、WPF 用渲染平移模拟：原来的位置在最近的 ScrollViewer 视口底边下面时往上挪到贴底，最多挪到父元素的顶上（sticky 只在包含块里挪）；挪了就是"盖着下面的内容"（Avalonia 的 :stuck、WPF 的 IsStuck），换成 border 描边加 md 阴影。平移参与命中，按钮照常点得到；ZIndex 设高，盖在前面的内容上面。
- WinForms 没有渲染变换，子控件是窗口，挪位置会和容器的排版引擎互相改回去：不做吸底，文档里写明停靠在滚动区域外面的底边（一直看得见）。

### 表单控制器 FormController（2026-11-15）

- 上游的 useForm 建在 zod 上（schema 校验、推出约束、onSubmit 拿解析后的值）。C# 里用平台的习惯：模型上的 DataAnnotations 特性（Required、StringLength、MaxLength、MinLength、Range、EmailAddress、Phone、Url……）逐层校验（嵌套的对象、列表项），IValidatableObject 没写成员名的结果是整份表单的错误；再加 Validate 函数与按字段的异步校验 FieldValidators（防抖、只采用最后一次、提交时等它完成）。约束（必填标记、最大长度、范围）从同一批特性推出。
- 特性没写 ErrorMessage 时文案跟着语言包（必填是 FormRequired，长度、范围、格式按语言写出限制，同上游跟着语言包的 zod 报错）；EmailAddress 这类自带默认英文提示的，和新建的同类特性比，相同就算没写。
- 字段路径用点连接属性名与列表下标（Address.City、Items.0.Name），读写走反射；写入时按属性的类型转换（文本框写回字符串、数字字段写回 decimal）。Values 是初始值的一份 JSON 往返的深拷贝（模型要能序列化），IsDirty 比较 JSON；经 SetValue 写入时按校验时机重新校验，直接改了 Values 时调 Touch。
- 显示错误的时机、SetError（值改了就消失）、整份表单的错误、数组字段（key 稳定，错误与失焦记录跟着下标移动）、局部校验、字段联动、连按提交只调用一次，都照上游。上游 onSubmit 抛带业务错误码的错误时按明细标到字段；桌面上没有错误码体系，改成抛 FormSubmitException（按字段的错误、整份表单的错误），别的异常原样抛出。
- 提交复用还没完成的那一次：没有异步步骤时 Submit 会同步完成，不能在它里面清掉“进行中的提交”（不然之后一直拿到第一次的结果）。

### Form 与 Field 接入控制器（2026-11-15）

- Form 把控制器交给里面的 Field（上游的 FormContext）：Avalonia、WPF 是继承的附加属性 Form.Controller，Form 自己是 Decorator；WinForms 没有属性继承，FormControl（WinForms 的 Form 是窗口）是 Panel，字段沿 Parent 链找它，FormControl 换控制器、改布局时通知里面的字段，后加进来的子树在 ControlAdded 里接上，外层面板晚于字段进表单时在字段建句柄时补接。
- 标签布局（LabelPlacement、LabelWidth、LabelAlign）与 IsReadOnly 在表单上统一设置（上游 field-layout）：Avalonia、WPF 把 Field 的这几个属性改成继承的（WPF 注册成附加属性，继承才穿得过中间的面板），Form 用 AddOwner，字段上写了的优先；控件的只读改成读继承的 Field.IsReadOnly，不在字段里的控件也跟着表单只读。WinForms 的字段属性没写（null、Value 为 null 的档位）时用表单的，不在字段里的控件找所在的表单。不可用用平台的 IsEnabled / Enabled，本来就往里传。
- Field 的 Path 是上游的 name（各平台的控件已有 Name）：错误不写时取 VisibleError(Path)，必填不写时取 Constraints(Path).Required（Field.IsRequired 改成 bool?），焦点离开整个字段时 Blur（Avalonia、WPF 的 IsKeyboardFocusWithin 变 false，WinForms 的 Leave），登记标签与聚焦给错误汇总（挂在树上、加载着时才登记）。
- 自动绑定（上游 valueProp，这里 BoundProperty）：里面只有一个接入字段的控件、控件上没写值时，表单的值写到控件、控件的值写回表单。不写 BoundProperty 时取 Value、IsChecked、Values、Text 里第一个"默认双向绑定"的（Avalonia、WPF 看属性元数据；WinForms 看有没有同名的 XxxChanged 事件）——勾选框的 Value 是项的值、不是双向的，于是落到 IsChecked，同上游"声明了 _checked 而没有 _value 时写 _checked"。"控件上没写值"：Avalonia、WPF 看属性有没有本地值、样式或绑定，写入用 SetCurrentValue（不顶掉使用方的样式、绑定）；WinForms 看不出来源，和新建的同类控件比，不同就算写了。值的类型不同时按目标类型转换（数字之间、集合转成 List 或数组），比较时集合按元素逐个比。
- 约束落到控件（上游 schema 推出的 maxlength、min / max）：输入框的 MaxLength，NumberField 的 Min、Max。Avalonia 用样式优先级写（控件上写的本地值照样优先），WPF 在没写时 SetCurrentValue，WinForms 记着使用方写的、没写时用约束。
- 提交：SubmitAsync（以及给按钮 Command 的 SubmitCommand，WinForms 的 ButtonControl 没有 Command 就在 Click 里调），单行输入框里按回车也提交（Avalonia、WPF 接冒泡的 KeyDown，WinForms 在 ProcessCmdKey 里，它沿父链往上走）；没通过时聚焦第一个出错字段的控件（错误汇总做了以后先聚焦汇总）。ResetCommand / Reset 回到初始值，绑定的控件跟着变。

### FormErrorSummary（2026-11-15）

- 从所在表单的控制器取：提交过之后，整份表单的错误（没有链接）在前，其余是登记过的字段里显示着的错误，按登记的顺序（上游按字段在表单里出现的顺序；字段按显示顺序登记，结果相同）；没有就收起。每项是 danger 色调的链接，点了调登记时给的 Focus（聚焦字段的控件，同上游跳到 controlId）。标题、每项的文字取语言包的 FormErrorSummary、FormErrorItem，Title 可以换。
- 自己能聚焦但不在 Tab 顺序里（上游 tabindex -1）；Form 提交失败时先聚焦显示着的汇总，没有汇总才聚焦第一个出错的控件。读屏名称是标题。上游的焦点环在外圈：这里聚焦时把描边换成焦点环色（WinForms 加粗到 2）。
- 外观：dangerSoft 底、dangerBorder 细边、md 圆角、四周 md、行距 sm；标题是警示图标加文字（medium、dangerText），列表是 List。Avalonia 是 Border（样式在 FormErrorSummary.axaml），WPF 是 Border（圆角、内边距按字号在代码里算，样式在 FormErrorSummary.xaml），WinForms 是自己画底与边的 FlexControl。WinForms 的字段与汇总都实现 IFormPart，FormControl 换控制器、加进子控件时一起接上。
- 上游的 admission 示例要用日期字段（DateField 还没做），示例等日期组件做了再补。

### FormSection（2026-11-15）

- 标题栏（标题或展开按钮、说明，结束边的操作；下面一条 borderMuted 细线、下边距 sm）加字段的栅格，行距 lg、列距 xl。Columns 是"最多几列"：栅格按 MinColumnWidth（默认 26em）分列、不超过 Columns，并保留空列（auto-fill，只有一个字段时也只占一列宽），同上游的 `repeat(auto-fill, minmax(max(min(最小列宽, 100%), 平分宽), 1fr))`。各平台的 Grid 为此加了内部的 MaxColumns（WinForms 另加 AutoFill），公开的 Grid 仍是"同时给 Columns 与 MinItemWidth 时以 MinItemWidth 为准"。
- 也是一层字段布局：Avalonia、WPF 用 Field 的继承属性（AddOwner），这一组写了的盖过 Form 的；WinForms 新增 IFieldLayout（FormControl、FormSectionControl 都实现），字段沿父链取：标签的三项里层写了的优先，只读只要有一层是 true 就是 true（同上游 provideFieldLayout）。
- 可折叠：标题换成按钮（Avalonia、WPF 是 ButtonBase，悬停 fill 底；WinForms 是从 Accordion 抽出来的 DisclosureTrigger），读屏带展开状态与标题级别，栅格放进 Collapse。收起时箭头朝行尾（Avalonia、WPF 转 -90°，WinForms 换成 ChevronRight / ChevronLeft）。IsOpen 在 Avalonia、WPF 默认双向绑定，WinForms 发 OpenChanged。
- 读屏：有标题时是一组（group），名称是标题、帮助文字是说明；没有标题时只是一层栅格，不进读屏的控件树。WinForms 的读屏没有标题级别，HeadingLevel 选标题用哪一级的 H 控件。
- 上游 id 当锚点、配 TableOfContents 的分节导航，以及 scroll-margin 让开粘性顶栏，是网页的；sections 示例要用 TableOfContents，还没做。

### FormDialog 与 PromptAsync（2026-11-15）

- FormDialog 是 Dialog 的子类（WinForms 是 FormDialogControl : DialogControl）：Dialog 加了一个内部的正文钩子（Avalonia、WPF 的 Body，WinForms 的 Body(content)），FormDialog 把使用方的 Content 放进 Form，前面依次是整份表单错误的提示条（Alert danger）与错误汇总。确认就是 Form.SubmitAsync：没通过校验返回 false（对话框留着，焦点到错误汇总），OnSubmit 抛 FormSubmitException 时 SubmitAsync 返回 false、提示条显示 FormError，别的异常由 Dialog 的确认兜住（留着）。上游的 propsForm、propsSummary 是 Form、Summary 两个部件实例。
- 命令式的小表单（上游 useFormDialog）是静态的 PromptAsync(owner, FormPromptOptions)，结果是字段名到值的字典（单值是去掉首尾空白的字符串，多选是字符串列表），取消是 null；字段是 FormPromptField 记录（Text、Textarea、Select，Multiple 时多选；Date 是 DatePicker，范围是 Min、Max，值是 ISO 日期）。必填没填时错误是语言包的 FormRequired，Validate 只看单值字段。单行输入框里按回车就是确认（同浏览器的隐式提交：Avalonia、WPF 接冒泡的 KeyDown，WinForms 在 ProcessCmdKey 里），为此 Dialog 的确认流程改成 internal。
- 看板的 confirm 示例用它：放下时先问意见，确认了才改数据。上游卡片模板拿到的 dragging（拖动中换一种写法）这里没有，卡片模板只拿到数据。

### 日期分段：DateSegments 与 DateSegmentGroup（2026-11-15）

- 孪生文件 DateSegments 是上游 useDateSegments 的状态：草稿、正在输入的数字、没打满四位的年份，逐段输入、↑↓ 回绕、Page Up / Down、Home / End、Backspace、12 / 24 小时制、全角与阿拉伯-印度数字、填全才写回、ISO 互转（FieldsOf、ToIso、FromIso）。上游靠 watch 跟外部的值，这里由控件在外部改值后调 Sync；状态不发事件，控件做完一步操作后重画。
- 段的顺序与分隔符：上游用 Intl.DateTimeFormat 的 formatToParts；.NET 用区域格式的 ShortDatePattern、LongTimePattern 拆成段与分隔符，跳过不要的段时连同它前面的分隔符一起去掉（zh-CN 年/月/日、en-US 月/日/年、de-DE 日.月.年，en-US 的时间 2:05 PM，上游测试的写法都对上）。区域格式是 24 小时制而使用方要 12 小时制时，中日韩上午下午在前紧挨着（上午9:00），其余在后面；日期与时间之间中日韩是空格、其余是", "（同 Intl）。上午下午的文字取 AMDesignator / PMDesignator。
- 段名上游取自 Intl.DisplayNames，.NET 没有对应：中文、英文内置（年、月、日 / year、month、day），其余语言用英文。数字一律用拉丁数字（上游 zh-CN、en-US 也是）。
- DateSegmentGroup：Avalonia、WPF 每段是能聚焦的 DateSegmentBox（读屏 Spinner，带 RangeValue 与 Value，名称是"组名 段名"，没填的念语言包 DateFieldEmpty），打字走文字输入事件，按键走 KeyDown；聚焦时 accent 底。WinForms 是一个自己画的控件（同 OtpFieldControl），聚焦的段是 DateSegments.FocusedSegment，Tab / Shift+Tab 先在段之间走、到两端才离开，读屏的每段是子 AccessibleObject（SpinButton）。Enter 写回打了一半的年份再提交所在的表单。上游的 contenteditable、输入法组合、粘贴拦截是网页的。

### SegmentedField、DateField、TimeField（2026-11-15）

- SegmentedField 是输入框外壳加一组 DateSegmentGroup：Avalonia、WPF 是带模板的控件（同 Select 的外壳：控件高度、左右 md、soft / 只读 / 出错 / 不可用，焦点在里面时画焦点环），WinForms 是用 ControlShell 自己画外壳的 FlexControl（同 SelectControl，GetPreferredSize 不小于控件高度）。值是 ISO 字符串的 Value（Avalonia、WPF 默认双向绑定，WinForms 发 ValueChanged），段的语言、小时制、占位取所在的语言包；所在表单重置（ResetCount 变了）时丢掉打了一半的段；点外壳空白处聚焦第一个没填的段。读屏的主元素是里面的分段组：组名取控件自己的名称或所在字段的标签，帮助文字取字段的说明与错误。
- DateField、TimeField 是它的派生类（上游是两个组件包着 SegmentedField）：Granularity 换段，Min / Max 是 ISO 字符串，不可选的日期、时刻是 Func（DateOnly、TimeOnly）。超出范围由 IsOutOfRange 判断（日期的判断是孪生文件 DateFieldRange：一段整段在范围外、按日选时是不可选的日期，带时间的界限同一天里再比时分秒）。上游的值可以是 @internationalized/date 的对象并按传入的类型写回（valueType）；C# 里值统一是 ISO 字符串，另有类型化的属性 DateField.Date / DateTime、TimeField.Time。
- 年月、只有年的排法：用区域的年月格式（zh-CN 是"2026年10月""2026年"，同 Intl），年月格式写的是月份名（en-US 的 MMMM yyyy）时用短日期格式去掉日（10/2026）。中文的"年""月"作为前一段的单位跟着它，前一段在就留着。
- 示例：DateField 的 basic、granularity、locales、appearance，TimeField 的 basic、hour-cycle、states；DateField 的 rules（DateValue 值类型、时区）没有对应。

### Calendar 与 CalendarState（2026-11-15）

- 日期网格的状态是孪生文件 CalendarState（上游 calendar-state）：选项都是 Func（同上游传 getter，状态每次读最新的值），焦点、翻页、视图、区间的锚点与悬停预览、键盘都在里面；三个平台的 Calendar 只负责画与把事件交给它。文字（标题、读屏念的完整日期、星期名）是孪生文件 CalendarText：.NET 的区域格式（ICU）与 Intl 大体一致，最短的星期名就是 Intl 的 narrow，长日期没有星期时补在后面。
- 状态要等控件进了界面（Avalonia 的 VisualRoot、WPF 的 IsLoaded、WinForms 的窗口句柄）才开始：属性初始化的先后不定，提前开始时焦点会在 DefaultFocused、今天给出之前落到别的日子上。
- CalendarView 是界面的基类（RangeCalendar、日期选择器的弹层以后共用），Calendar 加单选 / 多选、Min / Max、不可选的日期、Granularity、Picked 事件。值是 ISO 字符串的 Value（多选 Values），另有 DateOnly 的 Date；SelectionMode 不接受 Range（区间用 RangeCalendar）。
- Avalonia、WPF 每格是一个按钮（CalendarCellButton，只有焦点那一格在 Tab 顺序里，读屏 DataItem，不能选时念不可用），键盘移动、点选后整个网格重建、焦点交给新的焦点格；日期网格是列宽、行高按内容的 Grid（表头行比日期行矮、周数列比日期列窄，同 TS 的 table），月份、年份是三列的 UniformGrid。Avalonia 的状态是伪类（:selected、:middle、:today、:outside、:unavailable、:off、:period），选择器写 `:is(rune|CalendarView)`（类型选择器不匹配派生的 Calendar）；格子大小写在样式里（Width / Height 是 em）。WPF 的状态是只读依赖属性，格子大小是 CalendarCellButton.SizeEm（em 系数，部件样式里改），网格带读屏的表格节点（Panel 本来没有），删除线在代码里加（TextDecorations 不继承）。
- WinForms 每个月是一张自己画的 CalendarGridControl（只有焦点日期所在的那张在 Tab 顺序里，格子是读屏的子对象，周数是行表头），页头是 IconButtonControl、ButtonControl 子控件；页面没换时只重画格子，换页、换视图才重建。自定义格子是 CellText（文字，也是读屏接在日期后面念的）加 DrawCell 事件（同 ListBox 的 DrawItem），格子大小是 CellSizeEm。
- 从右往左：Avalonia 整棵子树镜像，翻页箭头的图标设 FlipInRtl；WPF、WinForms 的图标不镜像，箭头按方向换（WinForms 的位置自己镜像）。语言包的 Direction 不会自动换成平台的方向，示例里自己设（Avalonia、WPF 的 FlowDirection，WinForms 的 RightToLeft）。
- 示例：basic、footer、locales、multiple、multiple-months、prices、sizes、unavailable、views。上游的 components.Calendar 定制（应用里统一显示周数）与 DateValue 值类型没有对应。
- 结构与状态分开：页头与网格只在换页、换视图、换选项（周数、一周的第一天、语言、字号、可用、自定义格子）时重建，选中、焦点、悬停预览只按状态原地刷新格子。悬停预览时重建会换掉指针下的元素，Avalonia 随即发离开、进入事件，来回重建停不下来。
- Calendar、RangeCalendar 共有的选项（Min、Max、不可选的日期、并排月数、粒度、DefaultFocused、只读）在 CalendarBase（WinForms 的 CalendarControlBase），状态也在那里接好；派生类只给选择方式、已选的日期与写回。

### RangeCalendar（2026-11-15）

- 值是孪生文件里的 IsoDateRange（两端 ISO 字符串，上游的 `DateRange<string>`），另有日期的 Range；Picked 在选完一个区间时发。按月、按年选时写回整月、整年。上游的值可以是 DateValue 并按传入的类型写回（valueType、ZonedDateTime 保留时区与时间），这里没有对应。
- 快捷区间（上游 presetRange / presetIsoRange）是孪生文件里的 DateRangePreset 与 CalendarState.PresetRange（按今天与一周的第一天算），名字是 CalendarText.PresetName（语言包 DateRangePreset 的键是上游的 camelCase 名）。
- 示例：display、hotel、leave、report。

### QuarterCalendar（2026-11-15）

- 状态是孪生文件 QuarterState（上游 QuarterCalendar 组件里的那一套：焦点与年份、←→ 到头换年、Page Up / Down、单选与区间、悬停预览、Esc），不借 CalendarState：季度面板没有日、月视图，也不往更粗的视图切。整段在 Min–Max 之外的季度不可用，判断用 DateFieldRange（同上游 isPeriodDisabled）。
- 值分成 Value（单选，2026-Q4）与 RangeValue（区间，IsoDateRange 的两端是季度）；SelectionMode 只接受 Single、Range。选好了发 Picked（上游 onPick）。
- 外观与日历的月份视图一致（上游同一个配方）：Avalonia 抽出 CalendarSurface（字号档位、根、读屏分组），CalendarView 与 QuarterCalendar 都从它派生，默认样式的选择器写 `:is(rune|CalendarSurface)`，部件名用 CalendarViewParts；WPF 直接用 CalendarViewParts 的部件样式键；WinForms 把画一格的代码抽成 CalendarCellPainter，日期网格与季度的四格共用。按季度给的部件属性（propsCell 是函数）没有对应。
- 上游没有单独的演示页（API 列在 Calendar 页），示例是 Calendar 页的 quarter。

### DatePicker 与 PickerPopover（2026-11-15）

- DatePicker 是 DateField 的派生类（上游是 DateField 加弹层的组合）：后缀里的打开按钮放进 Suffix，日历弹在外壳下方。值、粒度、范围、不可选的日期全沿用 DateField；Granularity 决定弹出的是日历（年、月、日、带时间）、季度面板（Quarter）还是按 ISO 周排的日历（Week：周一开始、显示周数，已选那一周的另外六天浅色底，日历只把周一当作已选）。
- 打开按钮、弹层、Esc、点外部、Alt + ↓、关上后焦点回到按钮这一套是各平台的 PickerPopover（上游 usePickerPopover，DateRangePicker、TimePicker 以后共用）：Avalonia、WPF 用平台的 Popup（同 Select：以外壳为锚点；WPF 四周留透明边给阴影、自定义定位），WinForms 用两层窗口 FloatingSurface（同 PopoverControl）。弹层的内容只在开着时建（"今天"、快捷选项按打开的那一刻算）。
- 开关状态是选择器自己的 IsOpen（Avalonia、WPF 双向绑定），用户开关时发 OpenChanged（原因：按钮、选好了、Esc、点外面）。只读、不可用时按钮不可用、不能打开：外壳同步状态后调派生类的 Synced（SegmentedField 新加的钩子），按钮跟着。
- 一开始就开着（IsOpen 写在初始化里）：等外壳进了窗口再弹出（平台的弹出层要锚点已经在窗口里）。WinForms 的日历要有窗口句柄才建网格，弹层还没显示时 CreateControl 不给看不见的窗口建子控件的句柄，量尺寸前逐个建。
- 快捷选项是 DatePreset（固定值或每次打开时算的函数），当前值那一项 soft 样式、读屏念"已按下"；超出范围的不可用。"今天"按粒度是今年、本季度、本月、本周或今天，带时间时保留原来的时间（没有时补 00:00），选好后焦点去"时"那一段接着输入。WinForms 的按钮没有内容对齐，快捷选项的文字居中（上游靠左）。
- 上游的值可以是 DateValue（valueType、CalendarDate 进出）：C# 里值总是 ISO 字符串，要日期运算时读 Date。示例：booking、granularity、presets、states、values（values 改成用 Date 做日期运算）。

### Transfer（2026-11-16，仅 Avalonia）

- 数据同 Listbox：ItemsSource（ListboxOption、字符串，或任意数据加 ToOption），右边的值是 Values（多选的值同 Listbox 的叫法，默认双向绑定），按移过去的先后；Titles 两个标题，不写时取语言包。
- 每一边是 20em 高的外框（border、md 圆角、surface 底，裁剪）：标题栏（全选的 Checkbox、标题 medium 省略、sm muted 的"勾上 / 总数"，上下 xs 左右 sm，下边 borderMuted）、
  Searchable 时的 SearchField（soft、sm，外面四周 xs）、占满剩下高度的可多选 Listbox。勾选的状态在 Transfer 里（每边一份），Listbox 的 Values 跟着它；
  全选只管看得见（搜索后留下）、能勾的项，半选是 IsIndeterminate。搜索默认按 SelectSearch.Matches（忽略大小写与重音）；上游还认拼音，我们没有拼音表。
- 中间两个 soft、sm 的 IconButton（读屏名称"移到已选 / 可选"），没有勾上的时不可用；双击一项（DoubleTapped，从事件源找 ListboxItem 的 Option）直接移过去；
  不可用的选项勾不上也移不动。移完由 LiveRegion 播报"移了几项到哪边"。窄于 40rem 时上下排，按钮横排、旋转 90°。
- 像素对照：members 0.33%。TreeTransfer（左边是勾选的 Tree）下一步做。

### Heading 的 Anchor（2026-11-16，仅 Avalonia）

- 原来认为"桌面没有本页地址"不做；桌面文档站有了按 id 定位的章节（DocSection、TableOfContents），补上。Anchor 写了且有 Name（TS 的 id）时文字后面跟一个 # 按钮（行内的 InlineUIContainer，竖直居中），
  读屏名称取语言包（链接到本节）；点了先发路由事件 AnchorRequested（带 Id，文档站拿去复制本节的链接），没有被处理就 BringIntoView（TS 的 #id 跳转）。
- Text 改了就重建行内内容（Run 加按钮）；设 Inlines 时 Text 跟着变，用标志挡住重入。关掉 Anchor 或没有 Name 时回到只有文字。
- 按钮在 InlineUIContainer 里，主题与样式实测都选不中它（类型、类选择器都不行），外观在代码里设：透明底、textMuted（悬停 accentText，按主题资源绑定）、0.75em 字号、与文字隔 xs、xs 圆角；
  平时透明，悬停标题或用 Tab 聚焦时显示（durations.sm 渐变）。示例：Text 页 headings 的"安全与登录"（Name 写 security：XAML 的名字生成器把 Name 当字段名，不能带连字符）。像素对照 headings 14.96% → 0.32%。

### StepsForm（2026-11-16，仅 Avalonia）

- 外层就是 Form（部件 PART_Root）：Controller、标签布局（LabelPlacement、LabelWidth、LabelAlign，Field 的同名属性 AddOwner）转给它；IsReadOnly 下发到字段。上游的 disabled 是 IsDisabled：只禁用这一步的内容与提交按钮，
  步骤条、上一步、下一步照常（IsEnabled 会把整棵子树禁用）。各步是 FormStep(Title, Fields, Content) 记录，Content 是函数：换到这一步才建（字段重新登记），建的是哪一步按 FormStep 对象记（属性初始化时 Steps 可能晚于 Controller 到）。
- 下一步：ValidateAsync(这一步的字段)，通过才走；没通过时等错误显示出来（Loaded 优先级）把焦点给这一步第一个出错的 Field 的控件。提交是 Form.SubmitAsync（失败时 Form 自己聚焦第一个错误），再跳到第一个有错的步骤（当前步骤自己没错时）；
  上游用 effect 看 submitCount，这里在提交之后直接判断。单行输入框里按回车：在 Form 的回车提交之前用隧道事件接住，不在最后一步时是下一步，最后一步是提交，只读、不可用时什么也不做。
- 换步写 Current（默认双向绑定），焦点移到这一步的内容上（可聚焦、不画焦点环，读屏名称"第 2 步，共 2 步"）；步骤条 Navigable，只能点回走过的步骤，只读、不可用时哪一步都能点；有显示着的错误的步骤标成 Error。
  控制器的 Changed 里刷新步骤状态、按钮（下一步在单字段校验时 IsLoading，提交在 IsSubmitting 时 IsLoading）。
- 像素对照：review 0.48% / 0.49%；onboarding 5.37%（我们矮 4 像素：每行字段矮 2 像素，同 Form 页的差异，见 DOCSITE 差异基线）。示例：StepsForm 页的 onboarding、review（同上游；确认密码用模型的 IValidatableObject，Dependencies 让改了密码重新校验确认）。

### NavBar 与 NavMenu（2026-11-16，仅 Avalonia）

- 菜单数据是一个记录 NavMenuItem(Label)：Href、Icon、Children、Badge、Disabled、Active（上游的 NavBarItem 与 NavMenuItem 合成一个：同一份菜单数据同时给顶栏与侧栏），HasPages 判断有没有能显示的页面。
- NavMenu 本身就是 NavList（StyleKeyOverride 用 NavList 的主题；CurrentHref、Navigate、紧凑照旧），Items 变了重建子元素：叶子是 NavItem，顶层的目录是 NavSection（TopLevel = Group 时是 NavGroup），更深的目录是 NavGroup，没有页面的目录不画。
- NavBar 的当前页、跳转同 NavList：CurrentHref（默认双向绑定）、先发 Navigate（标记 Handled 不跳）、再调 NavigateCommand、改 CurrentHref；当前页的项、子项是当前页的菜单按钮、收起来的项里有当前页时的"更多"都是 :current。
  链接、菜单按钮、"更多"都是 NavBarButton（ButtonBase 派生，主题在 NavBar.axaml：控件高度、左右 md、md 圆角、中等字重、textMuted，悬停 fillHover 底与 text，当前页 accentText 与底边 2 像素强调色线、下面两个角不圆在代码里设）。
  有子项的是 Menu（菜单项是 MenuItem，点了走同一套跳转；当前页的读屏状态是"当前页"，有计数时文字后面跟角标）；桌面没有站内地址，菜单里不用 MenuLinkItem（它用系统打开绝对地址）。
- 放不下：一行（列间距 xs）从前往后排到留出"更多"的位置，其余 IsVisible = false（不占位、读屏与 Tab 跳过）；藏起来的项量不出宽度，用它上次显示时的宽度。"更多"的菜单在打开时按收起来的项建（有子项的按 MenuGroup 列出）；
  Popover 的 OpenChanged 不报告代码里改的 IsOpen，所以看 IsOpen 属性本身。图标、角标、箭头要显式竖向居中（Flex 的 Align 管不到写了对齐的子元素），行高照 NavRow 设成 normal。
- 像素对照：NavList 页 menu 0.24% / 0.23%；nav-bar 3.30%，差在上面的 Field 与 Slider（整体低 2 像素、滑块圆钮），顶栏本身对得上。示例：NavList 页的 menu、nav-bar（同上游；上游的地址不匹配锚点，没有当前页，这里不设 CurrentHref）。

### PivotTable 与透视计算（2026-11-16，仅 Avalonia）

- 透视计算是孪生文件 Pivot.cs（上游 pivot.ts）：Pivot.Of(明细, PivotOptions) 返回 PivotResult（RowKeys、ColumnKeys、Cell、RowTotal、ColumnTotal、GrandTotal、Extent）。数值全是 decimal：
  求和、最小、最大精确，平均是和除以个数（上游平均是浮点）；接口给的十进制字符串按数字算（同 TableColumns.Numeric）。维度值按语言比较：CompareInfo 加 .NET 10 的 CompareOptions.NumericOrdering（同 localeCompare 的 numeric，中文按拼音），
  Pivot.Comparer(语言) 给出，PivotOptions.Compare 可以换；行逐级排序（同一上级下的下级按 RowOrder 或比较），没列进 Order 的排在后面。
- PivotTable 本身就是 Table（不继承 ColumnTable：上游的 rows、columns 是维度，和 ColumnTable 的同名属性意义不同）。ColumnTable 的建表抽成内部的 ColumnTable.Fill（ColumnTableSource 带上数据、列、语言、合并、按格定制），两者共用。
  维度是 PivotDimension(Key, Header) 记录（Value、Format、Order），数值是 PivotValue(Key, Header)（Value、Type、Format），取值同列定义（同名属性或字典的键）；聚合 PivotAggregate 枚举。每行的 DataContext 是 PivotRow(Key, Dimensions)。
  上游从 ColumnTable 继承的 cellSpan、summaryLabel 照转（GetCellSpan、SummaryLabel）；部件名同 ColumnTable。
- 列：各级行维度（除最后一级外相同的上级合并）、列维度的每个值（靠结束边）、合计列（有列维度且 Totals 时）；Totals 时每列一个合计（合计行）。空格子是 Empty（默认"—"）。合计列的格是 semibold（模板优先级的资源绑定）。
- 热力图：强调色按这一格在各格最小到最大之间的位置取 6% 到 45% 的不透明度，设在格的 Background 上（模板优先级）；Table 把格的 Background 画满整格（含内边距，同 td 的底色），盖在行的底色上，所以 TableCell 的 Background 现在是表格画的。
- Table 的单元格加了等宽数字（TextElement.FontFeatures = tnum，同上游 td 的 tabular-nums；表头格没有）。Table 内部的 Rows（各部分的行）改名 SectionRows，免得和 PivotTable 的 Rows 冲突。
- 像素对照：count 0.43%；heatmap 1.47% / 1.51%，是全局字体的差异（上游 system-ui 在 Windows 11 是 Segoe UI Variable，数字宽约 9%，数字列分得宽、热力底色的列边界错开），见 DOCSITE 差异基线。示例：PivotTable 页的 heatmap、count（同上游）。

### ColumnSettings（2026-11-16，仅 Avalonia）

- Popover（bottom-end）的触发器是 IconButton（Columns3、ghost、中性，名称"列设置"），面板竖排隔 sm、至少 14rem 宽：标题行（标题 sm、中等字重；"重置"是 sm、ghost 的 Button）与最高 20rem 的 ScrollArea 里的 Sortable，每项是一列的 Checkbox。
- Hidden、Order 是 IReadOnlyList&lt;string&gt;，默认双向绑定，写回新的列表（同上游给 _hidden、_order 赋新数组）；Order 没列到的列按定义的顺序排在后面（TableColumns.Arrange，孪生文件里；上游还把固定列排到两头，列定义有了固定再加）。
  拖完排序时 Sortable 已经就地改好了列表，写回 Order 时不再按它重排。重置清空两者再发路由事件 ResetRequested（上游 onReset）；Changed 表示别的列设置改过，这时"重置"也可用。
- 勾选框按项建一次，Hidden 变了就地改勾选与可用（整张列表重建会让键盘勾选的焦点丢掉）；只剩一列可见时它的勾选框不可用（只数现有的列）。部件名在 ColumnSettingsParts（上游 propsTrigger、propsPanel、propsList、propsCheckbox、propsReset，另有 header、title）。
- 示例：Table 页的 column-settings（我们自己的：上游只在 QueryTable 里用它，这里配 ColumnTable，两边看同一份 Hidden、Order）。

### ColumnTable 与列定义（2026-11-16，仅 Avalonia）

- 列定义是孪生文件 TableColumns.cs（以后的 DataTable 也用）：DataTableColumn(Key, Header) 记录，Value、Cell、Type（ColumnType 枚举）、Format（NumericFormat）、Width、MinWidth、Align（TableAlign）、Wrap、Empty、Statuses、Summary 是 init 属性；
  这一轮只放 ColumnTable 用得到的，排序、筛选、编辑、固定、调整宽度等 DataTable 再加。取值同 FieldCompare：Value 函数，或按 Key 取同名属性（字典按键）。
  上游 summary 的 'sum' 或函数在这里是 ColumnSummary.Sum / ColumnSummary.Of(rows => …)。Cell 返回 null 时按类型显示值（上游是渲染空）：只有几行要换内容时不用自己格式化其余的行。
- TableColumns 的计算照 columns.ts：TypeOf（写了 Type、Format 或相加的合计算数字；否则看第一个非空值，字符串不猜）、AlignOf（数字类靠结束边）、Numeric（数值与十进制字符串换成 decimal，不经过浮点）、
  FormatCell（数字、金额、百分比按 NumericFormat，列的 Format 没另写样式时换成类型的样式，百分比默认最多 1 位小数；文件大小 1000 进位、取整后进到 1000 的换下一档；日期按 TimeText 的 medium，日期时间加 short 的时间）、
  Sum（decimal 相加，没有数值时 null）、RepeatedRowSpans（mergeRepeated 的跨行数，后一列不跨前一列的分组）。显示（上游的 useColumnDisplay）在 ColumnTable 里：空值是列的 Empty 或语言包的 EmptyValue，状态是 StatusBadge。
- ColumnTable 本身就是 Table（StyleKeyOverride 用 Table 的主题，外观属性同 Table），按列定义生成表头、表体、表尾。数据是 ItemsSource（Table 自己有 Rows），实现了 INotifyCollectionChanged 的集合增删时整张表重建；
  每行的 DataContext 是那一行的数据。合并：GetCellSpan（上游 cellSpan，CellSpan 是它返回的记录）优先，其次 MergeRepeated；返回 0 的格不生成。没有行时一格跨满所有列、居中、textMuted 的 Empty（默认"暂无数据"）。
  有一列写了 Summary 时多一行表尾（第一列没写时是 SummaryLabel，默认"合计"）。部件（上游 propsHeaderCell、propsRow、propsCell、propsSummaryCell）是 ColumnTableParts 的名字，按行定制写样式或绑定行的 DataContext（上游可以传按行计算的函数）。上游的 getKey 不需要（不比对虚拟 DOM）。
- Table 加了合并单元格：TableCell.RowSpan、ColumnSpan（上游 rowspan、colspan）。网格按 HTML 表格的规则排格（每格放在本行第一个没被上面跨行的格占住的列，跨行不超出所在的部分）；跨列的格要的宽度比那几列合起来的大时差的平分，
  跨行的格高出来的加到它的最后一行。分隔线改成画在格上（同 CSS 的 td 下边线）：跨过的行线在合并格那段断开；跨行的格用它所在那一行的底色（指针下的行、斑马纹不染到它）。
- 单元格的裁剪框是含内边距的整格（同 CSS 的 td）：格排在内边距里、自己不裁（主题里 ClipToBounds 关掉），网格给它设 Clip 几何。原来格自己裁在内容区，表格里迷你图最后的点被切掉一半。
  内边距不交给格自己的 ContentPresenter：布局取整会把 6.125 的内边距取成 6，行高比上游（内容加精确的内边距）矮，plans 暗色多出一整行的差异。网格本身画行时也不裁（固定的那几层才裁到自己的区域），不然高度带小数时裁掉吸附到整像素的最后一条线。
- 定宽的列（TableHeaderCell.ColumnWidth，列定义的 Width）不分多出来的宽度：有不定宽的列时只分给它们（同 CSS 的自动表格布局；全是定宽的列时照旧按比例分给所有列）。原来定宽的列也跟着变宽，in-table 的件号列宽了 60 像素。
- 像素对照：Table 页 column-table 0.07% / 0.08%，plans 不变（0.08% / 0.12%）；Sparkline 页 in-table 0.63% / 0.61%（剩迷你图折线差一个像素的位置）。示例：Table 页的 column-table（询价比价单，同上游）、Sparkline 页的 in-table（同上游；迷你图高 1.5em 写成 21 像素，表格正文 md 字号）。
- FieldCompare 的 CompareField 是这套列定义之前的独立记录；上游的字段就是 DataTableColumn。改成共用要动 WPF、WinForms 的 FieldCompare，等那两个平台恢复时一起改。

### CodeGroup（2026-11-16，仅 Avalonia）

- 上游的 items 是 CodeBlock 的属性加 label、value 的对象；这里各段直接是 CodeBlock（Items 是内容），一段的选项就是 CodeBlock 的属性，标签文字、值是附加属性 CodeGroup.Label、CodeGroup.ItemValue（默认等于标签）。各段的 Flush 设成 true（同上游强制 flush）。Items、标签、值变了都重建标签与面板，先把代码块从旧面板里取出来。
- 由 Tabs（sm）与 TabList 组合，Value 与 Tabs.Value 双向绑定（默认 TwoWay），不写时 Tabs 显示第一段，点了才写回；几组绑同一个值一起切换。
- 外框 1 像素 borderMuted、md 圆角、surface 底、裁剪；颜色是模板优先级的资源绑定（样式盖得过），圆角与标签栏左右 space.sm 按字号在 Measure 里算。标签栏的 surfaceMuted 底与内边距用 TabList 新加的 Background、Padding（转给标签栏外框，分隔线仍横贯整行；套一层 Border 的话分隔线会缩进来）。标签栏的读屏名称默认是各段标签用" / "连起来，模板优先级，使用方写的盖得过（上游 propsList 的 aria-label）。
- 面板的焦点环画在里侧（外框裁剪，同上游 focusRingStyles.inset）：CodeGroupParts.axaml 把 TabPanel 焦点环的 -4 外扩改成 0。
- 示例：DocsKit 页的 group（同上游两组绑同一个值；命令换成 dotnet CLI、Package Manager、Paket 的安装与移除）。

### PropsTable（2026-11-16，仅 Avalonia）

- 同 TextTable，本身就是 Table（StyleKeyOverride 用 Table 的主题），按 Props（ApiProp 的列表）与 Caption 生成表头、各行；Density 的默认值改成 compact（上游 mergePartProps 给的默认），外观属性照 Table 能改。列标题、"必填"（warning）、"可绑定"（accent）取 DocsKitMessages，跟着语言包变。
- 数据是 record：ApiProp(Name, Kind, Type, Description)，Required、Default、Bindable、Values 是可选的 init 属性；Kind 是 ApiPropKind 枚举（上游的 keyof ApiPropKinds）。上游的 ComponentApi 是文档站从类型生成数据用的，桌面没有生成器，不做。
- 名称不折行（行内代码在上游能任意处断开，表格会按它把名称列压到最窄），标记放不下时换行；类型 codeType 色；默认值不折行，没有时 textMuted 的"—"。说明一列的内容至少 18rem 宽：Table 按各格内容的最小宽度分列宽，最小宽度设在内容上就是上游 td 的 min-width。
- 可选值有任何一个带含义时列在说明下：平台 Grid 两列（Auto、*，即 max-content 1fr），sm 字号、列间距 md、行间距 xs、与说明隔 sm；间距按字号算，字号变了重算。
- 示例：DocsKit 页的 handwritten（同上游：Rating 的四个属性、斑马纹）。

### SlideCaptcha（2026-11-16，仅 Avalonia）

- 组件只负责交互、不判对错：SlideChallenge 的背景与拼图是 IImage（服务端给的图由应用解码成位图），拼图和背景一样高、左边缘对着缺口的左边缘，位置换算成像素就是答案。松手发 Released（SlideResult：位置、最多 240 点的轨迹、时长），"换一张"发 RefreshRequested；Status（Idle、Verifying、Success、Error）由父级按判定结果设，Verifying、Success 时锁定，换了图或设回 Idle 时滑块回到起点。
- 画面按图的宽高比、宽度占满；拼图按位置放（宽按比例，带投影），右上角"换一张"，通过、没对准时底部一条状态色的结果条。滑块条 2.75rem 高，滑过的一段从左边起（缺口在图上的位置是物理的，从右往左时也一样），中间是提示（读屏跳过，状态由实时区域播报）。滑块是可聚焦的 SlideHandle，读屏是滑杆（IRangeValueProvider：0 到能走的距离，值是像素位置）；键盘 ←/→ 2 像素（从右往左时反过来）、Home / End、Enter 或空格松手，没动过不松手；指针拖动时捕获指针，丢了捕获算取消、回到起点。
- 上游松手回到起点的回弹、没对准时滑块晃一下是过渡动画，这里直接到位。画面与滑块条是 Border 的派生类，样式选择器写 :is(Border)；Border 没有字号，md 圆角在代码里按字号算。
- 示例：basic（同上游：本地画渐变与圆、挖一个缺口，判定差 4 像素以内、用时不少于 280ms）。

### NotificationList 与 NotificationCenter（2026-11-16，仅 Avalonia）

- 通知是 NotificationItem 记录（Id、Title、Time 是 DateTimeOffset，另有 Description、Type、IsRead、Href、Tone），类型是 NotificationType(Key, Label)。组件只报告：MarkedRead（读过的 Id，点了一条、全部标为已读）、ItemClicked；读过的状态由应用保存，改了 Items 再给回来。有绝对地址（Href）的那一条点了用系统打开（同 Link），应用内的跳转在 ItemClicked 里做。
- NotificationList：标题行（Title，默认"通知"，与"全部标为已读" xs ghost；ShowTitle = false 时它挪到筛选行的最后，上游的 title={false}）、筛选行（类型的 ToggleGroup xs，"只看未读" Switch sm 靠结束边）、列表（ScrollArea，ListMaxHeight 限高）、空状态（Empty sm：没有通知时是 Empty 属性或"没有通知"，筛完没有时是"没有符合条件的通知"）、底部（Footer）。每一条是 NotificationRow（ButtonBase：Enter、空格、点击都一样；读屏是按钮，有地址时是链接，名称前加"未读"）：0.5rem 的未读色点（StatusDot，Tone 默认 accent）、标题 sm（未读 semibold，伪类 :unread）与说明 xs muted、RelativeTime xs muted；上边一条 borderMuted，悬停 fillHover。内边距在代码里按字号算（Border 没有字号）。
- NotificationCenter：Popover（没有内边距的派生类）的触发器是 Indicator（未读数）包住的铃铛 IconButton，读屏名称"通知，未读 N 条"；面板里是 NotificationList，宽 min(26rem, 窗口宽 − 2rem)、列表最高 24rem，默认弹在 bottom-end。列表在弹出层里，事件冒泡不到通知中心（弹出层的根不在它的可视树里）：在列表上接住、从通知中心再发一次；点了一条就收起。IsOpen、Placement、Offset、Arrow、OpenChanged 透传给 Popover。
- RelativeTime 的样式键是 Text：按部件名选它要写 rune|Text#PART_Time，写 rune|RelativeTime 选不中。
- 示例：top-bar、inbox、widget（同上游）。

### ErrorBoundary（2026-11-16，仅 Avalonia）

- 上游靠 Vue 的 onErrorCaught 接住里面组件在 setup、渲染、事件处理里抛的错误。桌面上能可靠接住的是两处：建内容时（ContentTemplate.Build）与排版时（里面的 Measure、Arrange 在 ErrorBoundary 自己的 MeasureOverride、ArrangeOverride 里调用，抛出的异常在这里接住，换上出错提示再排）。事件处理、异步里的错误不经过组件（平台的事件直接调处理函数，同上游接不住定时器、Promise 里的错误），由代码用 ErrorBoundary.Show(控件, 错误) 交给外面最近的边界（同 react-error-boundary 的 showBoundary），不在边界里时返回 false。
- 内容是模板（ContentTemplate，XAML 里直接写 DataTemplate）：重试、ResetKey 变了时整个重建（出错时的状态不可信）；进了界面才建（属性初始化的先后不定，ErrorCaught 也要先订阅上）。上游的 resetKeys 数组在这里是一个值，要几个时给元组。
- 接住时发 ErrorCaught（错误与位置：create、layout、report），不再往上传。默认出错提示是 danger、sm 的 Result（部件 PART_Fallback）：标题（语言包 ErrorTitle）、错误信息与"重试"（sm、soft、中性、RotateCcw），读屏立即播报（Assertive，上游的 role="alert"）；Fallback 拿到错误与重试的方法自己画。
- 示例：dashboard、fallback（同上游；出错放在建内容时）。

### ThemeSwitcher 与 ThemeThumbnail（2026-11-16，仅 Avalonia）

- 上游的主题偏好（createThemePreference：选择存在 localStorage、按系统切换、把主题交给 createUi）在桌面上就是 Avalonia 的主题变体：Value 是当前的选择（ThemeVariant，Default 是跟随系统），选中时写回，AppliesToApplication（默认 true）时设到 Application.Current.RequestedThemeVariant；只改一块时关掉它，把 Value 交给 ThemeVariantScope。记住选择由应用自己存 Value。列出的主题是 Themes（默认亮色、暗色，全部内置的是 RuneThemeVariants.All），IncludeSystem 列"跟随系统"（上游的 system，与 System 命名空间同名）。上游选中后从按钮处圆形展开（View Transitions），桌面上直接切换。
- 菜单是 Menu 加一个 MenuRadioGroup（读屏名称"主题"，不显示组标题），触发器是 IconButton（图标表示当前的选择）。内置主题与跟随系统有默认文字（语言包 Theme*，公开为 ThemeSwitcher.BuiltinLabel，上游的 builtinThemeLabel）和默认图标（同上游 DEFAULT_ICONS），自己的主题用 Labels、Icons（按主题变体的 Key）。上游透传给菜单的属性：IsOpen、Placement、Size、Ripple、OpenChanged 接到里面的菜单。
- ThemeThumbnail（上游在 theme-customize，这里放 ThemeCustomize 文件夹）：自己画的页面一角，颜色用 TryFindResource(键, 主题变体) 取那套主题的资源，不用套 ThemeVariantScope；属性叫 ThemeVariant（Theme 是 Avalonia 控件主题）。6em × 3.75em 定宽，默认靠左、顶端对齐（上游 inline-flex，放在竖排里不被拉宽），读屏跳过。
- 示例：ThemeSwitcher 页的 scoped（亮色、暗色、纸张，只作用于下面一块）与 gallery（全部内置主题的单选卡片）。

### FileDrop 与 FileAccept（2026-11-16，仅 Avalonia）

- 选文件的区域：点击、Enter / 空格打开系统的文件对话框（TopLevel.StorageProvider，按 Accept 换成扩展名筛选；Directory 时是文件夹对话框），也接平台的拖放（DragDrop，Avalonia 12 的 DataTransfer.TryGetFiles）。交出的是 PickedFile（IStorageFile 与相对路径：选文件夹时"文件夹/子文件夹/文件名"，同上游的 webkitRelativePath），经路由事件 FilesPicked、Rejected（上游的 onFiles、onReject）；不是多选也不是选文件夹时只取第一个。文件夹递归用 IStorageFolder.GetItemsAsync。
- accept 的匹配是孪生文件 FileAccept（上游 matchesAccept）：浏览器的文件带 MIME 类型，桌面上的只有名字，类型按扩展名推（常见的图片、音视频、文档、压缩包），推不出时只能靠扩展名规则；同时给对话框算出扩展名筛选。
- 外观：底色与 2px 虚线边框自己画（Avalonia 的 Border 画不了虚线），状态是伪类 :pointerover、:dragging、:invalid、:locked（只读或不可用，样式在 FileDropParts.axaml）；键盘聚焦时外面一圈焦点环。图标按字号画（Icon 的大小是它的字号），1.5em 在代码里设；上游 propsIcon 换图标在这里是 Icon 属性。读屏是按钮，名称是字段的标签、否则是提示文字，按下打开对话框。
- 示例：Upload 页的 import（同上游；Upload 还没做）。测试里的文件、文件夹是 DispatchProxy 做的假 IStorageFile、IStorageFolder。

### NumberRangeField（2026-11-16，仅 Avalonia）

- 两个不带加减按钮的 NumberField 中间一个"–"（Separator，textMuted，读屏跳过），两个框平分宽度。值是 NumberRange(Start, End)（两端 decimal?，默认双向绑定）；改一端时两端都有值且起比止大就对调（NumberRange.With），不报错。Min、Max、Step、Format、Prefix、Suffix 与外观（Variant、Size、Radius）在两个框上同步；上游的 placeholder 二元数组拆成 StartPlaceholder、EndPlaceholder，不写时是语言包的"最小值""最大值"。
- 接 Field：区间自己实现 IFieldControl，Field 认领的是它（标签点击聚焦组里第一个框；Field 往里找控件时遇到它就停，不再往里认领两个框）。这一组是读屏的 Group，名称是字段的标签、帮助文字是字段的说明与错误；只有第一个框跟着必填，两个框各自念"最小值""最大值"，第二个框的帮助文字只有自己的占位（上游"控件里再嵌的控件不接 Field"）。出错、只读两个框各自跟着所在的 Field。
- 示例：NumberField 页的 range（同上游的三个）。

### ColorSwatch（2026-11-16，仅 Avalonia）

- 色块、名称、取值竖排（隔 xs）。Color 是画刷：上游的主题 token（colors.accent）在这里是 DynamicResource 绑的画刷资源，换主题时画刷换掉，取值跟着变，不用像上游那样挂载后从元素上读。取值不写时显示颜色本身（#rrggbb，不透明度不满时 #rrggbbaa，按画刷的 Opacity 折算）；上游的 value={false} 是 ShowValue = false。
- 色块自己画（3rem 高、md 圆角）：surface 底上 8 像素一格的 fill 棋盘格（上游 1rem 一组、四格），上面是颜色，内描边是正文色 14% 的 1 像素线（上游的 color-mix）；换主题时重画。色块是装饰，读屏跳过。
- 示例：DocsKit 页的 swatches（同上游的十二个 token）。

### ViewportFill（2026-11-16，仅 Avalonia）

- 竖排的 Flex，高度是所在滚动区域（外层最近的 ScrollViewer，没有时是窗口）的可视区高减去自己的顶边与 Bottom（默认 xl），不小于 MinFillHeight（Length，默认 24rem；平台的 MinHeight 是像素数，所以换了名字）。顶边是在滚动内容里的位置（加回已经滚过的距离），不随滚动变；每次排版后重新量（上面的内容变高、窗口变大小都跟着），值没变时不再触发排版。
- 撑满剩下高度的子元素写 Flex.Grow；内容超高时 Flex 会让所有子元素一起收缩，只想让列表收缩时给其他子元素关掉 Shrink（上游的 DataTable flexible 只让表格收缩）。
- 测试窗口只装了组件库的主题，平台的 ScrollViewer 没有模板（可视区是 0），测试里用 ScrollArea。
- 示例在 AsideLayout 页（viewport-fill，同上游；上游的 DataTable 还没做，用滚动的行代替，外框是 32rem 高的滚动区域代替窗口）。

### AuthLayout（2026-11-16，仅 Avalonia）

- 认证页版式：Hero（品牌区）、Content（表单）、Tools（结束边上角的工具）三块自己排版。有 Hero 且自己够 64rem 宽时左右分栏 1.15 : 1（上游 minmax(0, 1.15fr) minmax(0, 1fr)），品牌区铺满整高、表单区内容垂直居中；窄了品牌区按内容高放在顶部、表单区顶部 xl 内边距、内容贴近顶部；没有 Hero 时只有居中的表单区。工具区按整页定位在结束边上角（离两边 md），在最上层；从右往左时平台整体镜像。
- 底色是 Background（默认 background，样式在 AuthLayoutParts.axaml），自己画；表单区是 Border，没有字号，内边距（lg、窄时顶部 xl）在代码里按字号算。上游的 minHeight: 100dvh 在桌面上是放进窗口时拉满。品牌区的外观（上游 propsHero）按部件名 Border#PART_Hero 写样式。
- 示例：login（同上游；品牌区 accentSoft 底、四周 xl 的样式写在示例里）。

### AsideLayout（2026-11-16，仅 Avalonia）

- 主栏 + 侧栏：Children 是主栏的块、Aside 是侧栏的块，各是一个竖排、隔 Gap（默认 md）的 Flex（部件 AsideLayoutParts.Main、Aside，侧栏的读屏名称是 AsideLabel）。按自己的宽度决定叠不叠（上游的容器查询）：不到 56rem 时叠成一列、侧栏在后；够宽时侧栏 AsideWidth（Length，默认 26rem）、主栏占满剩下的，顶端对齐；从右往左时平台整体镜像，侧栏到左边。
- 侧栏贴住（Sticky，默认 true）复用 Affix：并排时侧栏外层换成不加阴影、离顶 md 的 Affix，它只在父元素（这个布局）的范围里贴住，同 CSS 的 position: sticky；叠成一列或关掉 Sticky 时换成不动的 Decorator（Affix 的 OffsetTop 为空也按 0 贴住，不能靠它关）。上游离顶还加 AppShell 的 stickyOffset：桌面的 AppShell 顶栏不在滚动区域里，不用加。
- 示例：detail（同上游，侧栏 18rem、不贴住）。

### Can 与 Provider.Permissions（2026-11-16，仅 Avalonia）

- 权限判断是 Provider 的可继承附加属性 Permissions（`Func<string, bool>`，上游 createUi / Provider 的 permissions），哪里都没写时一律放行；`Provider.Can(控件, 权限点…)` 是上游的 useCan（几个时任一满足即可），用来禁用按钮、过滤菜单项。
- 上游靠响应式状态让界面跟着权限变；C# 里换角色、登录退出后给 Permissions 换一个新的函数，属性变化沿逻辑树通知到每个 Can。自己用 Provider.Can 判断的地方（按钮的 IsEnabled）由使用方在同一处重算。
- Can 是 Decorator：Content（有权限时）与 Fallback（文字或控件）由里面的 ContentPresenter 呈现。上游的 permission 可以是数组，XAML 里写成逗号分开的一个字符串；没写 Permission 时不设限。只控制界面，不是安全边界。
- 示例在 Provider 页（permissions，同上游）：切换角色，"新建""删除"跟着出现、消失，"批量操作"按 Provider.Can 禁用。

### CalendarBoard（2026-11-15，仅 Avalonia）

- 整页的月历（上游 CalendarBoard）建在孪生文件 CalendarState 上（同 Calendar：单选、今天、范围、只读、一周的第一天、键盘），只是界面换成每天一个大格子。Value 是选中那天的 ISO 日期，Month 是 yyyy-MM，都默认双向绑定；翻页写回 Month，父级改 Month 时翻过去。进界面前不碰状态：一开始的页先看选中的那天、再看 Month（同上游 useCalendarState 的 selected 优先于 defaultFocused），Month 写在初始化里也不会抢先定下页面。
- 数据：ItemsSource（ObservableCollection 增删时重建格子）加 GetEventDate / GetEventEnd（DateOnly，跨天的每一天都有，最多铺 366 天）、GetEventLabel、GetEventTone；每天最多 MaxEvents 条，其余收成"还有 N 项"（点了选中那天）。上游的 renderEvent 是 EventTemplate（数据是 DataContext），renderDay 是 DayContent（日期与 CalendarBoardDayState，返回文字或控件），getDayNote 是 GetDayNote，getEventKey 不移植（格子随月份重建）。
- 外观：页头是 Flex（标题 lg semibold 占满、Toolbar、"今天" sm soft neutral、上下月 sm 图标按钮，放不下换行）；表格是七列等宽的 Grid，表格线收拢成一条（每格画左、上两边，最后一列、最后一行补右、下）；格子至少 7em 高、四周 xs，周末 surfaceMuted，选中 accentSoft 加 2px accentBorder 内框（画在格子里的一层上，格子自己的边框是表格线）。格子、日程条是 Border，没有字号，em 解析不出来：高度、内边距、圆角、间距在代码里按看板的字号算；日程条的色调用 Resolved.Palette，样式取 Soft 与 Text。
- 读屏：看板是分组，表格以标题为名称，日期数字是 DataItem，名称是完整日期（今天加"今天"）与"N 项日程"；只有当前日期在 Tab 顺序里，键盘移动后焦点跟过去；翻页时播报新的月份。点格子的空白处选中那天，点格子里的按钮、输入框照常。
- 示例：schedule（筛选在 Toolbar 里，右边列出选中那天的日程）、roster（只读的车间排班，DayContent 放早中夜三班，GetDayNote 放节假日与调休）。

### TimePicker 与 TimeSlots（2026-11-15）

- TimePicker 是 TimeField 的派生类（同 DatePicker 之于 DateField）：后缀里的按钮（Clock）打开 PickerPopover，弹层与外壳同宽（PickerPopover 的 MatchWidth，上游的 matchWidth）、内边距 xs（Inset），里面是时间列表（Listbox，最高 16em，其余滚动；必选，再点已选项不取消）与"此刻"（ghost、sm、占满宽度，上方 xs）。WinForms 同宽时内容层关掉 AutoSize（不再按自己想要的宽度改回去），列表开 AutoSize 才会被 FlexControl 按首选高度摆。
- 列表的时刻做成孪生文件 TimeSlots：从 Min（默认 00:00）起每 Step（默认 30）分钟一项到 Max（默认 23:59）；文字按小时制（HourCycle，不写时按语言），24 小时制补零（09:00，同浏览器 timeStyle short），12 小时制用语言的短时间格式，语言本身是 24 小时制时上午 / 下午放前面（tth:mm）；值是 HH:mm，选好后精确到秒时补 :00。TimeField 的解析也改用 TimeSlots.Parse（原来三个平台各有一份同样的私有方法）。
- 不可选的时间（IsTimeUnavailable）在列表里不可用、键入时外壳出错；当前值正好是列表里的一项时选中它（只比时分）。"此刻"精确到分，超出范围、不可选时不可用。选好后关上，焦点回到按钮。上游的 valueType="dateValue" 在 C# 里是 Time 属性（TimeOnly）。
- 示例：meeting、states（上游两个 demo）。

### DateRangePicker 与 SegmentedFieldBase（2026-11-15）

- 分段输入框的外壳拆成抽象基类 SegmentedFieldBase（WinForms 是 SegmentedFieldControlBase）：外观档位、所在 Field 的标签与状态、表单重置、清空、点外壳空白处聚焦都在这里，按"几组分段"（1 或 2）建 DateSegments 与 DateSegmentGroup；每组的值怎样读写、出错怎样判断、读屏名称由派生类给。SegmentedField 是一组（Value 字符串，DateField、TimeField、DatePicker 不变），DateRangePicker 是两组。整理与功能分开提交。
- 外观共用一套：Avalonia 的 ControlTheme、WPF 的主题样式都以 SegmentedField 为键、以基类为目标类型（基类的样式键是 SegmentedField），模板里多了 PART_Separator（"–"，textMuted，左右 xs）与 PART_End（结束那组），一组时藏起来；两组时开始那组按内容宽，结束那组占满剩下的宽度。WinForms 的"–"是 TextControl，左右 xs 是它的内边距（FlexControl 不看外边距）。parity 把 XBase 的部件名类认作 XParts（SegmentedFieldBase 的部件在 SegmentedFieldParts）。
- DateRangePicker：值是 IsoDateRange（两端 ISO 字符串，形式随 Granularity 同 DateField），两端都填好才写回；只填了一端时值是 null，那一端记在选择器里（自己写回的值不再同步回两端，否则删掉一端时写回的 null 会把另一端清掉；表单重置时两端放回值）。结束早于开始（同一种 ISO 形式按字符串比较）、超出范围、不可选时外壳出错，值照样写回。开始那组最后一段输满时焦点进结束那组，结束那组第一段删空再删回到开始那组的最后一段（DateSegments.Exit）。两组的读屏名称是"字段名 开始日期 / 结束日期"。上游的 valueType="dateValue" 在 C# 里是 Range（两端 DateOnly），startName / endName 是原生表单提交的字段名，不移植。
- 弹层沿用 PickerPopover：按粒度是 RangeCalendar（年、月整段，周按 ISO 周：周一开始、显示周数，写回两天各自所在的周）或 QuarterCalendar 的区间模式；带时间时日历选日期，开始补 00:00、结束补 23:59（时的粒度 23:00、秒的粒度 23:59:59），已经填了的时间不变，选好后焦点去开始那组的"时"。快捷区间是 RangePreset（孪生文件）：内置的名字（DateRangePreset，按今天与语言的一周第一天算，名字取语言包）或自己的区间（固定或每次打开时算），夹到 Min–Max 之内，按粒度截成月、季度、周；当前区间那一项按下样式。VisibleMonths 默认 2。
- 顺手修了 WPF PickerPopover 的一个焦点竞争：关弹层时推迟还焦点给按钮，选择器随后把焦点交给"时"那一段，推迟的那次又把焦点抢回按钮（CI 上偶发失败）；推迟执行时改读选择器指定的目标。
- 示例：periods、report、states（上游三个 demo；states 里出差的天数用 Range 算）。

### Tour 与 FloatingGeometry（2026-11-15）

- 浮层贴着目标的定位做成孪生文件 FloatingGeometry（上游 floating 的 computePlacement：想要的一侧放不下换对侧，crossAxisFallback 时再试垂直的两侧，都放不下选溢出最少的；只沿对齐方向挪回、离边 8）。WinForms 原来的 FloatingLayout.Position 就是这套逻辑的平台版，改成换算矩形后调它（提示、弹出框、菜单、下拉照旧），整理在同一个提交里。Avalonia、WPF 的浮层仍用平台 Popup 的定位，Tour 的卡片用它。
- Tour：模态，同各平台的 Dialog（Avalonia、WPF 是盖住窗口客户区的 Popup，WinForms 是半透明的分层窗口加 FloatingSurface，宿主窗体在 Win32 层禁用）。遮罩铺满、目标处挖掉一个 md 圆角的矩形（上游用镂空盒子外一圈巨大的阴影；这里 Avalonia、WPF 用 CombinedGeometry 的 Exclude，WinForms 在位图上用透明色 SourceCopy 填），镂空比目标大 SpotlightPadding（默认 6，上游的 padding）。卡片：surfaceRaised、border 细边、lg 圆角、lg 阴影，宽 min(22em, 窗口宽 − 2em)，四周 lg，封面贴着上、左、右边，标题 H2 md，说明 muted，底栏是步数（sm muted，占满剩下的宽度）与跳过（ghost 中性）、上一步（outline）、下一步 / 完成；卡片在目标的 Placement 一侧（默认下方，离镂空 8，放不下时翻转、再试左右），没有目标时居中。
- 目标是控件（上游的选择器、元素、函数）：Target 或按需取的 TargetFactory；换步骤时 BringIntoView，窗口排版变化时（Avalonia、WPF 的 LayoutUpdated，WinForms 宿主窗体移动、变大小）镂空与卡片跟着动。IsOpen、Current 在 Avalonia、WPF 默认双向绑定；完成发 Finished，OpenChanged 报告原因（跳过 close、完成 confirm、Esc escape）；上游 propsNext 的 disabled 是 CanGoNext。
- 打开后的画面（独立的弹出窗口）截图工具截不到，位置与行为由测试核对。

### KanbanBoard（2026-11-15）

- 建在孪生文件 SortableModel 的分组上（同上游建在 useSortable 上）：列是组，卡片在哪一列由 GetColumn 给；拿起、← / → 换列、↑ / ↓ 调整落点、放下、取消与播报都在模型里，控件只把按键、指针换成模型的调用，按 Active、Target 画。
- 数据由使用方控制：上游的 onMove、onReject、onReorder 是 Moved、Rejected、Reordered 事件；订阅了 Reordered 才能列内排序（ItemsSource 就地改，同 Sortable）。使用方在 Moved 里改数据（ObservableCollection 的增删自动跟上，原地改了属性时调 Refresh）。CanMove 返回原因，经过的列高亮：能放 accent、不能放 danger 并在列头下写出原因，落点处一条 2 像素的插入线。
- 每列：surfaceMuted 底、lg 圆角，列头上下 sm、左右 md（名称 sm 字号 medium 字重，卡片数是 Badge，有卡片时按列的色调），卡片列表左右与下方 sm、卡片之间 sm、竖向滚动（ColumnMaxHeight，不写时窗口高度的 70%，上游的 70vh）。各列至少 ColumnWidth（默认 16em），富余的平分，放不下时整块横向滚动。
- 卡片：surfaceRaised 底、border 细边、md 圆角、四周 sm，内容在左、把手在右上（xxs 内边距的 GripVertical，悬停 fillHover）；键盘拿起时 accent 边框与 md 阴影；指针拖动时原位变淡（0.4），影子卡在覆盖层里跟着指针（lg 阴影、转 1.5°）。卡片按 key 留着，换列、重排时挪动而不是重建，键盘焦点留在把手上（挪动时的失焦不算放下）。
- WinForms：控件不能半透明、画不出阴影，原位的占位是虚线框（内容藏起来），影子卡是放在最上层的副本；把手按上游的大小（1em 图标加四周 xxs），不按最小点击区放大（卡片本身就能拖）；卡片内容的鼠标事件落在最里面的子控件上，按下、拖动挂在每一层上（按钮、输入框不算）。
- 上游“放下后先确认”的示例要用 FormDialog，还没做；示例只有工单看板（列宽 14rem、列高 32rem 按 16 像素的 rem 换算）。

### Sortable 与 SortableModel（2026-11-15）

- 孪生文件 SortableModel（上游 useSortable 里与平台无关的部分，连分组一起：看板 KanbanBoard 要用）：拿起、组内移动、换组、放下、取消、焦点离开，每一步的读屏播报（语言包的 Sortable*），按键换成 SortableKey 交给 Press，指针拖动时按“越过了几个别的项的中线”算位置（IndexAt）后调 DragOver。组内顺序立即写回（Reordered 带上被移动的项：相邻交换时按位移认不出是谁动了），换组只报告 SortableGroups.Moved。
- 数据：上游 `_items` 写回新数组；C# 里 ItemsSource 是可改的列表，就地改（ObservableCollection 用它的 Move，别的 IList 删了再插）并发 Reordered（从第几位到第几位）。Avalonia、WPF 的内容是 ItemTemplate，WinForms 是 ItemFactory 建的控件。
- 每一项按 key 留着、重排时挪动而不是重建：键盘焦点留在被移动的把手上；挪动子控件时会短暂失焦，这期间的失焦不算放下（上游的 reordering）。WinForms 不动子控件集合、只按顺序重排位置，焦点不会丢。
- 外观：surface 底、borderMuted 细边、md 圆角，上下 xs、起始边 xs、结束边 sm；把手 max(1.75em, 最小点击区)，textMuted 的 GripVertical，悬停 fillHover，不出波纹；拿起时品牌色边框、md 阴影、放到最上层（WPF 的阴影在单独的一层，同 Card；WinForms 画不出阴影）。被拖的项跟着指针（位移 = 指针挪开的距离 − 它在排版里的位置变化），拖到外层滚动区域边缘 40 以内时朝那边滚（每次最多 16）。上游别的项的布局动画（平移让位）没有做，直接跳到新位置。
- 读屏：列表、列表项（同级位置与总数），把手是按钮，名称“移动 项名”，说明是操作方法（上游的 aria-describedby）；播报是 assertive（Avalonia、WPF 的实时区域，WinForms 发通知）。上游把手的 aria-pressed 没有对应：拿起的状态在播报里。
- WinForms 的 ScrollAreaControl 补了按内容的首选大小（开 AutoSize 时 FlexControl 按它排：横向滚动时高同内容，纵向时最高到 MaximumSize），横排的列表放进去不用自己算高度。

### VirtualList、InfiniteListStatus 与 InfiniteList（2026-11-15）

- 孪生文件两个：VirtualScroll（VisibleRange、ScrollAlign，按对齐方式算滚动位置、二分查找可见范围：上游 virtualizer 里与平台无关的部分），InfiniteList<T>（上游的 useInfiniteList）。InfiniteList 没有上游的响应式 params：游标与参数由取页函数自己捕获，参数变了调 Restart(防抖) 或 Refresh；immediate 换成新建后调一次 Refresh；enabled 是 IsEnabled；状态变了发 PropertyChanged（Avalonia、WPF 可以直接绑定），Items 每次换新的列表。不管项类型的 IInfiniteList 给 InfiniteListStatus 读。
- Avalonia、WPF 用平台的虚拟化面板（VirtualizingStackPanel，按实际大小排，不需要 estimateSize 与 getKey；overscan 由面板决定）：Avalonia 的项列表是自己的 ItemsControl（主题里没有它的外观，模板在代码里只放 ItemsPresenter），WPF 的模板与面板也在代码里按方向、滚动方式换。每项的外层是读屏的列表项，带 PositionInSet / SizeOfSet（Avalonia 12、WPF 都有），列表层不进控件视图（上游的 role="presentation"）；没有数据时是组。可见范围按建出来的项与视口的相交算。ScrollToIndex 先让面板把它建出来（ScrollIntoView / BringIndexIntoViewPublic），再按它的实际位置用 VirtualScroll.OffsetFor 对齐。
- WPF 的虚拟化面板只在自己是滚动的那一层时虚拟化：Window 模式（随外层 ScrollViewer 滚动）会建出全部项，文档里写明。WPF 里 `Grid` 解析成 Rune 的 Grid（自动分栏、按无限高量子元素，滚动区域拿不到视口），这里写全名 System.Windows.Controls.Grid。
- WinForms 没有平台的虚拟化：VirtualListControl 自己按 EstimateSize 排、只为可见范围（前后多 Overscan 项）调 ItemFactory 建控件，建出来后按 GetPreferredSize 量，可视区上方的项大小变了时把滚动位置挪同样多（上游 virtualizer 的做法），滚出范围的项释放。窗口坐标有上限（约 3.2 万像素），上万项不能做成按总高度撑开的控件：给 ScrollAreaControl 加了内部接口 IVirtualScrollContent，实现它的内容只占视口大，总大小与滚动位置由滚动区域交给它（Listbox、Tree 的画行视图以后也该接上）；Window 模式要撑开，受这个上限限制。读屏的列表项没有同级位置的属性，说明里写“第几项，共几项”。
- InfiniteListStatus：失败时是 danger 的“加载失败，请重试”与重试按钮（sm、ghost、RotateCw），取完了（有项、不在加载）是“没有更多了”（NoMoreText 换文字，ShowNoMore = false 不显示），其余时候不占位；有内容时至少 2.75em 高、上下 xs、左右 md、sm 字号。上游按错误码取文案（errorCodeText），桌面上没有错误码的语言包，一律用笼统的提示。读屏：Avalonia 是礼貌的实时区域，WPF 发 LiveRegionChanged，WinForms 发通知。
- WPF 顺手修了一个初始化顺序的问题：ButtonBase、Rating、Slider、SignaturePad 用 UIPropertyMetadata 替代 IsEnabled 的元数据，平台的 Primitives.ButtonBase（滚动条里的 RepeatButton）先初始化时会抛异常；改成 FrameworkPropertyMetadata（单独的提交）。

### JsonViewer（2026-11-15）

- 纯计算是孪生文件 JsonViewerNodes（上游的 kindOf、childrenOf、sizeOf、display、copyText）：值可以是 System.Text.Json 的 JsonElement、JsonNode，也可以是任意 .NET 值。上游的普通对象在 C# 里是键为字符串的字典、没有自己写 ToString 的类与结构、记录、匿名类型（公开属性）；Map 是键不是字符串的字典，Set 是 ISet / IReadOnlySet，其他可枚举的集合是数组；Decimal、bigint 对应 decimal、BigInteger（按固定区域照实显示），Date 对应 DateTime、DateTimeOffset、DateOnly（同 toISOString：UTC、毫秒、Z；不知道时区的 DateTime 不加 Z）；自己写了 ToString 的（Uri、Guid、TimeSpan、枚举）是 other；没有 undefined。循环引用按引用比较经过的对象。
- 字符串的写法同 JSON.stringify（只转义引号、反斜杠与控制字符，中文原样），路径同上游（标识符用点，别的写成 ["…"]，Map 是 .get(键)，Set 是 [...路径][序号]）。复制的值：容器用 Utf8JsonWriter 按节点写成缩进两格的 JSON（数字照实写成原文，Map 写成对象、Set 写成数组，循环引用写成 "[Circular]"，不转义中文）。
- 控件建在各平台的 Tree 上：GetKey 是路径、GetLabel 是“键名: 值”（读屏、按字母跳转、过滤按它），选择方式单选、缩进 1em，树最高 TreeMaxHeight（上游的 maxHeight，默认 24em；控件自己的 MaxHeight 包括工具条与路径栏），toolbar 叫 ShowToolbar。见过的节点在取子节点时按路径记下，路径栏按选中的路径找回节点。
- 一行：Avalonia、WPF 是树的 ItemTemplate（WPF 的 JsonViewerRow 按数据上下文建），键名（序号是注释色）、值（按类型着色、放不下省略）、容器后面的项数；WinForms 的行是画的，给 TreeControl 加了内部的 PaintContent（画文字那一块），三段按同样的颜色用 TextRenderer 画，匹配的加粗。
- 等宽字体关掉连字（上游 fontVariantLigatures: none，免得 >= 画成 ≥）：Avalonia 是 FontFeatures 的 -calt、-liga，WPF 是 Typography 的 StandardLigatures、ContextualAlternates；WinForms 的 GDI 关不掉 OpenType 特性，用 Cascadia Mono（从 CodeBlockControl 挪进 Fonts.CodeMono，整理成单独的提交）。
- 路径栏：sm 字号、至少 2.25em 高、textMuted；选中前是提示，选中后是路径（等宽、放不下省略、悬停看全文）与“复制路径”“复制值”两个 CopyButton（图标一样，提示与读屏名称说明各复制什么）。

### Tree（2026-11-15）

- 纯计算是孪生文件 TreeData（上游 utils/data/tree 的 flattenTree、buildTreeIndex、toggleChecked、checkedByStrategy、filterTree、buildTree、moveTreeNode，加上 useTreeExpansion 的 TreeExpansion）：节点是任意对象，经 TreeAccessors（GetKey、GetChildren、HasChildren）读取；勾选状态是 CheckState（未勾、勾上、半选），按 CheckStrategy 收起；Build 遇到重复的 key、环时抛 ArgumentException。三个平台的控件只管展示与输入。
- 数据：上游的 getKey、getLabel、getChildren、hasChildren、loadChildren、isDisabled 在 C# 里是委托属性（isDisabled 叫 IsItemDisabled，判断节点；WinForms 的 hasChildren 叫 HasChildNodes：控件自己有 HasChildren）。Expanded、Selected、Checked 是 key 的列表（Avalonia、WPF 默认双向绑定；WinForms 报告各自的 Changed）。懒加载：LoadChildren 返回任务，结束前那一行显示加载图标；子节点放进数据后调 Refresh（WinForms 是覆盖的 Control.Refresh）；失败时收回去并报告 LoadError（没有订阅时写进跟踪日志）。
- 行是平铺的（上游虚拟滚动不能嵌套 role="group"），层级与同级位置给读屏：Avalonia、WPF 是虚拟化列表（ItemsControl 加虚拟化面板）里的 TreeItem，自动化对等体实现 ExpandCollapse、SelectionItem、Toggle；WinForms 的行是画出来的（每行一样高、只画看得见的），读屏是大纲（Outline），子项是看得见的每一行。上游的 renderItem 在 Avalonia、WPF 是 ItemTemplate，WinForms 没有自定义内容（要多显示的写进 GetLabel）。
- 行高：控件高度与最小点击区（minTarget）取大，上下各留 1；缩进每层 Indent（默认 1.25em），展开箭头 1.5em 见方（展开时转 90°，从右到左时镜像），勾选框 1em、右边多留 xs。状态同列表行：悬停、键盘高亮 fillHover，选中 accentSoft 底、accentText 字，不可用变淡；过滤匹配的加粗。焦点环画在树的边上往里收（上游 focusRingStyles.inset）。
- 高度：按内容高，写了 MaxHeight（WinForms 是 MaximumSize 的高，不写时最高 20em）时节点多了在树里滚动。没有节点时：IsLoading 显示四条骨架（宽度依次递减），否则是空状态（文字取语言包，过滤时是“没有匹配”，Avalonia、WPF 的 Empty 可以换内容，WinForms 是 EmptyText）。
- 键盘同 APG 的树：↑↓、Home、End，→ 展开 / 进入第一个子节点，← 收起 / 回到父节点（从右到左时对调），Enter 选中（没有选择方式时展开或收起），空格勾选或选中，按字母跳转（同 Listbox 的 Typeahead）。
- 移动：订阅了 Moved 才能拖动（挪开 4 以上），落在一项的上四分之一放在前面、下四分之一放在后面、中间放进里面（accentSoft 底加 accent 框；前后是一条从那一层缩进处开始的线），拖到上下边缘时滚动；Alt + 方向键同上游。目标按移走之后的同级算，CanDrop 限制落点，放进自己的子孙里不行；移动后读屏念“移到哪里第几位”。新数据由使用方用 TreeData.Move 生成、交回 ItemsSource。
- WPF 的测试里模拟不了修饰键，Alt + 方向键留了内部方法 MoveByKey；WinForms 的 Click 与 Layout 与 Control 的成员重名，内部方法叫 ClickItem、Parts。

### SignaturePad（2026-11-15）

- 笔画的计算是孪生文件 SignatureStrokes（上游 SignaturePad 里的 signature_pad 式算法）：按速度定笔宽（写得快变细，速度加权平滑），近于 1.5 的点不记，一笔拆成穿过相邻两点中点的二次贝塞尔段，导出 SVG（读回的旧签名嵌成图片）。平台控件只把指针换成 Begin、Extend、End，每次整笔重画（笔画不多，不用像上游那样只画新多出来的一段）；不取合并掉的中间点：它们没有各自的时刻，速度会算错。
- 值是透明底的 PNG 字节（上游是 data URL，桌面上存文件、进数据库都用字节），Avalonia、WPF 默认双向绑定，WinForms 报告 ValueChanged；自己交出的值回来时不当成“换了签名”，别的值（编辑页读回的旧签名）画在底下，可以接着写或清空。handle 换成控件上的 ToPng、ToSvg、Undo、Clear；name（表单提交）不移植。
- 上游的 minWidth、maxWidth 与控件自己的排版属性重名，叫 PenMinWidth、PenMaxWidth；height 就是控件的 Height（不写时 10em）。外框同输入框（描边随出错、只读、悬停，跟所在 Field），底色与墨色是纸面：从 QRCode 里抽出各平台的 Paper（深色主题下仍白底近黑），整理成单独的提交。
- WPF 的测试里模拟不了鼠标位置，写字的入口留了接收坐标的内部方法 Write；WinForms 的笔画按逻辑像素记、画时按 DPI 放大（SVG 与另两个平台一致），GDI+ 只有三次贝塞尔，二次的控制点换成两端到它的 2/3；控件不能半透明，不可用时颜色减半。

### Toast：ToasterController、Toaster（2026-11-15）

- 控制器是孪生文件 ToasterController（上游的 createToaster）：TimeProvider 计时（测试里换成 FakeTimeProvider），计时器的回调回到建它的线程上执行（同 LoadingBarController）；列表与每条的变化经 Changed 通知。上游的 options 是对象展开、update 用 Object.assign 合并：桌面上 ToastOptions 是记录，不写（null）就是没给；Update 只换写了的，要去掉某一项时用同一个编号 Show 换掉整条。标题是文字（三个平台的 Alert 标题都是字符串），正文、操作同 Alert（WinForms 的正文是文字、操作是控件）；icon: false 是 ShowIcon = false；duration: Infinity 是 Timeout.InfiniteTimeSpan；setLimit 是 Limit 属性；promise 收 Task 与三个阶段（成功、失败由结果或异常算出）。上游 Toast.tsx 的单元测试测的就是控制器，parity 里把 Toast 对到 ToasterController。
- 桌面上没有 position: fixed 与 Teleport：Toaster 放在盖住整个窗口的 Grid / Panel 里（Avalonia、WPF），区域停在一角（四周 lg、宽 min(26em, 可用宽)），空白处不挡点击；WinForms 的 ToasterControl 用 FloatCorner（从 FloatButton 挪到 Utils/Layout，改成按位置摆）停在父控件客户区的一角并放到最上层，大小正好包住通知，窗口区域（Region）裁到每条通知的圆角矩形，空白处透出下面的界面。模态对话框打开时上游把区域移进最上层的模态；桌面的对话框是弹出层（另一个窗口），通知在它下面，不做。
- 每条通知是 Alert（surface，lg 阴影：Avalonia 的 BoxShadow、WPF 的 Effect；WinForms 画不出阴影），重要的 Live 是 assertive，其余 polite（Alert 自己播报）。能关闭时订阅 Alert 的 CloseRequested（订阅了才有关闭按钮）。WinForms 的 AlertControl 写过图标就回不到“按色调选”，要按色调选时换一个新的。
- 进出场：进场淡入并从所在的边滑进 0.75em（md、enter）；离场淡出（sm、exit）、高度收起（md，间距在每一项里一起收掉）后调控制器的 Remove。Avalonia 是自己的 double 属性加 DoubleTransition（位移、缩放合成一个 TransformOperations），WPF 是“基础值写终点、从当前值起播”的 DoubleAnimation，WinForms 是 Animator 补间的 Signal（没有淡入淡出）。叠放：最新的一条在原位，更早的叠在它后面（每深一层错开 0.75em、缩小 5%，第三层之后隐藏），悬停或聚焦时展开；WinForms 不能缩放，用变窄代替。
- 计时暂停：指针在通知上（Avalonia、WPF 是区域的指针进出；WinForms 的指针停在子控件上时自己收不到鼠标事件，定时看指针的位置）、焦点在区域里、窗口不在前台（上游是页面切到后台）。剩余时间条按这一轮计时的总时长缩短，暂停时停住：Avalonia 用 RequestAnimationFrame，WPF 用 CompositionTarget.Rendering，WinForms 用定时器。
- F8：Avalonia 在顶层元素上隧道接按键，WPF 在窗口的 PreviewKeyDown，WinForms 用消息过滤器（所在窗体在前台时）。焦点在一条通知里时 Esc 关闭（WinForms 在 ProcessCmdKey 里）；关闭后焦点交给相邻的通知，没有时回到按 F8 之前的位置。横向拖动关闭：挪开 4 以上跟手（Avalonia、WPF 透明度随距离），超过宽度的 35% 滑出后关闭，不够就弹回；按在按钮、输入框上的不算。

### ImageCropper（2026-11-15）

- 裁剪框按宽高比放在区域中间、四周至少留 FrameInset，图片在底下平移、缩放，框里永远是图：建在孪生文件 PanZoom 上，给它加了上游的 frame（PanZoomRegion，平移限制按框算）与 cover（缩放下限是盖满框所需的比例）。框、盖满的缩放、初始状态、框里是图片的哪一块、输出的像素大小是孪生文件 ImageCropGeometry（连同 ImageCropShape、ImageCropFileType、ImageCropOutput、ImageCropFormat）。
- 上游的 handle（toCanvas、toBlob、toFile、reset）换成控件上的方法：ToBitmap（平台的位图：Avalonia 的 RenderTargetBitmap、WPF 的 BitmapSource、WinForms 的 Bitmap）、Encode（PNG 或 JPEG 的字节；Avalonia 12 的 Bitmap.Save 有 JpegBitmapEncoderOptions，WPF 用 Png/JpegBitmapEncoder，WinForms 用 GDI+ 的编码器）、Reset。webp 三个平台都没有现成的编码器，不做；存文件、交给上传由调用方做。
- 区域是自绘的：surfaceMuted 底、md 圆角，图片按状态画，框外压暗（黑 50%），框留一条白 80% 的细边（圆形是椭圆），键盘聚焦时一圈焦点环（WinForms 画在框里面一圈）。上游的 height（默认 18rem）叫 ViewportHeight（默认 18em；控件自己的 Height 包括工具条），padding 叫 FrameInset（WinForms 是逻辑像素，按 DPI 放大）。
- 圆形导出：Avalonia、WPF 推一个椭圆裁剪再画；WinForms 的裁剪区域没有抗锯齿，先裁成矩形、再用它当画刷填抗锯齿的椭圆。GDI+ 的高质量插值在边上会取到图片外面的透明像素（矩形四角半透明），画的时候用镜像平铺（ImageAttributes 的 TileFlipXY）。
- 图片原本的大小：Avalonia 用 Bitmap.Size，WPF、WinForms 按像素（不按图片里记的 DPI）。Avalonia 的 headless 测试不真解码、不真编码，像素与编码的字节在 WPF、WinForms 的测试里核对。
- 上游的头像示例配 Upload 的 transform（选好图片先弹出裁剪），Upload 还没做，示例改成直接裁一张图纸、点按钮出头像。

### Carousel（2026-11-15）

- 上游是浏览器的横向滚动加 scroll-snap：桌面上张片排成一条（每张宽 =（宽 − (SlidesPerView − 1) × Gap）÷ SlidesPerView），按位移切换，过渡 lg、系统关闭动画时直接到位。拖动（挪开 3 以内不算）跟着指针，松开吸附到最近的一张；滚到底算最后一张。Index 是最前面那张的序号，一次看几张时最大是 ceil(张数 − SlidesPerView)，页点也只有这么多。
- 数据：Avalonia、WPF 是 ItemsSource 加 ItemTemplate（上游的 items 与 children 渲染函数；控件原样放进去），Index 默认双向绑定；WinForms 的张片是控件，直接放进 Slides（Collection，增删时重建），Index 变了报告 IndexChanged。getKey 不移植。每张包在一个组里（读屏名称“第 2 张，共 5 张”）：重建时旧的那张先放手，控件才能放进新的。
- 位移：Avalonia 是 Track 自己的 Offset 属性加 DoubleTransition（RenderTransform 的动画不支持），WPF 是 DoubleAnimation（基础值先写成终点，动画结束后撤掉，拖动时直接改基础值），WinForms 是 Animator 补间的 Signal，每帧重排子控件。滚轮：Avalonia 读横向的 Delta（触摸板横滑）或 Shift + 滚轮；WPF 的滚轮事件只有竖向，只有 Shift + 滚轮；WinForms 在 WndProc 里接 WM_MOUSEHWHEEL（子控件不处理时转给父窗口），另有 Shift + 滚轮。WinForms 的鼠标事件落在张片里的子控件上，拖动挂在每一层子控件上（挂过的不再挂）。
- 自动播放：Autoplay 是间隔（TimeSpan?，DefaultAutoplay 是 TS 的 true 即 5 秒）；Avalonia、WPF 用 Task.Delay 加版本号（headless 测试里计时器不走），WinForms 用 Forms 的 Timer。指针在上面、键盘焦点进来时暂停：Avalonia 看聚焦的方式（Tab、方向键），WPF 看最近的输入设备是键盘，WinForms 看进来时没按着鼠标且窗口显示焦点提示（窗口刚打开时焦点自动落进来不算）；WinForms 的指针停在子控件上时自己收不到鼠标事件，到点时看指针的位置，在上面就这一轮先不换。
- 页点：点击区域 1.25em × 1.5em（至少 24），中间的小圆点 0.5em，当前的拉宽到 2em、圆点 1.25em、accent，宽度 md 的过渡；悬停 textMuted。上游的 aria-current 在 Avalonia 是 :current 伪类，WPF、WinForms 是 IsCurrent（内部）。
- 外观：两侧按钮是 surface、中性色、圆形的 IconButton，md 阴影（Avalonia 的 BoxShadow、WPF 的 Effect；WinForms 画不出阴影）；控制条圆角 full、surface 75% 的底（上游的 4px 背景模糊桌面上没有）。WinForms 的子控件各有窗口，透明时透出的是轮播外面的底，按钮与控制条用 Region 裁成圆角（没有抗锯齿）。

### SelectionBar（2026-11-15）

- 上游只在 DataTable 的“台账”示例里用到；数据表格还没做，示例挂在 DataTable 页下（selection-bar），配静态的 Table 与每行的勾选框。
- 上游的 onClear 叫 ClearRequested（只报告，清空选择由使用方做）；批量操作的按钮放在 Actions。数目变成 0 时淡出后不占位，淡出期间数目停在最后一次（Avalonia、WPF 用 Task.Delay 等淡出结束：headless 测试里计时器不走）；WinForms 不能半透明，直接藏起来。
- 上游经全局的播报区念数目（栏随第一次选择出现，挂在里面的 aria-live 第一句念不出来）；桌面的读屏通知不受这个限制：Avalonia、WPF 用栏里的 LiveRegion，WinForms 用 RaiseAutomationNotification。
- 字号 sm，内边距、圆角、最小高度（控件高度）都按 sm 的 em 算。WinForms 的 Control 的 DisplayRectangle 不扣 Padding，内容在 OnLayout 里摆进内边距。

### FieldCompare（2026-11-15）

- 上游的字段沿用表格的列定义（key、header、type、format、statuses、empty、value、cell）并用 ColumnTable 排表格；我们还没有数据表格的列定义，字段是独立的 CompareField 记录：Key（取同名属性，或字典的键）、Header、Value 取值函数、Format（孪生的 NumericFormat，同 Intl：金额、百分比、缩放）、Statuses（StatusMap）、Empty、Cell（记录与哪一份，返回显示的内容）、Better。上游的 type 由 Format 表达：金额是 Currency、百分比是 Percent（百分比字段的变化量就是百分点，不给变化率）；日期按短日期显示。
- 计算是孪生文件 FieldComparison（diffFields 与格式化）：数值换成 decimal 精确相减，变化率 = 差 ÷ 原值；变化量带正负号，后面全角括号里是变化率；色调按 Better（没写或没变 muted，变好 success、变坏 danger）。
- 表格用已有的静态 Table（紧凑密度）按行生成：字段、原值（淡显）、新值（改了的中等字重，没改的淡显）、变化（有数值改动才有这一列）；改了的行 Avalonia 加类 changed，WPF、WinForms 的 Tag 是 "changed"。列表是“字段名 | 值”两列（WPF 的 Grid 没有间距，用外边距；WinForms 自己排，字段名一列取最宽的），值是 ValueChange 加变化量。WinForms 的 ValueChangeControl 只放文字，状态在列表里显示名字；WinForms 的排法属性叫 CompareLayout（控件自己有 Layout 事件）。

### Affix（2026-11-15）

- 桌面没有 CSS 的 position: sticky。Avalonia、WPF 照 sticky 的语义做：不脱离排版，所在的 ScrollViewer（Target，不写时是外层最近的）滚动、排版变了时算原来的位置，滚过 OffsetTop（或贴底时的 OffsetBottom）就用 RenderTransform 的位移钉在滚动区域里，位移不超出父元素的范围（父元素滚出去时跟着走）；钉着时 ZIndex 抬到兄弟元素上面（后面的兄弟会盖住它）。阴影 md 画在包着内容的那一层（Avalonia 的 PART_Root 的 BoxShadow，WPF 的 Effect）。
- WinForms 的控件不能画到自己外面：贴住时把内容挪到 ScrollAreaControl 上面一层、按同样的规则摆，固钉自己按内容的大小留着空位，离开时内容回到原位（同 Ant Design 的 fixed 加占位）。外层的滚动区域可能后来才套上，建句柄时再找一次；画不出阴影。
- 上游的 onChange(stuck) 叫 StuckChanged，状态是 IsStuck；没有哨兵：贴住与否就是位移是不是 0。

### FloatButton、FloatButtonGroup、BackTop（2026-11-15）

- 桌面上没有 position: fixed：单独用的浮动按钮（组）靠所在面板的结束边与底部、外边距 lg。Avalonia 是默认样式的对齐与外边距（em），WPF 在没写对齐、外边距时代码里设，WinForms 的 FloatCorner 跟着父控件变大小摆到客户区角落并放到最上层；放在盖住页面的 Panel / Grid（WinForms 的父控件）里就浮在内容上。组里的按钮跟着组排。
- FloatButton 是组合：IconButton（surface、中性色、圆形、lg）包在圆形重叠的 Indicator 里。上游落到 IconButton 上的属性写在 Button 上（FloatButton.Button，同 TS 的 rest）；badge 拆成数字的 Badge 与小圆点的 BadgeDot；tooltip 叫 Tip，false 是 ShowTip = false。名称写在 FloatButton 上，转给里面的按钮；FloatButton 自己不进读屏的树。阴影 md：Avalonia 是按钮的 BoxShadow，WPF 是按钮的 Effect，WinForms 的控件画不出阴影。
- 速拨：主按钮是组的 Trigger（TS 的 propsTrigger），读屏报展开、收起（Avalonia 是主按钮自己的 ExpandCollapse 对等体，WPF 用按钮家族已有的展开状态，WinForms 是 ButtonControl.Expanded）；展开的那一列淡入（WinForms 直接显示），收起时不占位、不能聚焦；Esc 收起并聚焦主按钮，点组外面收起（Avalonia、WPF 盯窗口的按下，WinForms 用消息过滤器）。WinForms 的组里的按钮放在 Buttons，主按钮总在最下面。
- BackTop 从 FloatButton 派生：滚动容器是 Target（Avalonia、WPF 的 ScrollViewer，WinForms 的 ScrollAreaControl），不写时找外层最近的，再没有时是窗口里的第一个（TS 的整页）。平滑滚回顶部用动画驱动的进度（lg），动画结束时进度回到基础值，所以只在播放期间跟着滚、结束时直接到顶；滚到顶之后再聚焦开头第一个能聚焦的元素（先聚焦的话平台把它滚进视野，就跳过了平滑滚动）。WinForms 的控件不能半透明，直接出现；Threshold 按 96 DPI 算。

### Image、ImageGroup、Lightbox（2026-11-15）

- 地址是 Uri（上游的 src 叫 Source，同 Avatar）：要有读的状态（扫光的占位、读完淡入）与读不了的占位，所以不收已经解码的图片。读图集中在各库的 ImageLoader：avares:// 或 pack://、file://、http(s)://、base64 的 data:，相对地址按程序所在目录；后台解码，最近 32 张留在内存里（缩略图点开大图、Lightbox 预先读的前后两张不再读一遍）。sources（picture 的备选源）是浏览器的响应式图片，不移植。
- 平移与缩放是孪生文件 PanZoom（上游的 usePanZoom：zoomAround、clampPan、fitTransform 与控制器），平台控件只把指针、滚轮、双击、按键换成它的调用；滚轮按一格的 delta 取 e 的指数（同上游按 deltaY 连续缩放）。上游的 wheel: 'pan'、filter 是画布、流程图用的，做到时再加（frame、cover 随 ImageCropper 加了）。
- Lightbox 从 Dialog（WinForms 是 DialogControl）派生：模态、焦点、Esc、还回焦点、BeforeClose、命令式 ShowAsync 都是对话框的。面板换成自己的内容、铺满窗口，背景 scrimStrong、文字 onScrim。Dialog 的 RequestClose 改成给子类用（工具栏的关闭按钮问 BeforeClose）；WinForms 的 DialogControl 加了 Flat 钩子（FloatingSurface 的 Elevation.None：没有阴影、描边、圆角），面板是不透明的窗口，scrimStrong 叠在主题背景上画。
- 打开、换一张、旋转、还原之后整张放进视口：等图片区排好版再做（Avalonia、WPF 在 LayoutUpdated 里；排版途中改比例文字会让工具栏按旧的测量排）。原大按图片的像素算（WPF 不按图片里记的 DPI，WinForms 的一个像素是 1/96 英寸跟着 DPI 放大）。Avalonia 的 TransformOperations 按追加的顺序作用在点上，与 CSS 的 transform 列表相反：先追加缩放、再追加平移。
- ← / → 在面板里哪里按都算（WinForms 在 ProcessCmdKey 里，不让窗体拿去移动焦点），+ / − / 0 / ↑ / ↓ 只在图片区；上游视口里的 ← / → 会同时平移和切换，我们只切换。下载按钮弹出平台的保存对话框，存原始字节。
- Image 能预览时是按钮（Invoke / 默认动作打开），不在 ImageGroup 里时有自己的 Lightbox，用 ShowAsync 挂在图片下面打开；ImageGroup 从 Flex 派生（换行、间距 sm），打开时按界面里的先后顺序收集组里写了 Preview 的图，共用的预览就是 ImageGroup.Lightbox（上游的 propsLightbox）。WPF 的 Image 比格子大时默认从左上角排，图片居中对齐才同 object-position 的默认。
- 示例的图纸是上游示例的 SVG（tools/example-assets/drawings.mjs 用 Playwright 截成 PNG，放在 docs/site/drawings），三个示例项目复制到输出目录。inspect 截图前同 ts-shots 把时长 token 设为 0（减少动态效果），淡入不会截到一半。
- 顺带修了 WinForms 的两个老问题：不在界面里的命令式对话框（ShowAsync、ConfirmAsync）找不到宿主窗体、面板没有显示（FloatingSurface 现在顺着 IAnchored 借的 owner 找）；ConfirmAsync 在释放对话框之后才读理由，读到空字符串。

### Combobox、Autocomplete（2026-11-15）

- 同 TagsInput：输入框就是控件自己（Avalonia、WPF 从 TextInputBase 派生，WinForms 从 TextInputControl），上游的 inputValue 是平台的 Text，选中项的值是 Value；filter={false} 叫 LocalFilter=false，load 是 Load（同 Select 的 OptionsLoader 与 RemoteOptions，打开时与输入停下 300 毫秒后请求、只采用最后一次、可分页）。
- 选项弹出层抽成 ListPopover（同上游的名字）：TagsInput 与 Combobox 共用，三个平台各一份（Avalonia 的 Popup、WPF 带阴影层与平台定位回调的 Popup、WinForms 不激活的 FloatingSurface 与"显示期间的失焦、失活不算"的守卫），整理成单独的提交。
- 组件自己写 Text（选中后填入选项文字、父级改了 Value、选项后到）时用一个标记区分，不当作用户输入：用户输入才改过滤词并打开列表。键盘照 TagsInput 在 TextBox 之前接住（WinForms 的内层 TextBox 把开着时的回车、翻页与要清空时的 Esc 当输入键）；输入法组合中的键不处理。
- 展开按钮是 IconButton，部件名 PART_Chevron（上游的 propsChevron），不进 Tab 顺序；列表真的弹出时箭头翻转（Avalonia 的 :open、WPF 的 IsShown，WinForms 换成向上的箭头）。WinForms 的空状态只能换说明文字：上游的 empty 叫 EmptyText，没有自定义选项内容。
- Autocomplete 是 Combobox 的子类，只改默认值：允许自定义值、没有展开按钮、没有可选的项（也不在加载）时不弹出；值就是 Text。表单字段名（name）与弹出位置（placement）不移植。

### Drawer（2026-11-15）

- 内核同上游（ModalCore）：Drawer 从 Dialog（WinForms 是 DialogControl）派生，遮罩、焦点、Esc、点遮罩、BeforeClose、确认、命令式 ShowAsync 都是对话框的。为此 Dialog 把面板的摆放拆成给子类的钩子（量面板的约束、面板的矩形、面板排好之后、圆角、刚显示出来；WinForms 是 PanelBounds、SquarePanel、ArrangeExtras），行为不变；Avalonia 的 Dialog 部件样式改用 `:is(rune|Dialog)`，派生类也适用。
- 位置：左右两侧占满窗口高、宽度按 Size 档位（默认 xs）或 Width，上下两侧占满窗口宽、高度按内容；贴边的一侧没有描边、没有圆角。上游的 width 是控件自己的 Width（Avalonia、WPF 默认双向绑定：抽屉自己不参与排版，Width 正好空着），minWidth 是 MinWidth（默认 48）；WinForms 的控件尺寸照样会被设计器序列化，改叫 PanelWidth、MinPanelWidth，另有 PanelWidthChanged。
- 宽度调整手柄就是 ResizeHandle：给它加了内部的长度读写钩子，改的是抽屉面板的宽度而不是父元素；手柄放在弹出层里、贴着面板靠正文的一边，最多拖到窗口宽减 48，双击回到打开时的宽度。拖窄以后内容保持 20em（不超出窗口减 3em），被面板裁掉。
- 滑入：Avalonia 动画一个附加的进度属性换算位移，WPF 是面板与阴影层共用的 TranslateTransform（durations.lg、enter；系统关闭动画时不播），离场不播（Dialog 也没有离场动画）。WinForms 的面板是两层窗口，没有滑入动画；直角由 FloatingSurface.Show 的 square 画，贴边一侧的描边照常画在窗体边上。

### TagsInput（2026-11-15）

- 同上游：输入框就是控件自己。Avalonia、WPF 的 TagsInput 从 TextInputBase（平台 TextBox）派生，模板里图标、标签、文字区排在一个会换行的 Flex 里（列间 sm、行间 xs，文字区至少 6em），清空按钮靠结束边；标签是代码按值插进那一排的 surface 样式 Tag（按值复用，焦点在移除按钮上时不丢），只读、不可用时不订阅 RemoveRequested，就没有移除按钮。WinForms 的 TagsInputControl 从 TextInputControl 派生，标签是 TagControl 子控件，按"图标、标签、输入框、清空按钮"重排 Controls 的顺序。
- 上游的 _value 是 Values（同多选 Select），inputValue 是平台的 Text；filter={false}（异步取建议）叫 LocalFilter=false，Filter 是自定义的匹配函数。拆分、文字换成值、去重与上限在孪生文件 TagsText；默认分隔符 , ， ; ；，换行与制表符总是分隔。
- 键盘在 TextBox 自己处理之前接住：Avalonia 覆写 OnKeyDown 先判断，WPF 用 PreviewKeyDown，WinForms 转到外壳的 OnKeyDown（SuppressKeyPress），并让内层 TextBox 把回车、开着时的 Esc 当作输入键（不触发窗体的默认、取消按钮）。输入法组合中的键是 ImeProcessed / ProcessKey，不处理。粘贴：Avalonia 拦 PastingFromClipboard 再读剪贴板（TryGetTextAsync），WPF 用 DataObject.Pasting，WinForms 拦 WM_PASTE；只有一段时照常粘贴。
- 建议列表照 Select：Avalonia、WPF 是 Popup 里不可聚焦的 Listbox，WinForms 是不激活的 FloatingSurface 两层窗口，焦点一直在输入框里。WinForms 显示浮层窗口时平台可能把焦点拿走一下、让宿主窗体失活：显示期间的失焦与"点了外面"不算，显示完把焦点还给输入框。从建议里选的也受 Max 限制：拒绝时把列表的已选退回去（WPF、Avalonia 要给新的列表实例，列表才重新同步）。
- 失焦时加入没确认的文字只认输入框自己失焦（焦点移到标签的移除按钮上也算，同上游的 blur）。表单字段名（name）与弹出位置（placement，固定下方起始边对齐，同 Select）不移植；WinForms 的建议没有自定义内容模板。

### Splitter、ResizeHandle（2026-11-15）

- 比例的计算（夹在范围里保留一位小数、拖动换算、按键的目标、收起时读屏的 0 / 100、两栏按比例分、收起按钮的箭头方向、手柄的长度与方向）在孪生文件 SplitterScale，同 TS 的网格模板：分隔条占 space.md，剩下的按 size : 100 − size 分，收起的一栏不占位置。拖动的比例按整个分栏的长度算（含分隔条，同 TS 量 root 的宽度）。
- Avalonia、WPF 的 Splitter 是代码组合的：表面 Border（Bordered 时 1 像素边框、lg 圆角、surface 底，裁掉子元素）里一个排版面板放两栏的 ScrollArea（两个方向）、分隔条 SplitterHandle 与收起按钮 IconButton（最后加，叠在线与两栏上；不进 Tab 顺序，键盘在分隔条上按 Enter）。分隔条自己画线：平时 xs、borderMuted，悬停、拖动 md、borderStrong，键盘聚焦 md、强调色，粗细有 sm 的过渡；读屏是分隔符，RangeValue 的取值是比例。Avalonia 实现 ICustomHitTest（透明处也能按）；WPF 的线色引用资源（换主题跟着变），指针逻辑收进 PointerDown / PointerMove / PointerUp。收起按钮要不透明的 surface 底遮住线、悬停 fillHover：Avalonia 是 SplitterParts.axaml 里只选分栏自己的按钮的样式，WPF 是部件样式里排在配方后面的触发器。
- WinForms 的 SplitterControl：上游的 size 叫 Ratio（Control 自己有 Size），范围叫 MinRatio、MaxRatio，另有 RatioChanged、CollapsedChanged 事件；分隔条叫 Divider（Control 自己有 Handle）。WinForms 的子控件不随 RightToLeft 镜像，从右到左时自己把起始一栏排到右边、拖动方向取反；Bordered 时两栏按圆角裁（CornerClip）。收起按钮的底色用 ButtonSx，悬停时换成 fillHover。
- ResizeHandle 放在要调整的元素里，改的是父元素的 Width（横线时 Height），值夹在范围里、取整像素，再触发 LengthChanged；上游的 width、minWidth、maxWidth、defaultWidth 叫 Length、MinLength、MaxLength、DefaultLength（Width 这些是控件自己的布局属性）。Avalonia、WPF 要放在 Panel / Grid 里叠在内容上，自己按 Edge、Orientation 设对齐贴边；WinForms 的 ResizeHandleControl 跟着父控件的排版自己摆到边上、放到最上面。拖动按父元素的容器（WinForms 按屏幕）的坐标量：父元素在拖动中变大，贴着起始边时它的原点也在动。
- 粗指针（触屏）扩命中区是网页的事，桌面上不做；上游的 usePersistentState 记比例，桌面上交给使用方（双向绑定或 RatioChanged）。

### OtpField（2026-11-15）

- 同上游（input-otp 的做法）：只有一个真正的输入框，格子只是显示。Avalonia、WPF 的 OtpField 从 TextInputBase（平台 TextBox）派生，模板是自绘的 OtpSlots 加一个透明的文字层（Avalonia 的 TextPresenter、WPF 的 PART_ContentHost，前景、光标、选中都透明）；粘贴、退格、输入法、撤销、读屏的编辑框、字段的标签与出错都是现成的。WinForms 的 OtpFieldControl 自己画格子，真正的输入框是放在控件外面（看不见）的 TextBox，焦点转给它，读屏名称跟着字段的标签。
- 值是 Text（同 Textarea）；上游的 type 叫 Charset（Numeric、Alphanumeric）；onComplete 是 Completed 事件（只在变成填满时触发一次：过滤造成的二次改写不重复触发）。过滤在值变了时做（输入法组合中的字不在 Text 里，组合结束才过滤），先过滤再截到 Length 位，不用 MaxLength（会先按原文截断，粘贴"123-456"丢一位）。
- 格子的外壳颜色同 TextField（surface / soft 的 fill、border / borderStrong 悬停 / borderMuted 只读 / danger 出错），当前格画 2 像素焦点环，空的当前格里光标每半秒闪一次（系统关闭动画时常亮）。Mask：Avalonia 的 PasswordChar、WinForms 的 UseSystemPasswordChar；WPF 的 TextBox 没有密码字符，只是格子显示圆点。

### QRCode（2026-11-15）

- 编码器照上游 utils/qr/encode 移植成孪生文件 QrEncoder（ISO/IEC 18004，字节模式、四档纠错、最小版本、罚分最低的掩码），另加 SvgPath / Svg 导出（桌面示例的"复制 SVG"）；测试照上游另写一个解码器把矩阵读回原文。QRCode 的模块是一条路径：Avalonia、WPF 直接解析 SvgPath，WinForms 拼 GraphicsPath。
- 颜色是"纸面"：浅色主题下 surface 底、text 色模块，深色主题下固定白底 #1c2024。Avalonia 看 ActualThemeVariant 的根是不是 Dark（放进窗口前没有变体，按浅色），WPF 按主题 surface 画刷的亮度判断，WinForms 用 ThemeTokens.IsDark。上游的 color、bgColor 是 Foreground、Background（WinForms 也是这两个名字、Color?：ForeColor、BackColor 是环境属性，分不出设没设过）；margin 是 QuietZone（Margin 是平台的外边距）；size 是平台的 Width / Size，默认 10em，并且不被父级拉伸（同 inline-block）。
- logo 是图片或任意控件，放在 3% 内边距、sm 圆角的纸面底板上；有 logo 时纠错默认 H。状态层（加载中、已扫描、已过期与刷新按钮）盖在 logo 上面，底色是 90% 的纸面；刷新按钮只有订阅了 Refresh 才出现（WinForms 是 RefreshRequested：Control 自己有 Refresh 方法）。读屏：图片，名称默认语言包的"二维码"；状态是状态层的名称（Avalonia、WPF，Polite 实时区域）或描述（WinForms，换状态时通知）；logo 不进读屏树。

### Slider（2026-11-15）

- 上游的 _value 是数字或 [低, 高]：C# 拆成 Value（double）与 Range（SliderRange?，不为 null 时是两个滑块），两个都双向绑定；onValueCommit 是 ValueCommitted 事件。WinForms 另有 ValueChanged、RangeChanged（拖动中也会触发）。取值计算（对齐步长用 decimal，免得 0.1 + 0.2 的浮点误差；两个滑块互相限制；点轨道移动离得近的那个；按键的目标值）在孪生文件 SliderScale。
- 尺寸照 TS 按字号算：滑块 space.lg + space.xs 的圆，轨道 1.5 倍 space.xs 粗，刻度点 space.xs；点击区域至少 sizes.minTarget；滑块中心对准取值，两端各缩进半个滑块。有文字的刻度下方（纵向时右侧）留 1.5em（3em）。横向时占满给的宽度，纵向默认 10em 高。
- Avalonia、WPF：轨道、已选范围、刻度点由 Slider 画，滑块是可聚焦的子元素 SliderThumb（读屏是滑块，RangeValue 的范围是它能取的范围，取值文字在条目状态里），刻度文字是 Text；外层不占读屏节点。Avalonia 实现了 ICustomHitTest（整块都能点，轨道很细）；ArrangeOverride 里要按 finalSize 算位置（Bounds 还是上一次的）；Border.Render 密封，焦点环由 Slider 画在滑块外圈。WPF 的指针逻辑收进 PointerDown / PointerMove / PointerUp（合成的鼠标事件拿不到位置，测试直接按位置调用）。
- WinForms：整个控件自己画；单值时控件本身是读屏的滑块（Value 是取值文字，可写），范围时是一组、两个滑块子节点；范围时 Tab 先在两个滑块之间走再离开（ProcessDialogKey）。刻度文字夹在控件内（子控件画不到自己外面）；不可用时滑块的底色保持不透明（网页是整体半透明，滑块照样盖住轨道）。
- 不可用时整体半透明（Avalonia、WPF 设 Opacity，WinForms 画的颜色带透明度）；只读时能聚焦、拖动与按键不改值。表单字段名（name）不移植。

### DateTimeText、RelativeTime、Countdown（2026-11-15）

- 三个平台都有 System.DateTime，上游的 DateTime 叫 DateTimeText（WinForms 是 DateTimeTextControl，parity 的 TYPE_RENAMES）。值是 object：DateTimeOffset、UTC 与本地的 DateTime、带 Z 或偏移的 ISO 字符串是时刻，按语言包的 TimeZone（IANA 或 Windows 的 id，没写时本机时区）换算；未指定时区的 DateTime、不带偏移的字符串是墙上时间，DateOnly 只有日期（同 TS 的 Date / CalendarDateTime / CalendarDate）。MachineValue 是 time 元素的 datetime。
- 文字照 Intl 在 zh-CN、en-US 下的输出写（.NET 的文化格式与 Intl 不同："2026年10月20日 14:30"、"Oct 20, 2026, 2:30 PM"、"昨天"、"in 3 hr."），其他语言按英文的写法；TS 的 options（逐项指定）换成 .NET 的格式字符串 Format，按语言包的文化写。时间的 Long / Full 写 GMT 偏移，英文 Full 写时区的名字（中文没有可靠的本地化名字，同 Long）。这些都在孪生文件 TimeText。
- RelativeTime、Countdown 共用孪生的 NowClock（TS 的 useNow）：每次刷新后组件给出下一跳的间隔，对齐到间隔的整数倍再跳（同一窗口里的计时器一起跳），回调经 SynchronizationContext 回到界面线程；不在界面上时停下（Avalonia 离开可视树、WPF Unloaded、WinForms 句柄销毁），对应 TS 的"页面隐藏时停下"。时钟是 TimeProvider，测试换成 FakeTimeProvider。上游的服务端渲染记下"现在"不适用；要固定"现在"写 Now。
- 倒计时的剩余按精度向上取整（还剩 0.4 秒显示 1 秒），整秒对齐的一跳在目标带小数秒时显示不变，下一跳才少一秒（同 TS）。上游的 CountdownParts 在这里叫 CountdownRemaining（XxxParts 是部件名的类）；渲染函数是 Formatter（Func<CountdownRemaining, string>），onEnd 是 Ended 事件（从没到点变成到点时触发一次）。
- Countdown 的数字块：accent 底、accentContrast 字、semibold，最小宽度 2em，内边距 xxs / xs、圆角 sm 都按字号算。Avalonia、WPF 是 Flex 里的 Border 与 Text（每秒只换文字，格数变了才重建；Avalonia 的部件名在 CountdownParts），WinForms 是自绘元素 CountdownFace。字号随上下文：Avalonia 是继承的 TextElement.FontSize（放进 InlineUIContainer 会被行高裁掉，直接给字号），WPF 是 Control 的 FontSize，WinForms 是控件的 Font。Avalonia 量完写上基线（文字的第一行基线、数字块按格子里的文字）。
- 读屏：DateTimeText、RelativeTime 是文字（RelativeTime 的完整日期时间是提示，也是帮助文字 / 描述）；Countdown 是文字，Avalonia、WPF 的条目类型是 timer（TS 的 role="timer"），名称是显示的文字，写了名称时用它，里面的格子不另占节点。
- WPF 的 TextBlock.OnPropertyChanged 是密封的：DateTimeText、RelativeTime 换语言包时的重写挂在 Provider.LangProperty 的元数据上（OverrideMetadata）。WinForms 的 ElementControl 加了可重写的 OnScopeChanged（作用域的主题、语言包变了或换了位置），按语言包写文字的控件在里面重写；ItemListItemControl 加了 DescriptionContent（说明放控件，TS 的 description 是任意内容），示例里放相对时间。

### Checklist（2026-11-15）

- 状态、排法是枚举（ChecklistStatus、ChecklistLayout），项是普通的 ChecklistItem；图标、语言包的键、汇总（不适用的不算）在孪生文件 ChecklistModel。三种排法按 TS 每次重建；重建前放开旧行里使用方给的控件（证据、操作）。
- 上游"给了 onCheckedChange 才能勾选"：C# 是 CheckedChange 事件，订阅了就把状态图标换成复选按钮（事件的 add / remove 里重建）；读屏是复选框，名称是这一项的名称、帮助文字 / 描述是状态，不适用、受阻的不可用。按钮可点的区域不小于 sizes.minTarget（24），悬停时一层 fillHover 的圆。
- 说明、负责人、日期、证据是 fontSizes.sm（固定字号），使用方给的控件（证据的链接）也换成它；状态图标比文字大一档（1.14em）。图标排法整组是一张图，名称与提示是每一项的名称与状态。
- WinForms：控件自己有 Layout 事件，TS 的 layout 叫 ItemLayout（同 DescriptionsControl）；可勾选的状态是自绘的小控件（复选框角色、空格与 Enter 切换、键盘聚焦时焦点环）。部件名只有 Avalonia 有（ChecklistParts，操作那一格包一层 Border）。

### LoadingOverlay（2026-11-15）

- 防闪烁用已有的 DelayedLoading（200 毫秒内完成的不出现，出现后至少 500 毫秒）；读屏的"忙"（Avalonia、WPF 的 ItemStatus，WinForms 的 Busy 状态）与挡指针按 IsLoading 立即生效：遮罩出现前先盖一层看不见的。
- Avalonia、WPF：内容、挡指针的一层、遮罩叠在一起；遮罩的底是单独一层 surface 刷子、不透明度 0.7（TS 的 color-mix：资源里没有混好的刷子，跟着主题走），指示不跟着变淡；durations.md 淡入淡出；Blur 是内容本身的 BlurEffect（TS 是 backdrop-filter）。
- WinForms：子控件各有窗口，遮罩画在属于窗体的分层窗口里（同 Watermark，跟着控件移动、缩放）；遮罩出现前是 1/255 不透明的底（分层窗口只有不透明的像素才接点击），出现后每秒约 30 帧重画转动的指示（LayeredWindow 加了 Redraw：换画面不重新淡入）。没有 Blur，离场不淡出。
- 核对 WinForms 的分层窗口：屏幕截图（CopyFromScreen）拍不到分层窗口，直接把画好的那一帧存下来看。

### Ellipsis（2026-11-15）

- 文字是 Text 的 Truncate（一行）或 LineClamp（多行），展开时去掉限制。截断与否在排好以后量（同 TS 的 ResizeObserver 加每次渲染后），展开时不量：Avalonia 看排好的文字有没有省略过的行（TextLine.HasCollapsed）；WPF 用同样的字体按不限宽（多行时按现在的宽度、不限高）排一个 TextBlock 比实际大小；WinForms 的 Text 元素加了 Overflows（自然大小比 Bounds 大）。
- 截断且有提示时文字能 Tab 聚焦，提示用各平台的提示（Avalonia 的 ToolTip.Tip、WPF 的 ToolTip、WinForms 自带一个 TooltipProvider），键盘聚焦也显示；提示也是读屏的帮助文字 / 描述。Tooltip 不写时是文字本身（Expandable 时不提示），写 null 不提示。
- 展开按钮是链接样式（accentText、悬停下划线、和文字一样大），读屏是带展开状态的按钮：Avalonia 是 Link，WPF 是 ButtonBase 包一段 Text，WinForms 是 LinkControl 的子类（PushButton 角色、Expanded / Collapsed，空格也能按）。
- 顺手加了 Flex 的 Shrink（CSS 的 flex-shrink: 0）：Avalonia、WPF 的 Flex 原来让每项都能缩到 MinWidth，名称旁边的城市被挤成竖排；Shrink=False 的项不比内容窄（WinForms 的 FlexControl 原来按内容最窄的宽度，中文一个字就能换行，也加了 SetShrink）。

### Timeline（2026-11-15）

- 项是普通的 TimelineItem（标题、说明、操作人、详情可以是文字或控件；时间是显示的文字，DateTime 是机器可读的时间，做成时间的读屏帮助文字 / 描述）。Timeline 按项、Pending、Reverse、MaxVisible、IsExpanded 重建各行（同 TS 每次渲染重算）：收起的按钮在旧的那一端，倒序时在最后。重建前先放开旧行里使用方给的元素（一个元素只能有一个父元素）。
- 行：节点列 + 内容（交替排布时三列，时间在对侧，偶数项内容靠结束边对齐）；宽度不到 32em 时排回一侧（TS 的容器查询），宽度跨过阈值时重建。Avalonia、WPF 节点与连接线是 Border，WinForms 是画的；进行中的一项节点是 Spinner，读屏是忙（Avalonia、WPF 的 ItemStatus，WinForms 的 Busy 状态）。
- 操作人是文字时带首字母头像（1.75em：Avatar 按字号的 2.5 倍定边长，字号给 0.7 倍）；详情是 Collapsible，触发器是像链接的按钮，打开时才建内容。容器给链接的下划线是 hover。
- 上游的 components.Timeline（默认值 alternate、size）：Avalonia、WPF 是平台样式的 Setter，WinForms 是 ProviderControl 的 TimelineDefaults。部件名只有 Avalonia 有（TimelineParts）；WPF、WinForms 的行是代码组合的，没有部件样式键 / 部件属性记录。
- WinForms 的坑：flex 容器只量 AutoSize 的子控件（不是自动大小的按当前大小），自绘的行要开 AutoSize，不限宽时报内容的自然宽度。

### Rating（2026-11-15）

- 上游是单选组：每个分数一个视觉上隐藏的原生 radio。桌面上星由 Rating 自己画（孪生文件 RatingScale 算每颗填多少、能选的分数、方向键换到哪、点了以后的值）：空心的 borderStrong 描线打底，选中的那层按比例裁出、填上色调的实色；指针下的那颗放大 1.12 倍（没做过渡）。
- Avalonia、WPF：每个分数一个透明的 RatingOption 盖在星（整颗或半颗）上，接指针与焦点，读屏是单选项（"3 星"，SelectionItem 模式）；Tab 只落在已选的分数上（没选时第一个），方向键换分数并选中、到头绕回，空格选中，同原生单选组。焦点环由 Rating 画在聚焦的那颗星外面（只在键盘聚焦时）。从右往左排版时平台整个镜像。
- WinForms：一个能聚焦的控件，分数是读屏的子节点（AccessibleObject，单选项、能默认动作选中）；从右往左排版时自己镜像（星从右边排起、填色从右边裁起）。
- 只读时没有单选项，读屏是一张图，名称是自己的名称（或字段标签）接上"4.5 星，满分 5 星"。
- 坑：WinForms 的属性访问器里局部变量不能叫 field（C# 14 把它当成自动属性的后备字段，编译成半自动属性）。

### Popconfirm（2026-11-15）

- 三个平台都是 Popover 的子类（同 HoverCard），默认在上方：内容是自己建的图标、问题、说明与"取消 / 确认"，打开时焦点落在取消上（Popover 的 FocusTarget）。确认是 `Func<Task<bool>>`（同 Dialog 的 Confirm）：进行中确认按钮加载、取消不可用；结果为 false 或抛异常时保持打开。上游的 onCancel 是 Canceled 事件：确认后关闭的不算，确认进行中被 Esc、点外面关掉的等结果出来、没确认成才算。
- 读屏：浮层以问题为名称、说明为帮助文字（Avalonia、WPF 写在面板上；WinForms 写在内容窗口上，PopoverControl 加了可重写的 SurfaceName，默认仍取触发器）。最宽 20rem（按根字号 16 换算成 320）。
- 顺手修的（Avalonia）：Popover 面板、箭头的部件样式用的是 `rune|Popover`，类型选择器只认样式键，HoverCard 打开时面板没有底色、描边与阴影（headless 测试看不出）；改成 `:is(rune|Popover)`，派生类都套上，Menu 的弹出层不用再改样式键。ButtonBase 的主题去掉平台主题（Fluent）的焦点框：按钮自己画焦点环，键盘聚焦时原来会再叠一个黑框。

### NativeSelect（2026-11-15）

- ROADMAP 决定 28：各平台用自己的下拉框，使用方式同平台的 ComboBox。分组是孪生文件 NativeSelectGroups（没有组的在前，各组按第一次出现的顺序）；组名是一行不能选的标题，组里的选项缩进一档。上游的占位项（第一条、hidden disabled）在桌面上是 PlaceholderText，没选时值是 null。
- Avalonia、WPF 继承平台的 ComboBox，模板换成输入框外壳（同 Select）与我们的弹出面板；选项行由 Options 生成，Tag 是值（WPF 的行是 NativeSelectItem，内边距、圆角按字号算）。WPF 的 ComboBox 自己不处理点击打开，模板里盖一个透明的 ToggleButton。只读：别的选项不可用、不打开，方向键、滚轮也换不了（平台的下拉框关着时用它们换值）。WPF 用平台的 IsReadOnly（NativeSelect 从不可编辑），Avalonia 的 ComboBox 没有，自己加了 IsReadOnly。
- WinForms 就是 Win32 ComboBox（DropDownList、OwnerDrawVariable）：外框、下拉按钮、列表窗口是系统的，行是自己画的（组名、缩进、占位、已选 accentText）。系统没有不可选的行：落到组名、不可用的行上时跳到同方向的下一项；只读时在 WndProc 里吞掉点击、方向键、F4、Alt+↓、字母与滚轮。出错时在系统外框上描一圈 danger。读屏的名称、说明取所在字段（ComboBox 的读屏对象是平台内部的，只能写 AccessibleName / AccessibleDescription；使用方自己写了别的就不覆盖）。
- WinForms 的坑：改 ItemHeight 会建句柄（没放进窗口时建不了，等 OnHandleCreated）；设 DropDownHeight 会改 IntegralHeight 并重建句柄（构造里先关掉）；行高不一时系统摆好列表后还会再收几个像素，多出滚动条，打开以后按行高合计重设列表窗口。自然宽度同浏览器的原生 select：最宽的选项加留白与下拉按钮。
