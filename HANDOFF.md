# 交接

接手的人（或换了电脑的自己、新的 Claude 会话）先读这份，再读 [docs/DESIGN.md](docs/DESIGN.md) 和 [docs/ROADMAP.md](docs/ROADMAP.md)。

最后更新：2026-11-06。

## 1. 项目是什么

三套 .NET 桌面组件库：**Rune.Avalonia**、**Rune.Wpf**、**Rune.WinForms**。对标 vue-tsx-pro 的 `packages/ui`，three（3D）部分不做。

已经定下的方向（用户决定，不要再提议推翻）：

- **以 vue-tsx-pro（TS 版）为准，外观尽量一致；实现与 API 用各平台更合适的写法**（ROADMAP 的"平台化的约定"）。RuneForBlazor（<https://github.com/kenconnet666/RuneForBlazor>）是同源的 Blazor 版，还在开发中，只作参考，不共享代码。
- **不做单源、不做核心包**：三个库各自独立，写法贴合各自平台。一致性靠脚本维护：从 vue-tsx-pro 生成 token、主题、语言包、图标，再做名字对照检查。
- **核心特点是分层合并**：组件 < 档位 < 应用 < 外层 Provider < 内层 Provider < 使用方。上层只写要改的部分。可以加适量的运行时。
- **每个子节点的 API 都能转发，层层合并**（同 TS 的 `propsXxx`，嵌套组件的部件也能从最外层改到，DESIGN §7）。
- **先做基础的再做上层的**：先基础设施，再按 vue-tsx-pro 的依赖分层从下往上做组件（ROADMAP 阶段 2、3）。
- **尺寸沿用 rem / em**，便于整体缩放和响应式。
- **细粒度更新**：只更新该更新的节点。
- **开发期尽量热重载**。
- **骨架要能支撑长期开发**，不要临时目录。
- **上层组件建在基础设施与基础组件上**：上层要的能力先在基础层补齐再用，不在上层写平行实现（2026-11-06 用户强调，vue-tsx-pro 同样遵守）。

工作约定见用户的全局 CLAUDE.md：
- 对话、注释、文档用中文；代码、commit message 用英文。
- 整理与功能改动分开提交。
- 大的结构改动先给方案。

## 2. 现在的状态

- 阶段 0 骨架完成：三个平台都有组件库、示例应用和测试工程，基础设施都配了测试。DESIGN §2 有目录说明。
- Release 构建零警告，约 175 个测试（Avalonia 53、WPF 61、WinForms 61）；WPF 的 `BrushTransitionTests.Background_interpolates…` 在机器忙时本地会失败（逐帧取样），CI 上通过。
- 2026-11-07 的进展（里程碑 M0，见 ROADMAP 的 M0 清单）：跟上游 0.12.0-dev（`tools/vue-tsx-pro.ref`）；Tone → TextTone；按钮字重 500；控件高度 `ControlHeight`；档位的优先级（Avalonia `Resolved` / `TemplateResources`，WPF 主题触发器）；Badge 与按钮同一张调色板表；行高系数 `Leading`；可聚焦的不可用（Avalonia、WPF）。
- M1（2026-11-07 起）：Kbd、Code、Mark、Blockquote、StatusDot、H1–H6、Link、CodeText、List、Indicator，布局的 Flex、Grid、Separator、Container，Toolbar、Prose、StatusBadge、Skeleton、SkeletonText、Card、Avatar、Empty、Collapse、Progress、Switch、Checkbox、Radio（勾选类共用 `ChoiceBase`）、Field、CopyButton、Tooltip（浮层的位置类型 `FloatingPlacement` 是孪生文件）、ScrollArea 三个平台都做了，接着按 docs/DOCSITE.md 第三节的顺序做文档站要用的组件。孪生文件（三个库各一份的纯计算或纯数据：FlexLayout、GridLayout、StatusMap、AvatarInitials、ListboxOption、FloatingPlacement、ScrollAreaVariants）改一份要照抄另外两份，`node tools/twins/twins.mjs` 核对。parity 的登记表多了 `TYPE_RENAMES`（平台上改了名的组件，如 WinForms 的 List → UnorderedListControl）与 `PLATFORM_PROP_RENAMES`（WinForms 的 size → TextSize）、`PLATFORM_PROPS_NOT_PORTED`（某个平台上不移植的 prop）。WinForms 的截图（inspect snapshot）自己按 z 序逐层画：DrawToBitmap 把叠在上面的兄弟控件画到下面。
- 对照工具：`ts-styles` + `inspect styles` + `tools/parity/styles.mjs`（取值，Avalonia、WPF 上 Button、Badge、Text、Icon、IconButton 都一致）；`tools/parity/behavior.mjs`（上游测试用例 → 我们的测试，`[Upstream]` 标注）；`ts-shots` + `tools/parity/pixels.mjs`（像素，docs/DOCSITE.md 第一节）。做组件时三样都跑。
- 前置探测都做完了，结论写进了 DESIGN（档位类型 §3、组合档位 §4、部件转发 §7、主题切换 §9、热重载 §11）。
- 要定的事都定下了（ROADMAP"已定的决定" 10–20），"待定的决定"现在是空的。
- 阶段 1 已完成：`tools/ts-inspect`、`token-gen`、`lang-gen`、`icons-gen`、`parity`（三个库都有了 16 套主题的 token、中英文语言包和 Lucide 全集）。阶段 2 进行中：档位类型（`TextSize`、`TextTone`）与 Icon 三个平台都做了；WinForms 有了无窗口元素的基础（`Element`、单元素宿主 `ElementControl<T>`、应用级主题信号 `RuneTheme.Current`）。三个平台都有了示例登记表（`apps/Rune.*.Examples`，Gallery 与 inspect 共用）和 `inspect`；CI（`.github/workflows/ci.yml`）按平台并行跑全量，推送到 GitHub 后跑（2026-11-06 第一次推送）。Text 家族（Text、Paragraph、Strong、Em、Small）三个平台都做了；WPF 从 TextBlock 派生（决定 19）。Spinner（第一个带部件的组件）三个平台都做了，部件转发的三种写法都落地了。Badge（第一个自绘、带配方的组件）三个平台都做了，自绘组件的部件转发见决定 20。WPF Provider 的"部件键叠类型键"（决定 19，`PartKey`）与 Style.Resources 的递归合成都做了。Button 三个平台都做了（调色板 / 生成的配方、波纹、颜色过渡、焦点环、加载指示防闪烁，效果照 TS；DESIGN §14）。按钮家族照 TS 分层：ButtonBase（无外观，Avalonia、WPF）→ ButtonCore（外观内核）→ Button、IconButton；IconButton、CloseButton、ClearButton、LinkButton、IconLinkButton 三个平台都做了；语言包有了运行时（Avalonia、WPF 的 `Provider.Lang`，WinForms 的 `RuneLang.Current`，没写时跟随界面文化）。接着做 ButtonGroup、CopyButton。已知缺口：三个平台的 focusableWhenDisabled（平台上 IsEnabled=false 不能聚焦，要另做"可聚焦的不可用"）、WinForms 的 propsContent 与 ButtonBase。vue-tsx-pro 那边要做一轮样式体系改造，会按阶段发通知过来；对应关系与跟进记在 [docs/UPSTREAM.md](docs/UPSTREAM.md)（2026-11-06 只做了评估，等上游定稿再动代码）。用户要求无人值守持续执行，规矩见 ROADMAP"自主执行的规矩"。

## 3. 环境

- **.NET SDK 10.0.400 以上**（`global.json` 固定 10.0.400，可向上滚动到新的 feature band）。WPF、WinForms 只能在 Windows 上构建。
- **这台电脑的坑（和旧电脑一样）**：`C:\Program Files\dotnet` 只有 .NET 8 与 11 预览版，.NET 10（10.0.403）在 `~\.dotnet`。
  - 用 `~/.dotnet/dotnet` 执行构建与测试；直接运行编译出来的 `.exe` 前设 `DOTNET_ROOT=~/.dotnet`，否则报找不到运行时。

### C# 的 LSP（Claude Code 项目级插件）

- `.claude/settings.json` 登记了仓库内的插件市场（`.claude/marketplace`）并启用 `rune-csharp-lsp`。登记只让 Claude Code 认识这个市场，**插件还要安装一次**，装好后重启会话才生效；之后 Claude 的 LSP 工具能对 `.cs` 查定义、引用、悬停，编辑后推送诊断。
  - 有 `claude` 命令行时在交互式终端里用 `/plugin` 安装。
  - 这台机器没有命令行（只有桌面应用），是照 Claude Code 的格式手动装的：把 `.claude/marketplace/rune-csharp-lsp` 复制到 `~/.claude/plugins/cache/rune-for-csharp/rune-csharp-lsp/1.0.0`，再在 `~/.claude/plugins/installed_plugins.json` 里加一条 `rune-csharp-lsp@rune-for-csharp`（scope 为 project，projectPath 是仓库目录），格式照抄已有的条目。改了插件的 `.lsp.json` 后要重新复制。
- 语言服务器是 `dotnet-tools.json` 固定的 csharp-ls 0.28.0（MIT，Roslyn）。启动脚本 `tools/lsp/csharp-ls.mjs` 找到装有 global.json 所要 SDK 的 dotnet（这台机器是 `~\.dotnet`），按需 `dotnet tool restore`，再加载 `RuneForCSharp.slnx`。
- **先 Debug 构建一次**：WPF 的 Gallery 与测试工程的 XAML 引用了 Rune.Wpf 的类型，库的 Debug 输出不存在时这两个工程在语言服务器里加载失败。
- 自检：`~/.dotnet/dotnet tool run csharp-ls -- --diagnose -s RuneForCSharp.slnx`（要先设 `DOTNET_ROOT=~/.dotnet`），输出里没有"失败"即可。语言服务器的错误输出另存在系统临时目录的 `rune-csharp-ls.log`；Claude Code 连崩三次就停用它，要重启会话才会重新拉起。
- 用法：改 C# 时先用 LSP 查定义、引用、诊断，不靠文本搜索猜；语言服务器没起来时以构建结果为准。axaml / xaml 没有语言服务器，靠构建与测试。

### 命令表

查信息、做验证的命令都记在这里；ROADMAP"信息与验证工具"里的工具每做好一个，就补一行。

| 做什么 | 命令 |
|---|---|
| 构建 | `~/.dotnet/dotnet build RuneForCSharp.slnx -c Release` |
| 测试（换成 avalonia / winforms 的测试工程同理） | `~/.dotnet/dotnet test --project wpf/tests/Rune.Wpf.Tests` |
| 开 vue-tsx-pro 文档站（对照示例与交互，端口 5174） | 在 vue-tsx-pro 目录下 `pnpm dev:docs` |
| 查 TS 组件：props、部件、配方逐条 token、样式表、默认值、语言包、依赖 | `node tools/ts-inspect/ts-inspect.mjs component Button` |
| 查 token：16 套主题里的取值；不带名字列出所有组 | `node tools/ts-inspect/ts-inspect.mjs token accentHover` |
| 一组 token，加几列主题 | `node tools/ts-inspect/ts-inspect.mjs token --group colors --themes dark,paper` |
| 查语言包：键、说明、中英文案 | `node tools/ts-inspect/ts-inspect.mjs lang select` |
| 依赖分层表 / 一个组件往下用到、往上被用到的 | `node tools/ts-inspect/ts-inspect.mjs deps`、`… deps Select` |
| 以上都能加 `--json`；`--vue <路径>` 或环境变量 `VUE_TSX_PRO` 指定 vue-tsx-pro | `node tools/ts-inspect/ts-inspect.mjs --help` |
| 跑一份 vue-tsx-pro 文档站（ts-styles 用；不碰 vue-tsx-pro 会话的工作区）：在短路径下克隆、离线安装、构建、起开发服务器（端口 5274，路径长了 pnpm 写不了任务状态） | `git clone --shared ../../IdeaProjects/vue-tsx-pro ~/.rune/vtp-docs`，`git -C ~/.rune/vtp-docs checkout <tools/vue-tsx-pro.ref>`，在里面 `pnpm install --frozen-lockfile --offline --ignore-scripts`、`pnpm build`，再在 `apps/docs` 下 `npx vite --configLoader native --port 5274 --strictPort`（后台） |
| 取 TS 文档站一页的计算样式：每个示例、每个部件（scope/part#序号），亮暗主题；`--states` 另外强制悬停、聚焦、按下；结果写 `artifacts/ts-styles/<页面>.json` | `node tools/ts-styles/ts-styles.mjs button [示例,…] --vue ~/.rune/vtp-docs` |
| 我们这边同样格式的取值（Avalonia、WPF；WinForms 还没有） | `… Rune.Avalonia.Inspect.exe styles Button/variants [--theme dark] [--json]` |
| 取值对照：文档站对一个平台，逐个部件、逐个属性（文字决定的宽高不比，长度容差 0.5px），有差异时退出码 1 | `node tools/parity/styles.mjs button [示例,…] --platform avalonia` |
| 列出示例（"组件/示例名"，Gallery 与 inspect 共用的登记表） | `avalonia/tools/Rune.Avalonia.Inspect/bin/Release/net10.0/Rune.Avalonia.Inspect.exe list`（先 `export DOTNET_ROOT=~/.dotnet`） |
| 截文档站每个示例的效果区（亮暗、1x / 2x，另存文字盒子），写到 `artifacts/ts-shots/<页面>/` | `node tools/ts-shots/ts-shots.mjs checkbox [示例,…]` |
| 像素对照：两边同样条件出图（宽 756、留白 21、至少高 112 且内容竖直居中、`--exact` 不取整），盖掉文字、容 1 像素位移后逐像素比；报告 `artifacts/parity/pixels/<平台>/<页面>.html`；`all` 跑截过的全部页面出总表 | `node tools/parity/pixels.mjs checkbox --platform wpf` |
| 截图（PNG 写到 `artifacts/inspect/avalonia/`）；`--theme dark`、`--width 600`、`--padding 0`、`--min-height 112`（至少多高，内容竖直居中）、`--scale 2`、`--exact`（布局不取整） | `… Rune.Avalonia.Inspect.exe snapshot Icon/tones` |
| 元素树（类型、类与伪类、边界、所在模板） / 属性与来源（本地值、样式、模板、继承，画刷报出 token 名） / UIA 控件视图（角色、名称、Pattern）；都能加 `--json` | `… tree Icon/basic`、`… props Icon/tones`、`… uia Icon/accessibility` |
| WPF 的 inspect（同样的命令；tree 的"样式"列是隐式还是显式 Style，props 的来源分主题样式、样式触发器、资源引用等） | `wpf/tools/Rune.Wpf.Inspect/bin/Release/net10.0-windows/Rune.Wpf.Inspect.exe snapshot Icon/sizes` |
| WinForms 的 inspect（同样的命令，没有 `--scale`；tree 里单元素宿主下面列出它的元素，uia 是 MSAA 树） | `winforms/tools/Rune.WinForms.Inspect/bin/Release/net10.0-windows/Rune.WinForms.Inspect.exe uia Icon/accessibility` |
| 全量验证（三个平台并行构建、测试、截图；生成文件核对；parity 报告） | CI：`.github/workflows/ci.yml`（推送到 GitHub 后跑）；本地要跑全量时 `git worktree add ../RuneForCSharp-verify HEAD` 后在那边后台跑 |
| 基准：每个组件建 N 个，量创建、首次布局、渲染、换主题的耗时与分配、常驻内存（多轮中位数，平台自带的 Button 作对照）；`bench Button` 只跑名字以它开头的；`--count`、`--runs` | `… Rune.Avalonia.Inspect.exe bench --count 1000 --runs 6`（WPF、WinForms 的 inspect 同样） |
| 采样分析（找热点的调用链）：先 collect（exe 写绝对路径），再转 speedscope，再按帧汇总调用链 | `dotnet tool run dotnet-trace -- collect --output a.nettrace -- <exe 绝对路径> bench Button`；`dotnet tool run dotnet-trace -- convert a.nettrace --format speedscope -o a`；`node tools/trace/callers.mjs a.speedscope.json Monitor.Enter 8` |
| 锁定的上游提交（CI 检出它；工具读到别的提交时在错误输出里提醒）；跟进上游新版本时改它再重新生成 | `tools/vue-tsx-pro.ref` |
| 重新生成 token（改了 vue-tsx-pro 的主题后） | `node tools/token-gen/token-gen.mjs` |
| 核对生成的 token 与 vue-tsx-pro 一致（CI 用） | `node tools/token-gen/token-gen.mjs --check` |
| 重新生成语言包（改了 vue-tsx-pro 的语言包后）；`--check` 只核对 | `node tools/lang-gen/lang-gen.mjs` |
| 重新生成 Lucide 图标（vue-tsx-pro 升级了 @lucide/icons 后）；`--check` 只核对 | `node tools/icons-gen/icons-gen.mjs` |
| 重新生成组件配方（改了调色板表 `tools/token-gen/palette.mjs` 或配方表后）：WPF 的 MultiTrigger、WinForms 的颜色方法；`--check` 只核对 | `node tools/recipe-gen/recipe-gen.mjs` |
| 进度与名字对照：组件按层、三个库做了没有、缺哪些 props；语言包的键 | `node tools/parity/parity.mjs`（`--component Button` 只看一个，`--json`） |

- **对照仓库**（已加进 Claude 会话的工作目录）：
  - vue-tsx-pro：`C:\Users\Administrator\IdeaProjects\vue-tsx-pro`，主要看 `packages/ui/src` 和 `packages/ui/COMPONENTS.md`。
  - RuneForBlazor：`C:\Users\Administrator\RiderProjects\RuneForBlazor`，看 `docs/`（尤其 ROADMAP 的"Blazor 化的约定"）和 `tools/`（同一数据源的生成脚本）。
- **参考源码**：`C:\Users\Administrator\RiderProjects\refs\`，浅克隆，只读不构建。换电脑后照下表重新克隆：

| 目录 | 仓库 | 版本 | 看什么 |
|---|---|---|---|
| Avalonia | AvaloniaUI/Avalonia | 12.1.5 | 样式优先级、ControlTheme、Popup、自动化对等体 |
| Semi.Avalonia | irihitech/Semi.Avalonia | main | 成熟的 Avalonia 主题：档位与色调的写法 |
| Ursa.Avalonia | irihitech/Ursa.Avalonia | main | Avalonia 组件库：分页、对话框、时间线等组件的做法 |
| HotAvalonia | Kir-Antipov/HotAvalonia | v3.1.5 | 热重载怎么替换样式与视图 |
| wpf | dotnet/wpf | release/10.0 | 触发器、模板、Popup、UIA 对等体 |
| winforms | dotnet/winforms | release/10.0 | `AccessibleObject`、`ToolStripDropDown`、双缓冲 |
| HandyControl | HandyOrg/HandyControl | master | WPF 组件库的写法 |
| AntdUI | AntdUI/AntdUI | main | 全自绘的 WinForms 组件库（Ant Design 风格） |

- **探测工程不在仓库里**：放在会话的临时目录，结论和数字都写进了 DESIGN；下面第 4 节记录值得复用的做法。

## 4. 探测里值得复用的做法

### 4.1 WinForms 无窗口元素：焦点、键盘、无障碍（已写入 DESIGN §9）

一个宿主控件（一个 HWND）承载一组按钮元素，做法如下，以后的 `ElementHost` 照这个写：

- **焦点与 Tab**：宿主设 `ControlStyles.Selectable` 与 `TabStop`。
  - 重写 `ProcessDialogKey`：Tab / Shift+Tab 先在元素之间移动；走到头返回 `base.ProcessDialogKey`，交给窗体把焦点切到下一个控件。
  - `OnGotFocus` 里按 Shift 是否按下决定落在第一个还是最后一个元素。
- **键盘与鼠标**：`IsInputKey` 对 Enter、Space 返回 true，`OnKeyDown` 里触发当前元素；`OnMouseDown` 做命中测试，然后聚焦并触发。
- **焦点框**：只在 `Focused && ShowFocusCues` 时画（键盘操作时才显示，对应 `:focus-visible`）。
- **无障碍**：
  - 重写 `CreateAccessibilityInstance`，返回 `ControlAccessibleObject` 的子类，实现 `GetChildCount` / `GetChild` / `GetFocused` / `HitTest`。
  - 每个元素是一个 `AccessibleObject`，提供 Name、Role（PushButton）、Bounds（`RectangleToScreen`）、State（Focusable | Focused）、Parent、DefaultAction、`DoDefaultAction`、Navigate。子节点对象要缓存，不要每次新建。
  - 焦点移动时调 `AccessibilityNotifyClients(AccessibleEvents.Focus, index)`。
- **实测结果**（UI Automation 客户端看到的）：宿主是 ToolBar，子节点是 3 个 Button，有名称、有位置；有焦点的子节点 `HasKeyboardFocus` 为 true；子节点支持 **InvokePattern**；按屏幕坐标 `FromPoint` 能命中元素。
- 测试注意：
  - 模拟 Tab 用 `Message.Create(handle, WM_KEYDOWN, Keys.Tab)` 交给 `PreProcessMessage`；Shift 状态读的是真实键盘，模拟不了，直接调内部方法测。
  - UIA 客户端要在后台线程调用，UI 线程同时跑消息循环。

### 4.2 探测工程的写法

- **测量**：先预热两轮，再跑 3–5 轮取中位数；分配用 `GC.GetAllocatedBytesForCurrentThread()`。
- **Avalonia**：控制台程序里 `AppBuilder.Configure<App>().UseHeadless(...).SetupWithoutStarting()`，当前线程就是 UI 线程，`Dispatcher.UIThread.RunJobs()` + `UpdateLayout()` 推进。主题写成 axaml（编译期 XAML 才有代表性），几十上百条样式用 node 脚本生成。运行时 XAML 用 `AvaloniaRuntimeXamlLoader.Parse`（要引用 `Avalonia.Markup.Xaml.Loader`）。
- **Avalonia 热重载**：Debug 构建的桌面应用（窗口放在 `(-10000, -10000)`）定时把控件的属性打到标准输出；脚本启动它、几秒后用 sed 改 axaml、再读输出。
- **WPF**：运行时 XAML 的副本存成 `.txt`（`.xaml` 会被 SDK 自动当成 Page 编译）。
- **WinForms 绘制耗时**：画到 `BufferedGraphicsContext.Allocate` 出来的缓冲上，不要画到 GDI+ 的 Bitmap 上（DESIGN §9）。
- **vue-tsx-pro 的依赖分层**：node 脚本遍历 `packages/ui/src`（跳过 `.test`），用正则取值导入（跳过 `import type`），解析相对路径；组件文件只用 `utils/`、`config/` 的是 L0，否则取用到的组件文件的最大层号加 1。以后并进 `tools/parity`（ROADMAP 阶段 1）。

## 5. 下一步

2026-11-06 用户要求无人值守做完组件库的全部内容（只有发布暂时不做，扩展与 three 也尽量做），行为与样式尽量和 vue-tsx-pro 一致；上游有问题时通知维护 vue-tsx-pro 的会话修复发版。规划在 ROADMAP 的"里程碑"（M0–M6）与决定 21–25。

1. **M0**（ROADMAP 的 M0 清单）：上游 0.11 的 5 个工作包、按钮字重与控件高度；`ts-styles`、取值比对、行为对照、截图对比；已做的 16 个组件过三道核对；按钮家族的缺口。上游 0.11 合入发版后改 `tools/vue-tsx-pro.ref` 并重新生成。
2. **M1–M4**：按依赖分层从下往上做组件（ROADMAP 阶段 3 的五层清单，以 `parity` 的依赖表为准），每个组件按"自主执行的规矩"里的流水线走。
3. **M5、M6**：扩展（决定 23，全部扩展三个平台都尽量做）与收尾（打包试装，不发布）。
4. WPF 的 XAML 热重载要在 Rider 2026.2 或 VS 里手动试（DESIGN §13）。
