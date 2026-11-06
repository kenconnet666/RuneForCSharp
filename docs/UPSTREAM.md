# 上游改造的跟进

vue-tsx-pro 那边的会话改了设计时会发通知过来（每完成一个阶段一次）。这里记每次通知对三个库意味着什么：哪些照搬、哪些换做法、哪些不涉及，以及跟进到哪了。
通知只是同步设计决定，**按各自的技术栈适当跟进**，不逐字照抄；上游没定稿的部分不动代码。

## 样式体系联合改造（2026-11-06 收到计划草案；同日获批，上游开始执行）

计划：vue-tsx-pro 的 `.claude/reports/style-overhaul-plan.md`（阶段 0 护栏 → 1 引擎 → 2 架构与 API → 3 整理 → 4 美学 → 5 文档）。
上游的结论是运行时样式引擎不换架构，问题在"新组合按定制层相乘"和属性合并层；前三个阶段承诺外观不变，美学放最后。
这一轮只评估，不改代码；每个阶段完成的通知到了再按下面的对应关系跟进。

### 已定的方向（2026-11-06 上游通知）

- 顺序 0 → 5，每个阶段单独合进 master；前三个阶段外观 0 差异。
- 配置入口 ，旧的 defaults / styles 保留一个版本。
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

1. **Avalonia 的 Size、Radius 用 LocalValue 优先级的绑定**（`ButtonCore.BindTo`：字号绑主题资源、圆角绑 `{rune:Em}`），使用方或 Provider 区域的样式直接写 `CornerRadius`、`FontSize` 盖不过它——与上游 4.2 修的是同一类问题。上游阶段 2 落地时一起改：试绑定在样式优先级（`BindingPriority.Style`）上，确认主题与使用方样式的先后，加测试。WPF 的圆角走 EmSize 的 coerce（使用方的值优先），字号是本地值的资源引用，同样核对。
2. 词表改名会动已做组件的公开 API（Text、Icon 的 Tone），改名时三个库同一个提交周期做完，parity 的改名表同步。

### 进度

- 2026-11-06：收到计划草案，完成上面的评估。同日上游获批，方向见"已定的方向"。代码未动，等上游各阶段的通知；还没做的组件直接按已定的词表与规则做（新名字、控件高度收成一处、浮层阴影按新规则）。
