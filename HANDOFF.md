# 交接

接手的人（或换了电脑的自己、新的 Claude 会话）先读这份，再读 [docs/DESIGN.md](docs/DESIGN.md) 和 [docs/ROADMAP.md](docs/ROADMAP.md)。

最后更新：2026-11-06。

## 1. 项目是什么

三套 .NET 桌面组件库：**Rune.Avalonia**、**Rune.Wpf**、**Rune.WinForms**。对标 vue-tsx-pro 的 `packages/ui`，three（3D）部分不做。

已经定下的方向（用户决定，不要再提议推翻）：

- **以 vue-tsx-pro（TS 版）为准**。RuneForBlazor（<https://github.com/kenconnet666/RuneForBlazor>）是同源的 Blazor 版，还在开发中，只作参考，不共享代码。
- **不做单源、不做核心包**：三个库各自独立，写法贴合各自平台。一致性靠脚本维护：从 vue-tsx-pro 生成 token、主题、语言包、图标，再做名字对照检查。
- **核心特点是分层合并**：组件 < 档位 < 应用 < 外层 Provider < 内层 Provider < 使用方。上层只写要改的部分。可以加适量的运行时。
- **尺寸沿用 rem / em**，便于整体缩放和响应式。
- **细粒度更新**：只更新该更新的节点。
- **开发期尽量热重载**。
- **骨架要能支撑长期开发**，不要临时目录。

工作约定见用户的全局 CLAUDE.md：
- 对话、注释、文档用中文；代码、commit message 用英文。
- 整理与功能改动分开提交。
- 大的结构改动先给方案。

## 2. 现在的状态

- master 上有 5 个提交：构建配置、三个平台的骨架、文档。
- 三个平台都有组件库、示例应用和测试工程，基础设施都配了测试。DESIGN §2 有目录说明。
- Release 构建零警告，36 个测试全部通过（Avalonia 4、WPF 15、WinForms 17）。
- **还没有任何组件**。下一步是 ROADMAP 的阶段 1（生成脚本）和阶段 2（打通基础设施）。

本地命令：

```bash
dotnet build RuneForCSharp.slnx -c Release
```

```bash
dotnet test --project wpf/tests/Rune.Wpf.Tests
```

## 3. 换电脑后要准备的

- **.NET SDK 10.0.400**（`global.json` 固定版本，可向上滚动到新的 feature band）。WPF、WinForms 只能在 Windows 上构建。
- **对照仓库**：
  - vue-tsx-pro：<https://github.com/kenconnet666/vue-tsx-pro>，旧电脑上在 `C:\Users\lionheart\IdeaProjects\vue-tsx-pro`
  - RuneForBlazor：旧电脑上在 `C:\Users\lionheart\RiderProjects\RuneForBlazor`
  - 新会话里把它们加进工作目录，主要看 `packages/ui/src` 和 `packages/ui/COMPONENTS.md`。
- **旧电脑上的一个坑**：`C:\Program Files\dotnet` 只装了 .NET 11 预览版运行时，.NET 10 在 `~\.dotnet`。
  - 直接运行编译出来的 `.exe` 会报找不到运行时，要用 `dotnet xxx.dll` 运行，或者设 `DOTNET_ROOT`。
  - `dotnet test` 不受影响。新电脑装好 SDK 就没有这个问题。
- **探测工程不在仓库里**：放在旧电脑的临时目录，换电脑后就没了。结论和数字都已经写进 DESIGN.md；下面第 4 节记录了最后一轮还没写进去的结果，以及值得复用的做法。

## 4. 最后一轮探测（2026-11-06，部分未写进 DESIGN）

### 4.1 WinForms 无窗口元素：焦点、键盘、无障碍（全部通过，已写入 DESIGN §9）

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
- **实测结果**（UI Automation 客户端看到的）：
  - 宿主是 ToolBar，子节点是 3 个 Button，有名称、有位置；
  - 有焦点的子节点 `HasKeyboardFocus` 为 true；
  - 子节点支持 **InvokePattern**，调用后元素被触发；
  - 按屏幕坐标 `FromPoint` 能命中元素。
- 测试注意：
  - 模拟 Tab 用 `Message.Create(handle, WM_KEYDOWN, Keys.Tab)` 交给 `PreProcessMessage`；Shift 状态读的是真实键盘，模拟不了，直接调内部方法测。
  - UIA 客户端要在后台线程调用，UI 线程同时跑消息循环。

### 4.2 整套主题切换的开销（未写进 DESIGN）

3000 个控件，每个用 4 个主题 token（DynamicResource），亮色切暗色：

| 平台 | 应用级切换（3000 个） | 局部切换（300 个） |
|---|---|---|
| Avalonia（headless，不含渲染） | 273 ms，属性变化 9000 次 | ThemeVariantScope：11 ms，900 次 |
| WPF（离屏窗口） | 换 Application 的合并字典：363 ms，9000 次 | 换局部的合并字典：28 ms，900 次 |
| WinForms | 未测完（会话中断）。做法：主题 token 是 `Signal`，3000 个无窗口元素各一个绘制 effect，改 3 个 token 看通知、失效与重画的耗时 | — |

结论：
- 两个 XAML 平台的 token 全用 DynamicResource，整页切换在几百毫秒量级，可以接受。
- 局部主题（Provider 换主题）只影响作用域内部，开销与作用域大小成正比。

### 4.3 还没做的探测（按优先级）

1. WinForms 主题切换耗时（见上）。
2. **档位类型**：enum 还是可扩展的字符串包装类型（record struct 加 TypeConverter，或者静态 `Parse`）。要看两件事：
   - Avalonia 编译期 XAML（`Variant="solid"`）和伪类映射是否顺手；
   - WPF 的 `Trigger Value="solid"` 能否转换、相等比较是否正确。
   TS 版允许使用方扩展档位取值。
3. **Avalonia 组合档位**：直接写 36 个组合样式（实测 52 个嵌套样式时创建慢约 37%），还是把色调做成调色板对象（控件按 Tone 取一组画刷，档位样式只有 6 个）。两种都量一下创建耗时和主题切换。
4. **Avalonia 桌面窗口里改库的 ControlTheme 能否被 HotAvalonia 热重载**。headless 下观察到已有控件不刷新。可以用真实 Win32 窗口放在屏幕外测（`UsePlatformDetect`，`Position = (-10000, -10000)`）。
5. WPF 在 Rider 2026.2 或 VS 里改 `Provider.Styles` 能否 XAML 热重载：需要在 IDE 里手动试。

## 5. 下一步建议

1. 把 4.3 的第 2、3 项做完，定下档位类型和 Avalonia 组合档位的写法（ROADMAP 阶段 2 的前置决定）。
2. 阶段 1：`tools/token-gen`，从 vue-tsx-pro 的 `packages/ui/src/config/theme` 生成三种格式。RuneForBlazor 的 `tools/theme-gen` 读的是同一个数据源，可以参考它怎么解析。
3. 阶段 2：每个库做 Button 家族，把 Provider 的 defaults / styles、size 与 em、主题切换、减少动态效果、热重载钩子串起来。WinForms 同时把 4.1 的做法写成正式的 `ElementHost`。

ROADMAP 末尾还有几个要用户拍板的事项（推进节奏、WinForms 范围、热重载方案、对照粒度、CI），开工前先问。
