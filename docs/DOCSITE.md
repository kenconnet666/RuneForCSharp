# 文档站规划：正式文档站与像素级对照

目标：三个平台各有一个正式的文档应用，页面、示例与 vue-tsx-pro 文档站一一对应；每个示例都能和上游的同一个示例逐像素对照，差异有报告、有登记。
这份文档记现状、要先做好的东西与先后顺序；做完一项勾掉一项，细节随做随补。决定写进 ROADMAP 的"已定的决定"。

## 现状（2026-11-07）

| | 上游文档站 | 我们 |
|---|---|---|
| 页面 | 136 个组件页（15 个分类）+ 指南、演练场 | 18 个组件页（Gallery 里按示例列） |
| 示例 | 448 个 | 45 个（三个平台同一批，id 与上游一致） |
| 页面结构 | 顶栏、侧栏（分类）、本页目录、示例两列、示例卡片（效果 + 源码 + 进演练场）、键盘表、API 表、上一篇 / 下一篇、命令面板搜索 | Gallery：左边主题与示例列表，右边一个示例 |
| 对照工具 | 自带 `scripts/docs/shots-batch.mjs`（逐示例截图）、`shots-compare.mjs`（两批截图逐像素比） | `ts-styles` + `inspect styles` + `styles.mjs`（取值）、`behavior.mjs`（行为）、`inspect snapshot`（单个示例截图） |

上游各分类的页数与示例数：base 9 / 54、layout 8 / 17、display 22 / 57、feedback 7 / 21、overlay 7 / 27、navigation 7 / 20、input 21 / 70、date 10 / 37、form 2 / 10、table 3 / 20、patterns 14 / 25、charts 16 / 51、three 9 / 31、diagram 1 / 4、config 1 / 4。
扩展（charts、three、diagram、code-editor、maps、xlsx）与页面组合（patterns）排在最后，先把组件库主入口的页做齐。

## 一、像素级对照要先解决的

对照分三层，前两层已经在用：取值（每个部件的计算样式逐项比）、行为（上游测试用例逐条对上）、像素（新）。像素这一层回答"看起来一样吗"，
前两层回答"为什么不一样"。

### 1. 让两边在同样的条件下渲染

- **字体**：上游默认用系统字体（苹方、微软雅黑），文档站还能换等距更纱黑体 SC、霞鹜文楷。对照时两边固定用同一份字体文件（建议更纱黑体 SC：开源、
  中西文同宽、上游站已经带了），Playwright 截图时切到这份字体，我们的示例应用与 inspect 也加载它，不让系统字体回退参与。
- **缩放**：统一 100%（deviceScaleFactor 1）与 200% 两档；WinForms 的截图已经按 192 DPI 出。
- **画布**：上游示例效果区的画布是页面底色、md 字号、行高 normal（`site/canvas.ts`），效果区有固定的内边距与宽度；
  我们的 `inspect snapshot` 加同样的画布与宽度（现在只给了 `--width`）。
- **主题**：token 由生成器保证一致；对照亮、暗两套，其余内置主题抽查。
- **静止状态**：截图前停掉动画（时长 token 设 0，同 Avalonia 的测试）、指针移出、不留焦点；加载指示这类按时间变的部件单独标记。

### 2. 文字不可能逐像素一样，要分开比

Chromium（Skia）、Avalonia（Skia）、WPF（DirectWrite）、WinForms（GDI / GDI+）的文字栅格化各不相同，同一行字边缘的灰度一定不同。办法：

- **非文字像素逐像素比**：底色、描边、圆角、图标、分隔线、阴影。文字区域用掩码盖掉（掩码来自 `ts-styles` 与 `inspect styles` 已经有的文字节点盒子）。
- **文字只比盒子**：位置、宽高、颜色、基线（盒子已经在取值层比了一部分，这里换成"截图坐标里的盒子"再比一次）。
- **阈值**：每个通道差超过 24 才算不同（同上游 `shots-compare.mjs`），差异像素的比例超过阈值才报；每个示例允许的差异与原因登记在 `tools/parity/registry.mjs`（像 `BEHAVIOR_NOT_APPLICABLE`）。

### 3. 工具

- **上游截图**：直接用上游的 `shots-batch.mjs`（在 `~/.rune/vtp-docs` 那份副本里跑，按页面、按示例元素截图）；需要时给它加两个参数：字体、缩放。
  不改上游仓库：要的参数先发给维护 vue-tsx-pro 的会话。
- **我们的截图**：`inspect snapshot` 补画布、缩放、静止状态，按"页面 / 示例 / 主题 / 缩放"命名，与上游的文件名对齐。
- **比对**：`tools/parity/pixels.mjs`——读两边的截图与两边的文字盒子，按上面的规则比，出差异图与 HTML 报告（上游 | 我们 | 差异热图）；
  有差异时退出码为 1。图片读写用上游副本里装好的 `pngjs`（本仓库不装 node 包，同 `ts-styles` 借用 Playwright 的做法）。
- **CI**：三个平台各跑一遍已做的页，报告作为产物上传；阈值内的差异不让 CI 失败，新出现的超阈值差异让它失败。

## 二、正式文档站要先做好的

### 1. 内容从上游抽，不手抄

- **页面与示例的清单**：写 `tools/docs-gen`，从上游每页的 `docs.tsx` 抽出分类、顺序、页面标题与说明、示例的 id / 标题 / 说明、键盘表、API 表里列的组件，
  存成 JSON（像 token、语言包一样是生成文件，锁定上游提交）。我们的示例说明按平台改写，但 id、顺序、标题照上游。
- **示例注册改成两级**：现在每个平台是一张扁平的 `Example` 表；改成"页面 → 示例"，页面的元数据来自上面的 JSON，示例只写平台自己的内容与说明。
  缺示例时文档站显示"这个示例还没移植"，覆盖率由同一份清单算出。
- **示例的源码展示**：每个示例的代码要能在页面上看到（XAML / C#）。示例按"一个示例一个方法 / 一个 XAML 文件"组织（现在大多已经是），构建时把源码作为资源嵌进示例程序集，按示例切片。
- **API 参考**：从 XML 文档注释生成（属性、事件、部件、默认值、档位的取值），用 parity 的改名表标出与上游 props 的对应，平台没有的标"不移植"及原因。
- **指南页**：开始使用、主题与 token、作用域（Provider）、尺寸与 em、热重载、各平台的差异与限制（把 DESIGN 里散落的平台差异整理成给使用方看的文字）。

### 2. 文档站自己要用到的组件（先做，也都在组件路线图里）

上游文档站（`apps/docs/src/site`、`packages/ui/src/docs-kit`）用到的、我们还没有的：

| 用途 | 组件 |
|---|---|
| 整体布局 | AppShell（顶栏 + 侧栏 + 内容）、ScrollArea |
| 导航 | NavList（侧栏，分组、当前页）、本页目录（Toc / 锚点）、上一篇 / 下一篇、命令面板（CommandPalette，搜索页面与示例） |
| 示例卡片 | Tabs（效果 / 代码）、CodeBlock 与语法高亮（docs-kit 的 CodePreview、CodeGroup）、CopyButton、Tooltip |
| 参考表 | Table（API 表、键盘表；docs-kit 的 PropsTable、TextTable） |
| 顶栏 | Select / SegmentedControl（主题、字体切换）、IconButton ✓、Link ✓ |
| 已有 | Card、Flex、Grid、Container、Text / H1–H6、Code、Kbd、Badge、Separator、Empty、Skeleton、Switch |

顺序按上游的依赖层级：Tooltip、CopyButton、Tabs、Table、Select 是基础与输入里本来就要做的；NavList、AppShell、ScrollArea、CommandPalette 跟导航、浮层一起做；
CodeBlock 的语法高亮先做一个够用的版本（C#、XAML 两种语言），不追 code-editor 扩展。

### 3. 文档站的形态

- **建议**：三个平台各自的桌面文档应用，替换现在的 Gallery（`*/apps/Rune.*.Gallery` 改名为 `Rune.*.Docs`）。页面结构照上游：顶栏、侧栏分类、本页目录、
  示例两列、示例卡片、键盘表、API 表。用的就是组件库自己的组件——文档站本身是最大的一个示例，也是组件组合起来的验收。
- 对照报告是另外生成的静态 HTML（上一节的 `pixels.mjs`），不放进文档应用。
- 不做网页版的"三平台截图站"当正式文档站：桌面组件要能点、能用键盘才看得出行为，截图只适合对照。

### 4. 基础设施

- 示例应用与 inspect 共用示例注册（已经是这样），文档应用再加页面层。
- 热重载：示例改了即时看到（HotAvalonia、WPF 的 XAML 热重载、WinForms 的 HotReload.cs）。
- 截图在 CI 里跑亮暗两套、两档缩放，结果与上游的截图一起上传。

## 三、先后顺序

1. **对照基础**（先拿已做的 18 页跑通）：固定字体、`inspect snapshot` 补画布与缩放、借用上游的 `shots-batch`、写 `pixels.mjs` 与报告、登记表。
2. **内容抽取**：`docs-gen` 产出页面与示例清单；示例注册改两级；覆盖率报告。
3. **文档站要用的组件**：Tooltip、CopyButton、Tabs、Table、Select / SegmentedControl、CodeBlock（简单高亮）、NavList、Toc、ScrollArea、AppShell、CommandPalette。
4. **文档应用**：每个平台一个，替换 Gallery；API 表从 XML 文档注释生成。
5. **补齐组件与示例**：按上游的层级把 136 页做齐，每页过取值、行为、像素三道核对。

## 四、这一轮代码整理

原则：目录与上游 `packages/ui/src` 的分类、文件一一对应（上游一个组件一个文件，我们一个组件一个文件夹）；同一处写法第三次出现就收成一处；整理与功能改动分开提交。

做了：
- [x] 目录：`CodeText` 移到 `Base/CodeText`（上游 base/CodeText.tsx）；`SkeletonText` 移到 `Feedback/SkeletonText`（WPF 的部件样式随之拆出 SkeletonText.xaml）；
  `ListboxOption` 移到 `Input/ListState`（上游 input/list-state.ts，孪生文件表跟着改）；WinForms 的标题从 Text 拆到 `Base/Heading`（另两个平台本来就是）；
  Field 的标签布局档位（`FieldLabelPlacement`、`FieldLabelAlign`）拆成 `Form/Field/FieldLayout.cs`（上游 form/field-layout.ts）。
- [x] Avalonia：查主题资源的写法收成 `ThemeResources.Resource<T>`，时长 token 收成 `Motion.Duration(control, key, fallback)`（同 WPF），Ripple 的私有副本删掉。
- [x] WinForms：不可用时颜色混进背景色的写法收成 `Element.Dimmer`（勾选类、表单标签共用）。
- [x] 测试：按名字找部件、主题颜色、画刷颜色、读屏节点收进各平台测试的 `Infrastructure/Lookup.cs`。

保留不拆的（理由）：
- 按钮家族（Button、ButtonBase、ButtonCore、IconButton、IconLinkButton、LinkButton、CloseButton、ClearButton）留在 `Base/Button`：上游是分开的文件，
  但它们共用一份主题文件与 ButtonCore，拆开后每个文件夹只剩一两个文件，还要拆主题。
- Code 与 Mark 留在 `Base/Code`：共用主题文件（WinForms 还共用 ChipText），Mark 只有几行。
- Progress 一个文件五百多行：条形、环形共用状态与动画，拆开反而要来回跳。
- WinForms 的 `Elements/`、`Reactivity/` 是这个平台自己的基础设施（无窗口元素、信号），上游没有对应。

待做（下一轮）：
- [ ] 测试里其余的局部辅助方法（按钮的模板部件、WPF Switch 的部件样式键查找）换成 `Lookup` 的写法。
- [ ] Gallery 改名为文档应用时，示例注册改两级（见上面的"内容从上游抽"）。
