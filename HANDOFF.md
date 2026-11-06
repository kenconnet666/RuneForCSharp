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

工作约定见用户的全局 CLAUDE.md：
- 对话、注释、文档用中文；代码、commit message 用英文。
- 整理与功能改动分开提交。
- 大的结构改动先给方案。

## 2. 现在的状态

- 阶段 0 骨架完成：三个平台都有组件库、示例应用和测试工程，基础设施都配了测试。DESIGN §2 有目录说明。
- Release 构建零警告，37 个测试全部通过（Avalonia 4、WPF 15、WinForms 18）。
- 前置探测都做完了，结论写进了 DESIGN（档位类型 §3、组合档位 §4、部件转发 §7、主题切换 §9、热重载 §11）。
- 开工前要定的事都按建议定下了（ROADMAP"已定的决定" 10–18），"待定的决定"现在是空的。
- **还没有任何组件**。阶段 1 已完成：`tools/ts-inspect`、`token-gen`、`lang-gen`、`icons-gen`、`parity`（三个库都有了 16 套主题的 token、中英文语言包和 Lucide 全集）。下一步是阶段 2。用户要求无人值守持续执行，规矩见 ROADMAP"自主执行的规矩"。

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
- 自检：`~/.dotnet/dotnet tool run csharp-ls -- --diagnose -s RuneForCSharp.slnx`（要先设 `DOTNET_ROOT=~/.dotnet`），输出里没有"失败"即可。
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
| 取 TS 文档站的计算样式（`ts-styles`） | 还没做（阶段 2） |
| 截图、元素树、属性来源、UIA 树（各平台的 `inspect`） | 还没做（阶段 2） |
| 基准（`inspect bench`） | 还没做（阶段 2） |
| 重新生成 token（改了 vue-tsx-pro 的主题后） | `node tools/token-gen/token-gen.mjs` |
| 核对生成的 token 与 vue-tsx-pro 一致（CI 用） | `node tools/token-gen/token-gen.mjs --check` |
| 重新生成语言包（改了 vue-tsx-pro 的语言包后）；`--check` 只核对 | `node tools/lang-gen/lang-gen.mjs` |
| 重新生成 Lucide 图标（vue-tsx-pro 升级了 @lucide/icons 后）；`--check` 只核对 | `node tools/icons-gen/icons-gen.mjs` |
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

1. `tools/ts-inspect` 已做好（命令表里有用法）。移植组件前先跑 `component <名字>`，比通读源码快。
2. 阶段 1 的生成脚本与 parity 都已做好（DESIGN §6，命令表）。
3. 阶段 2：先做 Gallery 的示例登记表和三个平台的 `inspect`（截图、元素树、属性来源、UIA 树），再做基础设施层（部件转发与合并、作用域、主题、语言、交互工具），用 Icon → Text → Spinner → Badge → Button 家族 → Field 验证。WPF 的 Provider 要递归合成嵌套的部件样式；WinForms 把 4.1 的做法写成正式的 `ElementHost`，部件属性记录的合并用源代码生成器。
4. 阶段 3：按依赖分层从下往上做组件（ROADMAP 里有五层的清单）。
5. WPF 的 XAML 热重载要在 Rider 2026.2 或 VS 里手动试（DESIGN §13）。
