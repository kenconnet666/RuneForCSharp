# 文档站规划：正式文档站与像素级对照

目标：三个平台各有一个正式的文档应用，页面、示例与 vue-tsx-pro 文档站一一对应；每个示例都能和上游的同一个示例逐像素对照，差异有报告、有登记。
这份文档记现状、要先做好的东西与先后顺序；做完一项勾掉一项，细节随做随补。形态已定（ROADMAP 决定 26）：三个平台各自的桌面文档应用，替换 Gallery。
对照出的差异默认是我们这边的问题、在这边修；查清确实是上游的问题时，才记进 UPSTREAM.md 的"上游问题"并通知维护 vue-tsx-pro 的会话。

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

- **字体**：两边都用主题的字体栈（`system-ui, Segoe UI, PingFang SC, …, Microsoft YaHei UI, …`），在 Windows 上落到 Segoe UI（西文）与微软雅黑 UI（中文），
  本机与 CI 的 Windows 都有，不另带字体文件。文档站的字体切换（更纱黑体、霞鹜文楷）不参与对照。
- **缩放**：100%（deviceScaleFactor 1）与 200% 两档；WinForms 按显示器的 DPI 出图，比对时取同档的上游截图。
- **画布**：上游示例效果区 `[data-demo]` 宽 756，里面一层有 21（1.5em）的内边距，页面底色、md 字号、行高 normal（`site/canvas.ts`）；
  效果区至少 7rem（112）高，内容竖直居中（Example 的 canvas、CodePreview 的 `justify-content: center`）。
  我们的 `inspect snapshot` 用 `--width 756 --padding 21 --min-height 112` 出同样大小的图（底色、字号、字体本来就同上游的画布）；
  居中的偏移按设备像素取整（浏览器的布局是小数，边框贴着整像素画：偏移 35.5 时卡片的边画在 36）。
- **布局不取整**：上游按小数像素排版（一项高 39.375），Avalonia 默认按整像素取整，往下越排越偏；对照时 `--exact` 关掉取整（同取值对照）。
  WPF 默认就不取整；WinForms 只有整像素，偏差算进差异。
- **主题**：token 由生成器保证一致；对照亮、暗两套，其余内置主题抽查。
- **静止状态**：上游截图时减少动态效果、指针移开、不留焦点、等网页字体；我们的示例在 headless 里本来就是静止的。按时间变的部件（加载指示）单独看。

### 2. 文字不可能逐像素一样，要分开比

Chromium（Skia）、Avalonia（Skia）、WPF（DirectWrite）、WinForms（GDI / GDI+）的文字栅格化各不相同，同一行字边缘的灰度一定不同。办法：

- **非文字像素逐像素比**：底色、描边、圆角、图标、分隔线、阴影。文字区域盖掉：上游截图时记下效果区里每段文字的盒子（逐行），外扩 2 像素后不比。
- **文字只比盒子**：文字的位置、大小、颜色、字重由取值对照（`styles.mjs`）管。
- **位移容差**：一个像素在对方图里周围 1 个 CSS 像素内能找到相同颜色就不算不同（同 pixelmatch 的抗锯齿处理）：上游按小数像素定位、平台按整像素对齐，边缘差半个到一个像素。
- **阈值**：每个通道差超过 24 才算不同（同上游 `shots-compare.mjs`）；差异像素占未盖掉部分的比例超过 0.5% 才报。查清原因、决定不改的差异登记在
  `tools/parity/registry.mjs` 的 `PIXEL_ALLOWANCE`（像 `BEHAVIOR_NOT_APPLICABLE`），改得了的直接改。

### 3. 工具

- [x] **上游截图**：`tools/ts-shots`（上游的 `shots-batch.mjs` 截的是整个小节，含标题、说明、源码，所以另写）：每个示例的效果区一张，亮暗、1x / 2x，另存文字盒子。
- [x] **我们的截图**：`inspect snapshot` 加了 `--padding`、`--min-height`、`--exact`（`--width`、`--scale`、`--theme`、`--out` 本来就有）。
- [x] **比对**：`tools/parity/pixels.mjs <页面|all> --platform …`：两边出图、盖文字、容位移、逐像素比，出差异图与 HTML 报告（上游 | 我们 | 差异），
  `all` 另出每页一行的总表；超过允许值时退出码为 1。图片读写借用上游副本里装好的 `pngjs`（同 `ts-styles` 借用 Playwright）。
- [ ] **CI**：要 Windows、上游文档站的开发服务器与 Playwright 的浏览器，先在本机跑；等文档站要用的组件做完、差异收敛后再接进 CI（报告作为产物上传，新出现的超阈值差异让 CI 失败）。

### 4. 差异基线（2026-11-07，亮暗两套、1x；WinForms 按本机 192 DPI 即 2x；画布加了至少 112 高、内容竖直居中之后重跑）

`node tools/parity/pixels.mjs all --platform <平台>` 的总表，"超过"是超过 0.5% 的示例数 / 比了的示例数，括号里是最大差异：

| 页面 | Avalonia | WPF | WinForms |
|---|---|---|---|
| popover | 2 / 4（0.89%） | 4 / 4（0.92%） | 4 / 4（10.73%） |
| scroll-area | 6 / 6（4.10%） | 6 / 6（3.32%） | 6 / 6（13.90%） |
| code（CodeBlock 的四节） | 8 / 8（2.13%） | 8 / 8（2.89%） | 8 / 8（14.25%，Switch 那一行的行高） |
| checkbox | 6 / 10（1.40%） | 8 / 10（1.44%） | 10 / 10（17.82%） |
| empty | 2 / 2（1.46%） | 2 / 2（1.42%） | 2 / 2（10.77%） |
| icon-button | 4 / 4（1.95%） | 4 / 4（1.99%） | 4 / 4（16.41%） |
| icon | 12 / 14（2.42%） | 12 / 14（2.28%） | 12 / 14（9.36%） |
| list | 4 / 4（2.53%） | 4 / 4（2.48%） | 4 / 4（11.75%） |
| avatar | 4 / 4（5.95%） | 4 / 4（5.91%） | 4 / 4（23.69%） |
| card | 6 / 6（9.95%） | 6 / 6（8.68%） | 6 / 6（13.22%） |
| progress | 8 / 8（12.20%） | 8 / 8（12.19%） | 8 / 8（15.77%） |
| button | 20 / 20（14.38%） | 20 / 20（18.69%） | 20 / 20（29.12%） |
| text | 14 / 14（20.49%） | 14 / 14（19.74%） | 14 / 14（11.50%） |
| badge | 4 / 4（41.85%） | 4 / 4（48.31%） | 4 / 4（24.75%） |

上一版（画布没有最小高度）里 Icon、IconButton 页的二三十个百分点多半是这个原因：上游的示例区至少 7rem 高、内容居中，矮的示例整体往下错开。

看过的原因（逐个收敛时从这里接着查）：

- **示例内容与上游不一致**：差异十几到几十个百分点、高度差几十像素的，多半是示例写法偏了（Icon 的 custom、tones，Badge 的 shapes 高度差 20–60 像素；
  Checkbox 的单选示例曾把选项拉满整行，已改）。先对着上游的 demo 改示例。
- **细描线的抗锯齿**：图标 1.5px 的描线两边灰度不同，每张图 1.5%–2.5%。要么比对时对描线放宽（按上游的图找出细线像素），要么登记。
- **字宽**：同一字体，浏览器与平台的字宽差零点几像素，横排的项跟着文字往后错开 1–2 像素（Checkbox 的横排复选框）。
- **WinForms 的高度**：文字行高按字体算（不是 1.5em），每个控件四周留 4 像素给焦点环，越往下累积越多；外观本身对得上。要单独定方案（行高按 token 算、焦点环画到父级上）。

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

## 三、先后顺序（用户 2026-11-07 定：对照基础之后先做文档站要用的组件）

1. [x] **对照基础**：画布与缩放对齐、`ts-shots`、`pixels.mjs` 与报告、登记表；已做的页面跑出差异总表（见下面"差异基线"）。
2. [ ] **文档站要用的组件**：~~Tooltip~~、~~CopyButton~~、~~ScrollArea~~、~~CodeBlock~~（2026-11-07 做了，内置简单高亮）、Tabs、Table、Select / SegmentedControl、NavList、Toc、AppShell、CommandPalette。
   每个组件照常过取值、行为、像素三道核对。
3. [ ] **文档应用**：每个平台一个，替换 Gallery；同时做内容抽取（`docs-gen` 产出页面与示例清单、示例注册改两级、覆盖率），API 表从 XML 文档注释生成。
4. [ ] **补齐组件与示例**：按上游的层级把 136 页做齐，每页过三道核对。
5. [ ] **收敛差异**：差异基线里超过允许值的逐个查，改我们这边或登记原因；WinForms 的行高、焦点环留白另列计划。

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
- 按钮家族（Button、ButtonBase、ButtonCore、IconButton、IconLinkButton、LinkButton、CloseButton、ClearButton、CopyButton）留在 `Base/Button`：上游是分开的文件，
  但它们共用一份主题文件与 ButtonCore，拆开后每个文件夹只剩一两个文件，还要拆主题。
- Code 与 Mark 留在 `Base/Code`：共用主题文件（WinForms 还共用 ChipText），Mark 只有几行。
- Progress 一个文件五百多行：条形、环形共用状态与动画，拆开反而要来回跳。
- WinForms 的 `Elements/`、`Reactivity/` 是这个平台自己的基础设施（无窗口元素、信号），上游没有对应。

待做（下一轮）：
- [ ] 测试里其余的局部辅助方法（按钮的模板部件、WPF Switch 的部件样式键查找）换成 `Lookup` 的写法。
- [ ] Gallery 改名为文档应用时，示例注册改两级（见上面的"内容从上游抽"）。
