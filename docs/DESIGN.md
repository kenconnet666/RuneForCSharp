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
│  ├─ apps/Rune.Avalonia.Gallery/     组件示例（Debug 带 HotAvalonia）
│  └─ tests/Rune.Avalonia.Tests/      headless 测试
├─ wpf/
│  ├─ Directory.Build.props           导入根设置，改成 net10.0-windows + UseWPF
│  ├─ src/Rune.Wpf/
│  ├─ apps/Rune.Wpf.Gallery/
│  └─ tests/Rune.Wpf.Tests/           STA 线程 + 离屏窗口
└─ winforms/
   ├─ Directory.Build.props           导入根设置，改成 net10.0-windows + UseWindowsForms + PerMonitorV2
   ├─ src/Rune.WinForms/
   ├─ apps/Rune.WinForms.Gallery/
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
| 默认值（defaults） | 应用或 `rune:Provider` 的 Styles 里按类型写 Setter（`rune\|Button` 设 Variant、Tone、Size） | `Provider.Styles` 里按类型写 Style（合成 BasedOn 链） | 做 WinForms Provider 时定（M1，工作包 5） |
| 外观（styles） | 同上；要盖过档位给的颜色时选择器带类或伪类（"待定的决定"里的做法 A）；整片换色调设 `Palette` | 同上；任何 setter 都盖过主题样式的档位，内层普通值连外层条件值一起替换 | 同上 |
| 部件（propsXxx） | `/template/` 选择器、代码组合部件的子选择器（第 7 节） | 部件样式键 `ButtonParts.Icon`（`PartKey`） | 部件属性记录 `PropsIcon` |
| 单个实例（sx） | 实例的 Styles、Classes | 实例的 Style | `Sx` 记录（M1） |

稳定的名字：组件的公开属性、部件名常量与部件样式键（`ButtonParts.*`、`*Parts`）、token 资源键（`Rune.Brush.*`、`Rune.FontSize.*`……，由 token-gen 按上游生成）、`Rune.Palette.*`。
**不保证稳定**的组装用资源键：Avalonia 的 `Rune.ButtonCore`（按钮家族共用的 ControlTheme）、WPF 的 `Rune.ButtonCore` 与 `Rune.Recipe.*`（生成的配方触发器）。XAML 的资源键藏不住，所以写在这里；要改按钮家族的外观，写类型键的样式，不要 BasedOn 这些键。

### WinForms：分层解析

- 没有样式系统。外观是一个"字段可空"的记录，按"Sx > 内层作用域 > 外层作用域 > 配方"逐字段取第一个非空值（已实测）。
- 作用域控件沿 Parent 链查找，作用域里的样式与默认值都是信号。解析结果是 `Computed`，绘制时读取它（第 10 节）。
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
| 单个实例（`propsXxx`） | 控件自己的 `Styles` | 现在要包一层 Provider；计划让任何元素都能当一层（阶段 2） | 组件的 `PropsXxx` 属性 |
| 层层合并 | 外层作用域 < 内层作用域 < 实例的 Styles，逐个属性 | 主题 < 应用 < 外层 Provider < 内层 Provider，逐个 setter | 配方 < 外层作用域 < 内层作用域 < 实例，逐字段 |
| 部件的事件 | 组件给语义事件与可取消的前置事件（`Clearing` 带 `Cancel`）；输入级的接管用隧道事件；部件的路由事件照常冒泡 | 同左（Preview 事件） | 记录里可以放委托，使用方先执行、`Cancel` 跳过组件的处理；组件另给语义事件 |
| 受管属性 | 组件在代码里设本地值或用模板绑定，样式盖不过 | 同左 | 不进部件记录 |

实测结果：
- **Avalonia**：两层 `/template/` 只命中 Outer 里那个 Inner 的图标，单独的 Inner 不受影响；外层作用域设 StrokeWidth、内层作用域对同一路径只设 Foreground，结果两个都在；实例自己的 Styles 能覆盖作用域；`AutomationProperties.Name` 这样的附加属性也能经样式转发。
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
- **事件"使用方先处理、可接管"**：
  - Avalonia 和 WPF 都用隧道阶段：使用方在 Preview/Tunnel 里设 `Handled = true`，组件的内部处理就跳过（已实测）。冒泡阶段的普通订阅在组件处理之后才收到，组件标记 Handled 后就收不到了。
  - WinForms 没有路由事件，由组件提供可取消的前置事件（做组件时定）。

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
- **示例与 inspect**：示例按"组件/示例名"登记在各平台的 `apps/Rune.*.Examples`，Gallery 与 inspect 都用这张表。inspect 把示例放进宿主（背景、文字颜色、字号取主题），输出截图、元素树、属性与来源、读屏树：
  - Avalonia：headless 平台加 Skia、HarfBuzz（`UseHeadlessDrawing = false`），`CaptureRenderedFrame` 出图，`SetRenderScaling` 做缩放；值的来源取 `GetDiagnostic` 的优先级。样式与 Gallery 相同（FluentTheme 给 UserControl 等平台控件模板）。
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
- **暂缓**：`draw`（描线动画，一笔画出）三个平台都还没做，parity 里列为缺失。

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
  - 字体共用（`Fonts`）：同样的（字体、磅值、样式）一个 Font，元素不持有、不释放。主题的字体是一串候选，`Fonts.Installed` 取第一个装了的（Mono 用它；宿主、Gallery 也用它设主题的 Sans）。
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
- **Grid**：CSS grid 的子集（等宽列、minItemWidth 自动换列 + auto-fit 收起空列、行列间距、逐行 / 逐列放置与 dense 回填、跨列跨行、align / justify-items）。计算是孪生文件 `GridLayout`。`Length` 类型认 CSS 长度（px、em、rem，rem 按浏览器的 16px）。WinForms 的跨度既能用 GridItemControl 包一层（同 TS），也能用 GridControl 的扩展属性 `SetSpan` / `SetRowSpan`（同 TableLayoutPanel）。
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
- **Empty**：Avalonia、WPF 代码组合；WinForms 画图标、标题、说明，子控件与 Action 排在下面。图标默认 Inbox，设成 null 不显示（TS 的 icon={false}）。
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
- WinForms：
  - 元素 `Switch` + 宿主 `SwitchControl`；轨道、滑块、焦点环、轨道里的内容是画的。轨道色随滑块位置在 borderStrong 与实底之间插值（同一个 `Animator` 信号，等于 TS 的 background-color 过渡）；滑块的投影用主题的 shadows.sm（`Drawing.Shadow`，几圈半透明填充近似模糊）；不可用时每个颜色按 opacities.disabled 混进宿主的背景色（GDI 的文字不认 alpha，混色等价于整体半透明）。
  - 焦点环贴着轨道（外扩 4、宽 2），画在宿主默认的 4 像素 Padding 里，只在 `ShowFocusCues` 时画。
  - 轨道里的内容是 `SwitchContent`（文字或图标，字符串与 `IconData` 都能直接赋值：`CheckedContent = Lucide.Moon`），不另分 CheckedText / CheckedIcon；设计器里按文字编辑。图标取 `PropsContentText` 的字号（默认 xs）与色调。
  - 部件：`PropsLabel`、`PropsContentText`（`TextProps`）、`PropsSpinner`（`IconProps`）；propsControl、propsThumb、propsContent 是画出来的，没有元素，缺口同 Progress，等部件记录（`Sx`）一起定；propsRoot 不移植（外层就是 Switch）。
  - 读屏：复选按钮（`AccessibleRole.CheckButton`），名称是 AccessibleName 或文字，状态选中 / 未选中（切换时发 StateChange）、切换中带 Busy、只读带 ReadOnly；轨道里的内容不进无障碍树。MSAA 没有“必填”“校验未通过”的状态，`IsRequired`、`IsInvalid` 只影响外观。
  - 文字的行高按字体算（不是 TS 的 1.5em），在 1.5em 的第一行里垂直居中，多行时上下各留一份。

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
