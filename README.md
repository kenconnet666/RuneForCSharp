# RuneForCSharp

对标 [vue-tsx-pro](https://github.com/kenconnet666/vue-tsx-pro) 组件库的三套 .NET 桌面组件库：

| 库 | 平台 | 外观怎么写 |
|---|---|---|
| **Rune.Avalonia** | Avalonia 12（跨平台） | ControlTheme + 伪类档位，原生级联 |
| **Rune.Wpf** | WPF（.NET 10，Windows） | 主题样式触发器 + Provider 的分层合并 |
| **Rune.WinForms** | WinForms（.NET 10，Windows） | 信号驱动的自绘组件 |

三套库各自独立、写法贴合各自平台。共同的特点：

- 上层样式覆盖下层，并且按层合并：组件 < 档位 < 应用 < 外层 Provider < 内层 Provider < 使用方。
- 尺寸用 rem / em，可以整体缩放。
- 只更新该更新的节点。
- 开发期支持热重载。

设计与取舍见 [docs/DESIGN.md](docs/DESIGN.md)，进度见 [docs/ROADMAP.md](docs/ROADMAP.md)，接手先读 [HANDOFF.md](HANDOFF.md)。

## 开发

需要 .NET SDK 10.0.400（见 `global.json`）。WPF、WinForms 只能在 Windows 上构建。

```bash
dotnet build RuneForCSharp.slnx
```

```bash
dotnet test --project avalonia/tests/Rune.Avalonia.Tests
```

```bash
dotnet test --project wpf/tests/Rune.Wpf.Tests
```

```bash
dotnet test --project winforms/tests/Rune.WinForms.Tests
```

示例应用在各平台的 `apps/` 下。改 C# 代码时用 `dotnet watch` 跑示例可以热重载；Avalonia 的示例在 Debug 构建下还能热重载 axaml。

## 许可

MIT
