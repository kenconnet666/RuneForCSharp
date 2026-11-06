# 上游改造的跟进

vue-tsx-pro 那边的会话改了设计时会发通知过来（每完成一个阶段一次）。这里记每次通知对三个库意味着什么：哪些照搬、哪些换做法、哪些不涉及，以及跟进到哪了。
通知只是同步设计决定，**按各自的技术栈适当跟进**，不逐字照抄；上游没定稿的部分不动代码。

## 样式体系联合改造（2026-11-06 收到计划草案；同日获批，上游开始执行）

计划：vue-tsx-pro 的 `.claude/reports/style-overhaul-plan.md`（阶段 0 护栏 → 1 引擎 → 2 架构与 API → 3 整理 → 4 美学 → 5 文档）。
上游的结论是运行时样式引擎不换架构，问题在"新组合按定制层相乘"和属性合并层；前三个阶段承诺外观不变，美学放最后。
这一轮只评估，不改代码；每个阶段完成的通知到了再按下面的对应关系跟进。

### 已定的方向（2026-11-06 上游通知）

- 顺序 0 → 5，每个阶段单独合进 master；前三个阶段外观 0 差异。
- 配置入口 `components: { Button: { defaults, styles, variants } }`，旧的 defaults / styles 保留一个版本。
- 外观词表按计划 §4.4 改名：tone 只用 6 个状态色、宽度用 width、variant 只表示外观、density 统一、Orientation / Placement 共用；旧名保留一个版本。
- 主入口不再导出组件内部的样式表，只导出配方与共用件。
- 美学：着色页面 + 白卡片；控件高度改整数、落在 4px 网格（xs 24 / sm 28 / md 32 / lg 36 / xl 40）；中文 600 只给标题，按钮、标签页用 500；波纹默认关；去掉海洋主题，加高对比暗色。
- **原则（用户特别强调，两边一致）**：维护好基础设施与基础组件，上层组件一律建在它们上面——上层要的能力先在基础层补齐再用，不在上层写平行实现。

### 对应关系

| 上游 | 三个库 | 跟进 |
|---|---|---|
| 阶段 0：示例截图基线、逐像素对比 | 已有 `inspect snapshot`（三个平台、亮暗两套进 CI 产物），缺"对比基线"这一步 | 上游进阶段 4 之前给 inspect 加 `compare`（两组 PNG 逐像素比对，报差异图），CI 存基线；美学改动之后的回归都靠它 |
| 阶段 0：有效声明快照 | 对应 `inspect props`（每个元素解析后的值与来源） | 同上，挑代表性示例把 props 输出存成快照测试，重构时逐字比对 |
| 阶段 0：性能原型（Proxy、配方记忆） | 浏览器专用 | 不涉及 |
| 阶段 1：分段输出 + `@layer` | 不照搬：三个平台的优先级各有现成机制（DESIGN §4）——Avalonia 的样式级联、WPF 的 Provider BasedOn 链、WinForms 的记录逐字段合并。上游要保的语义"组件 < createUi < 外层 Provider < 内层 Provider < 使用方 < 受管，后层只改它写到的属性"三个库已经是这样 | 不涉及；只核对一点（见下面"要核对的"第 1 条） |
| 4.1 配置合一 `components: { Button: { defaults, styles, variants } }` | Avalonia、WPF 天然就是一个入口：一条 `Style TargetType="rune:Button"` 同时写默认值（Setter）和外观，档位是伪类选择器 / 触发器，部件是 `/template/` 选择器 / 部件样式键 | 两个 XAML 库不用改。WinForms 的应用级配置与 Provider 还没做，做的时候直接用这个形状（每个组件一条记录：Defaults、Styles、Variants），登记表照上游用脚本从配方生成 |
| 4.2 组件侧一个钩子；size、radius、tone 进配方档位 | 对应按钮家族的 `ButtonCore`（外观合并一处）。上游修的问题——定制的圆角被 radius 默认值盖掉——在 Avalonia 有同样的风险 | 见"要核对的"第 1 条 |
| 4.3 每个内部节点都是部件、`data-scope` 用实际组件名 | 已经这样：每个组件自己的部件名类（`ButtonParts`、`IconButtonParts`），WinForms 的部件属性记录 | 上游补的 `propsXxx` 由 parity 报出来，照常补 |
| 4.4 外观词表统一 | 影响已做的：Text 家族与 Icon 的 `Tone`（8 值，含 muted、inherit）；Badge、Button 已经用 `StatusTone`。还没做的组件（Skeleton、Container、Dialog、Drawer、Result、NavList、密度、方向与位置）| 改名清单定稿后：已做的照改（保留旧名一个版本，`[Obsolete]`）；还没做的组件**直接用新名字**，不先按旧名做 |
| 4.5 引擎 API 收口 | 浏览器专用 | 不涉及 |
| 5.1 共用片段（pressable、不可用、过渡、焦点环、最小点击高度……） | pressable 已经是 `ButtonBase`（波纹、焦点环、颜色过渡、加载）；最小点击高度 max(2.25em, 24px) 现在只在按钮家族里写了三处（每个平台一处） | 做输入框之前把"控件高度"收成一处（Avalonia 一个共用资源、WPF 一组 EmSize 设定、WinForms 一个方法），美学阶段改高度表时只改一处 |
| 5.3 裸值换 token（scrim、循环时长、linear 缓动） | token-gen 生成 | 上游加了 token，`token-gen --check` 在 CI 里失败，重新生成即可；之后做的组件用新 token |
| 5.4 Badge 与 Button 的色调表合一 | 我们也有两张：Button 用调色板表（`palette.mjs`），Badge 用 `StatusColors` | 跟上游一起合成一张（Badge 的颜色也从调色板表取） |
| 6.0 去掉海洋主题、加高对比暗色 | token-gen、lang-gen 生成（主题、`themeOcean` 文案都是生成的） | 重新生成；Gallery 的主题列表读生成的登记表，不用手改；核对生成器里有没有按名字写死的主题 |
| 6.1 token 取值（S1 文字中间调、S6 对比度、S7 标题阶梯） | 生成 | 重新生成，截图对比过一遍 |
| 6.1 S5 阴影分尺寸、深色 1px 内高光 | 还没有组件用阴影（WinForms 有 ShadowLayer） | 做浮层时按新规则做：Avalonia 的 BoxShadows 支持 inset；WPF 的 DropShadowEffect 没有内阴影，用一圈描边代替 |
| 6.1 S3 控件高度改整数表（24 / 28 / 32 / 40 / 48） | 现在是 em 公式 | 等定稿；高度收成一处之后换成按字号档位查表 |
| 6.1 S4 中文字重（按钮 500，600 只给标题） | WinForms 的 GDI 只有常规、粗体，semibold 已经画成粗体——正是上游说的"600 在 Windows 上塌成粗体"；Avalonia、WPF 用微软雅黑时同样 | 等定稿后改按钮家族主题里的 FontWeight；WinForms 的 medium 画成常规 |
| 6.2 C1 ButtonBase 继承字重 | Avalonia、WPF 的 ButtonBase 主题不设 FontWeight，本来就继承 | 不用改 |
| 6.2 C5 波纹默认关 | 全局开关 `Ripple.Enabled`（默认 true） | 定了就改默认值一处 |
| 6.2 C2 Card、C3 Field、C4 NavList | 还没做 | 做的时候按新规则 |

### 要核对的

1. **Avalonia 的 Size、Radius 用 LocalValue 优先级的绑定**（`ButtonCore.BindTo`：字号绑主题资源、圆角绑 `{rune:Em}`），使用方或 Provider 区域的样式直接写 `CornerRadius`、`FontSize` 盖不过它——与上游 4.2 修的是同一类问题。做法定在下面"阶段 1"实现计划的第 2 步（改成主题里一条 setter 的绑定；代码按 Style 优先级写值会压过使用方的样式，DESIGN §6 实测过，不能用）。WPF 的圆角走 EmSize 的 coerce（使用方的值优先），字号是本地值的资源引用，同样改。
2. 词表改名会动已做组件的公开 API（Text、Icon 的 Tone），改名时三个库同一个提交周期做完，parity 的改名表同步。

### 阶段 1：分段输出与级联层（2026-11-06 通知完成）

上游：vue-tsx-pro 分支 feat/style-overhaul（PR #3），`packages/tsx/STYLE-ENGINE.md` §11。

**上游的语义**（我们要对齐的是语义，不是 `@layer` 这个手段）：

1. 按合并来源分段：组件自己的（基础 + 档位 + 组合档位）< 每层主题定制（createUi、每层 Provider）< 使用方的 sx < 组件受管的。
2. 后段不论"特异性"都盖过前段。
3. 结果与"把所有段合成一个"逐字相同：某段写了属性 P 的普通值，就**整个替换** P 在前面各段的取值，连条件值（悬停、按下……）一起替换；只写条件值的，与前面的取值合并。属性族（border、margin……）在最高的那一段里输出完整的合并结果；合并后只剩条件值或被去掉的属性，补一条无条件的 `revert`（回到"没写"）。

**三个平台现在的样子**：

| | 段的先后 | 与第 3 条的差别 |
|---|---|---|
| Avalonia | 原生：ControlTheme < 应用 Styles < 外层作用域 Styles < 内层 < 本地值 | **不一致**：带伪类的选择器（StyleTrigger）不论写在哪一层，都胜过不带伪类的（DESIGN §4 的规则"要覆盖档位样式，选择器里至少带一个伪类"）。所以后段写的普通 Background 盖不过前段的 `:solid`、`:pointerover`，与上游"普通值整体替换条件值"相反。另外代码里按 Template 优先级绑定的值（色调 → 调色板、Icon 的色调颜色）也高于任何普通样式；Size、Radius 是本地值优先级的绑定，样式盖不过 |
| WPF | 主题样式（含它的触发器）< 应用隐式样式 < 外层 Provider < 内层 Provider < 元素的 Style < 本地值；Provider 合成 BasedOn 链 | 主题样式整体低于任何样式 setter，组件这一段与定制段的关系**已经一致**。**不一致**在定制段之间：外层 Provider 的触发器胜过内层 Provider 的普通 setter（WPF 里样式触发器高于样式 setter，与 BasedOn 的先后无关）。字号（Size）是本地值的资源引用，样式盖不过 |
| WinForms | 还没有定制入口（应用级配置与 Provider 未做） | 做的时候直接按上游语义设计 |

属性族在桌面上基本不是问题：XAML 没有简写 / 长写之分（Background、BorderBrush 各是一个属性）。例外是 Thickness、CornerRadius 这类"四边一个值"的属性：上游可以只改 paddingInline，我们只能整体写；组件主题里已经按"带入前面的完整取值"手写（链接样式的 Padding 写成 `0, 0.25em`），使用方要改一边时同样整体写。暂不为此做按边的定制入口。

**实现计划**（上游阶段 2 的 components 入口通知到了再一起做；先动 Avalonia、WPF）：

1. **把"段"落到主题链上，后段带入前段的条件值**（对应第 3 条）。定制段写了某属性的普通值时，合成器在这一段里为前面各段出现过的每个条件（伪类选择器 / 触发器）补一条同条件、同值的设置，于是任何状态下都是后段的值：
   - WPF：在 `Provider.Compose` 里做（它已经按层合成 BasedOn 链）。片段的普通 setter 覆盖外层链里设同一属性的触发器：给片段补上同条件的触发器。主题样式不用管（它整体在下面）。
   - Avalonia：Provider 的样式改成和 WPF 一样合成——作用域里按类型键放一个派生的 ControlTheme（BasedOn 外层的），定制片段并进去，同样为基主题里设同一属性的伪类样式补同选择器、同值的嵌套样式。补出来的只有被定制的属性，激活器只多在这些上（DESIGN §9 的开销）。
   - "revert"（去掉属性）：WPF 的 Setter 不能写 UnsetValue，写成属性的默认值；Avalonia 的 setter 写 UnsetValue 会落回下一层（是 `revert-layer` 而不是 `revert`），同样写默认值。继承属性（Foreground、FontSize）写默认值会断掉继承，这种只在真用到时再处理。
2. **组件这一段只用主题的 setter 与样式**，不用 Template / 本地值优先级的代码绑定，否则任何定制段都盖不过：
   - Avalonia 的 Size → 字号、Radius → 圆角（现在是本地值绑定）、色调 → 调色板、Icon 的色调颜色（现在是 Template 优先级，DESIGN §9 为省激活器改的）都改成**主题里一条 setter 的绑定**（按档位取资源的绑定，档位为空时给 UnsetValue 落回继承）：仍然不用 N 条伪类样式，开销与现在相同，优先级回到主题这一层。要先确认 Avalonia 12 里怎样写一个"跟着资源变"的自定义绑定（标记扩展返回绑定）。
   - WPF 的 Size → 字号同理改到主题样式里（setter + 转换器，或按档位的触发器）。
3. **验证照搬上游的办法**：随机生成若干段（几个属性的普通值与条件值、几种状态），用一个"合成一个"的参考模型算出每种状态的取值，与平台实际解析出的值逐个比对（WPF 用测试里的自定义依赖属性当条件，Avalonia 用伪类）；再用 `inspect props` 对全部示例做前后快照比对，差异为 0 才合。
4. WinForms 做定制入口时：属性值是"普通值 + 各状态的条件值"的记录，按上游语义逐属性合并（普通值整体替换，条件值合并），一开始就照这个写，不用补。

**阶段 2 已落的部分**（上游说等阶段 2 完成再统一通知）：components 配置入口合一、组件侧 `useComponent`、字号 / 圆角 / 色调进配方档位（正是上面第 2 步要对齐的）、引擎 API 收口（浏览器专用，不涉及）。

### 阶段 2 完成、阶段 3 进行中（2026-11-06 通知）

| 上游 | 三个库 | 跟进 |
|---|---|---|
| 定制入口合一 `components.Xxx = { defaults, styles, variants }`（登记表由 `UiComponents` 推出类型） | XAML 本来就是一个入口：一条 `Style` / `ControlTheme` 同时写默认值、外观，档位是伪类 / 触发器 | Avalonia、WPF 不改入口；WinForms 做定制入口时用这个形状 |
| 配方直接引用共用片段：字号、圆角、色调进档位（`variantOf`），不再排在配方之后 | 对应上面"阶段 1"实现计划的第 2 步 | 组件这一段改成主题里的 setter 绑定 |
| 按钮家族的部件：root、content、icon、iconEnd、spinner | 已经有（`ButtonParts`、`IconButtonParts`）；WinForms 缺 content（内容层不是元素） | WinForms 的 propsContent 仍是缺口 |
| 词表：Text、Icon 的色调类型 `Tone` → `TextTone`；Skeleton 的形状用 `shape`；Sparkline、TimelineChart、WaveGrid 的画法用 `type` | 三个库的 `Tone`（Text 家族、Icon 用，8 个值）同样改名 `TextTone`；prop 名 `tone` 不变，parity 不受影响 | C# 不能给类型留旧名的别名（record struct 不能继承，using 别名不出程序集），而且还没有外部使用方，**直接改名不留旧名**。Skeleton、图表还没做，做时直接用新名字 |
| 每层定制、使用方的 sx 各进一层 | 见"阶段 1" | 同上 |
| 阶段 3：条件一律写别名（`_hover`……） | 浏览器专用的写法 | 不涉及 |
| 阶段 3：焦点环新增 inset（画在里面）、virtual（aria-activedescendant 的当前项常亮） | 我们的焦点环在 ButtonBase 里，只有外扩一种 | 做 Listbox、Menu 时照做（桌面上"当前项"对应 UIA 的选中项 / 焦点项，不是真焦点）；ButtonBase 加 inset 变体 |
| 阶段 3：展开箭头 flip（朝上）、turn（朝行尾，RTL 下反向转），修了 RTL 下箭头朝上 | Icon 有 FlipInRtl | 做 Accordion、Select、Tree 时照做 |
| 阶段 3：`statusToneCompound` 让 Badge、Alert 的组合档位直接引用共用色表 | 我们也是两张表：Button 用调色板表（`palette.mjs` 生成），Badge 用手写的 `StatusColors` | 合成一张：Badge（以后的 Alert）的颜色也从调色板表生成 |
| 阶段 3：OtpField 用输入框配方；Popover、HoverCard、Menu 面板统一 `anchoredPanelSx` | 还没做 | 做的时候照做（浮层表面一个共用件） |

**跟进的工作包**（按先后；都要先跑 `inspect bench` 与 `inspect props` 的前后对比，外观与开销不能变差）：

1. `Tone` → `TextTone` 改名（三个库，小改动，公开 API）。
2. 组件这一段只用主题的 setter（阶段 1 计划第 2 步）：Avalonia 的 Size、Radius、色调 → 调色板、Icon 与 Text 的色调颜色改成主题里一条 setter 的绑定；WPF 的 Size 同理。加"定制段盖得过组件档位"的测试。
3. Provider 合成时后段带入前段的条件值（阶段 1 计划第 1 步）：先 WPF（`Provider.Compose`），再 Avalonia（Provider 改成合成派生的 ControlTheme）；随机分段的比对测试。
4. Badge 的颜色从调色板表生成，与 Button 同一张表。
5. WinForms 的定制入口与 Provider：按 `components` 的形状、按上游语义逐属性合并。

### 进度

- 2026-11-06：收到计划草案，完成上面的评估。同日上游获批，方向见"已定的方向"。代码未动，等上游各阶段的通知；还没做的组件直接按已定的词表与规则做（新名字、控件高度收成一处、浮层阴影按新规则）。
- 2026-11-06：收到阶段 1 完成的通知，对照三个平台写了上面"阶段 1"的差别与实现计划。按用户的安排（这阶段先做性能），代码等阶段 2 的通知到了再一起动。
- 2026-11-06：收到阶段 2 完成、阶段 3 进行中的通知，列出上面 5 个工作包。其中第 3 包是跨模块的重构（Provider 合成），开工前先请用户确认。
