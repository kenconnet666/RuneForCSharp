# 路线图

## 阶段

### 0. 骨架（已完成）

- [x] 仓库结构：按平台分目录，每个平台有 src、apps、tests；集中的构建设置与包版本（DESIGN §2）。
- [x] 已验证的基础设施，都配了测试：
  - Avalonia：`EmExtension`、`PseudoClassesExtensions`、`RuneTheme` 入口。
  - WPF：`Provider`（分层合并）、`Em`、`Container`、`BrushTransition`、`Motion`、`HotReload`。
  - WinForms：`Signal` / `Computed` / `Effect`、`Easing`、`Animator`、`Motion`、`HotReload`。
- [x] 三个示例应用：Avalonia 的 Debug 构建带 HotAvalonia。

### 1. 生成脚本（tools/）

数据源都是 vue-tsx-pro。CI 里固定一个提交，跑 `--check`，核对生成文件与源码一致。

- [ ] `token-gen`：从 `packages/ui/src/config/theme` 读 tokens 和 16 套主题，输出三种格式：
  - Avalonia 的 `.axaml` 资源字典（按 ThemeVariant 分）；
  - WPF 的 `.xaml`；
  - WinForms 的 `Tokens.g.cs`。
  - em 类 token 输出成系数。动效 token 在 Avalonia 里输出成按时长档位的 Transitions 资源（DESIGN §8）。
- [ ] `lang-gen`：从 `config/lang` 读语言包，生成 zh-CN、en-US。
- [ ] `icons-gen`：Lucide 的路径数据。Avalonia、WPF 转 Geometry，WinForms 转 GraphicsPath。
- [ ] `parity`：用 TS 编译器 API 从 vue-tsx-pro 抽一份清单（组件、`css.recipe` 的 name / parts / variants、ComponentDefaults），和三个库对照，报告缺失和改名。

### 2. 打通基础设施

每个库各做 Button 家族、Badge、Card、TextField，把下面这些落地：

- [ ] Provider 的 defaults 与 styles；
- [ ] size 档位与 em；
- [ ] 主题切换（亮暗、16 套主题）和局部主题；
- [ ] 减少动态效果；
- [ ] 热重载钩子。

这一阶段还要定下这些做法：

- [ ] 档位类型用 enum 还是可扩展的字符串包装类型（TS 版允许使用方扩展档位取值）。
- [ ] Avalonia 组合档位的写法：直接写 36 个组合样式，还是把色调做成调色板对象。
- [ ] WinForms 的无窗口元素宿主：布局、焦点、键盘、无障碍、命中测试。

还要补上 DESIGN §13 里的桌面和 IDE 实测。

### 3. 按批次移植组件

顺序参照 vue-tsx-pro 的 COMPONENTS.md §10：base → feedback → display → input → form → navigation → overlay → layout → table → date → patterns。

charts、maps、diagram、code-editor、xlsx 这些子包做不做、什么时候做，等前面完成后再定；three 不做。

## 待定的决定

- 推进节奏：同一批组件三个库一起做，还是 Avalonia 先行、WPF 跟上、WinForms 最后？
- WinForms 的范围：全量做，还是常用子集？它全靠自绘，工作量最大。
- Avalonia 热重载：用 HotAvalonia（MIT），还是官方授权版？
- 对照粒度：parity 只检查名字，还是也截图比对样式取值？
- CI：WPF、WinForms 只能在 Windows 上构建测试，Avalonia 另加 Linux。
