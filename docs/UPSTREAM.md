# 上游改造的跟进

vue-tsx-pro 那边的会话改了设计时会发通知过来（每完成一个阶段一次）。这里记每次通知对三个库意味着什么：哪些照搬、哪些换做法、哪些不涉及，以及跟进到哪了。
通知只是同步设计决定，**按各自的技术栈适当跟进**，不逐字照抄；上游没定稿的部分不动代码。
锁定的上游提交在 `tools/vue-tsx-pro.ref`（ROADMAP 决定 24）。

## 上游问题

移植时发现的 vue-tsx-pro 的问题：记一行，再在 GitHub 的 kenconnet666/vue-tsx-pro 开 issue、打 `parity` 标签（附复现与建议；2026-11-08 起，会话消息只用于通知），
那边的修复提交会关联 issue；修好、我们升级锁定后勾掉。

| 日期 | 组件 | 现象与复现 | 建议 | 状态 |
|---|---|---|---|---|
| 2026-11-07 | Checkbox、Radio、Switch | 原生 input 不继承字体，方框、轨道的 em 按浏览器给 input 的 13.333px 算，sm、lg 档与文字、滑块对不上 | input 加 `font: 'inherit'` | 已修（9f7be887，0.11.1；锁定跟到 875797ae） |
| 2026-11-07 | Listbox | multiple 示例写的 `height: 16rem` 落在有 padding: xs 的根元素上，默认 content-box，列表实际高 16rem + 0.5em；桌面上 Height 是整个控件，对照矮 7 像素 | listboxStyles.root 加 `boxSizing: 'border-box'`（同 control、itemStyles），或示例注明不含内边距 | 部分修复（313f0e23）：规则只罩带 data-part 的元素，Listbox 的根元素没有，ad4a77ac 上仍高 16rem + 0.5em；已再通知（2026-11-07） |

## 下一阶段计划（2026-11-07 收到通知，ROADMAP 阶段 25，上游无人值守执行）

上游每批推送照常通知，整个计划完成后再整体跟进一遍；下游问题以后用 GitHub issue（标签 parity）跟踪，会话消息只用来通知。

| 上游的定法 | 对我们 | 做法 |
|---|---|---|
| 还没有使用方：旧 API 直接删、不留 deprecated；每批 CI 绿了从 master 发版 | 我们同样没有使用方 | 跟到新版本时直接改名、删旧写法 |
| 样式引擎最低层统一 `box-sizing: border-box` | 修我们报的 Listbox 高度问题 | 等提交号，重截 listbox multiple |
| 组件里的导航链接部件（ItemList 标题、Card 页头操作、StatGroup 操作、Timeline 附件）默认悬停才有下划线 | 正文 Link 不变 | 做这些组件时照做 |
| P3 新组件：ChoiceCard、ItemList 可选中的行、ColorPicker、ImageCropper、Carousel、Dialog 拖动、Tag 加 selected、QRCode、FloatButton 组、JsonViewer、SignaturePad、DataTable 横向虚拟化 | Tag 刚做完 | selected 发版后补 |
| P10 把组件规格（属性、默认值、部件、档位、键盘）导成 JSON | 可以在 CI 里比对 | 已出（0.13.0，见第十一批），上线后让 parity 工具改读它 |

### 第一批（2026-11-07 通知，master a21e56f8）

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| box-sizing 统一（313f0e23）：Listbox multiple 示例正好 16rem | 修了我们报的高度问题 | CI 绿后锁定新提交，重截 listbox multiple；上游问题表那一行勾掉 |
| _hover、_active 在不可用时不算（0fc803a6） | 我们的不可用状态本来就不画悬停、按下 | 不涉及 |
| 样式引擎：焦点伪类优先级、globalRule、CSP nonce | 网页专用 | 不涉及 |
| 新组件 ErrorBoundary（b61938fb）、语言包 errorTitle、errorRetry | 语言包生成文件会变 | 锁定新提交时重新生成语言包；ErrorBoundary 等 Result |
| 输入框外壳只在点到空白处时转交焦点（1cc605e3） | 我们的外壳：Avalonia 按下按钮时不转交，WPF、WinForms 点外壳就聚焦里面的输入框 | 锁定新提交时核对三个平台，点在外壳里能聚焦的元素上不转交 |
| 文档站 IconButton 页并进 Button 页（7c7ac651） | ts-shots 的页名 icon-button 会失效 | 锁定新提交时把 IconButton 的示例挪到 button 页 |

跟进（2026-11-07，锁定 ad4a77ac）：语言包重新生成（ErrorTitle、ErrorRetry），token、图标、配方没变；三个平台的示例照新的 Button 页改（icon-buttons、presets、links 带上页脚的图标链接，删掉 IconButton 页）；
WinForms 的输入框外壳点在不能聚焦的部件（图标、前后缀）上时也把焦点交给输入框（Avalonia、WPF 本来如此）；listbox multiple 重截仍高 7 像素，见上游问题表。

### 第二批（2026-11-07 通知，master 到 a6ffbb54）

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| FilterBar 按钮区成为条件栅格的最后一格；QueryTable 查询栏与工具栏分开（a6ffbb54） | 还没做 | 做时照新的 |
| Grid / GridList 的 balanced（b0baadf5） | 已做 | 跟到 ad4a77ac 时做了：孪生文件 GridLayout 加 BalancedColumns，三个平台的 Grid 加 Balanced |
| Sparkline 的柱在 showLast 时最新一根实色，删 variant | 还没做 | 做时照新的 |
| 静态 Table 的 stickyHeader、单元格的 sticky（9a57a058） | 已做 Table | 等它的 CI 绿了下次锁定时做 |
| ProportionBar 图例从起始边排、列间距 lg；CommentThread、NotificationCenter 用 RelativeTime | 还没做 | 做时照新的 |
| Listbox 根元素补上部件标记，border-box 罩得到（ff185e89） | 修了我们报的高度问题 | 下次锁定时重截 listbox multiple |
| FilterBar / QueryTable 重做（cad81082，作废 a6ffbb54 那版）：标题单独一列，第一排条件 auto-fit 加按钮区（展开、高级筛选、actions、重置、查询、竖线、end），其余条件从第二排 auto-fill；minFieldWidth 默认 14em，加 propsBody、propsRow | 还没做 | 做时照 cad81082 |
| 文档站合页：NavMenu、NavBar 并进 NavList 页等 | NavList 页现有的示例 id 不变 | 新的 menu、nav-bar 示例等 NavMenu、NavBar |

### 第三批（2026-11-07 通知，master 到 ff221742）

锁定先跟到 d386edd8（当时 CI 绿的最新提交，含 554b7246、0950975c、0f69e5c5），6160d6d2、ff221742 的 CI 绿了之后再跟到 3c0c817b（含这一批的全部）。两次锁定语言包、token、图标、配方都没变。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| 单选、复选的卡片外观（554b7246）：card、横排按 minItemWidth（默认 14em）auto-fill；Radio、Checkbox 加 end、propsEnd | 已做 Checkbox、Radio | 这次做 |
| ItemList 整条可点、selected、disabled（0950975c） | 还没做 | 做时照新的 |
| 可选中的 Tag（0f69e5c5）：_selected 时是按钮（aria-pressed），没选中 surface、选中 solid 加对勾，默认 accent、sm | 已做 Tag | 这次做 |
| 静态 Table 的 stickyHeader、sticky（9a57a058，这次锁定才进来） | 已做 Table | 这次做 |
| ScrollArea 各部件带标记、border-box（6160d6d2） | 网页的部件标记与盒模型，桌面上本来就是 border-box | 不涉及 |
| 链接下划线 LinkUnderlineScope（ff221742）：显式 > 容器给的 > defaults.Link > always；ItemList、Stat、Timeline 等整块 hover，Card 只给页头 | 已做 Link、Card | 这次做；ItemList、Stat、Timeline、Descriptions、Checklist 做时照做 |
| Upload 接父级放进来的待上传文件（ff221742） | 还没做 | 做时照新的 |
| 文档：ButtonBase 页换成"长得不像按钮的按钮"；Upload、Timeline、Code 合并示例 | Code 页的示例变了 | 跟着像素差异改示例 |

### 第四批（2026-11-08 通知，master 到 89c01fc8）

锁定先跟到 f5d31cdd（含 237f97d2）：当时 77cffad3（FloatButton）的 CI 失败，cffe9ad9、89c01fc8 还在跑；89c01fc8 绿了之后随第五批跟到 91903a2b。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| Dialog 的 draggable（237f97d2）：按住标题栏拖，不超出窗口，每次打开回到正中 | 已做 Dialog | 这次做 |
| FileDrop、Upload 选文件夹（directory，f5d31cdd） | 还没做 | 做时照新的（桌面上是选文件夹的对话框、拖进来的文件夹递归读出文件） |
| FloatButton、FloatButtonGroup（速拨），BackTop 基于它（77cffad3） | 还没做 | CI 绿了锁定，做时照新的 |
| useInfiniteList、InfiniteListStatus，语言包 listNoMore；Select、Combobox、Mentions 的 load 分页（page、{ items, next }），ListboxView 的 onRangeChange 与 loading: 'more'（cffe9ad9） | Select 的远程选项还没做 | 语言包已重新生成（ListNoMore）；做 Select 的 load 时连分页、Listbox 的 RangeChange 一起做 |

### 第五批（2026-11-08 通知，master 到 fd628293）

锁定先跟到 91903a2b（QRCode，当时 CI 绿的最新提交；ad0a85ea 一次失败一次成功，728b56b4、fd628293 还在跑），语言包多了 ListNoMore、QrCode、QrExpired、QrRefresh、QrScanned；
之后跟到 0.12.0 发版（dda1276f，含这一批的全部），语言包又多了 JsonViewer 的 Json*、SignaturePad 的 Signature*。token、图标、配方都没变。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| QRCode 与自写的编码器 encodeQr（91903a2b） | 还没做 | 做时编码器做成孪生文件（纯计算），标准附录的向量一起搬进测试 |
| 子路径 ui/area：全国省市区（ad0a85ea、01a40e80） | 还没做 Cascader、TreeSelect | 做 Cascader 时照新的 |
| JsonViewer（728b56b4，建在 Tree 上） | 还没做 Tree | CI 绿后锁定，做时照新的 |
| SignaturePad、纸面配色 paper（fd628293）：要拍照、扫描、导出的一律浅底深色 | 还没做 | 语言包已重新生成；做 QRCode、SignaturePad 时用同一套纸面配色 |

### 第六批（2026-11-08 通知，0.12.0 之后 master 到 96efc7ad；P3 组件补缺完成）

锁定先停在 0.12.0（dda1276f）：这一批的组件提交 CI 都没绿；之后跟到 08906695（文档提交，CI 绿，含这一批全部）。语言包重新生成：
合并成 ZoomIn、ZoomOut、SlideCounter、ResetView（去掉 Lightbox*、Chart* 的同义键），新增 Crop*、Color*、Carousel*；token、图标、配方没变。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| Carousel（8b909944、c09d837b）：原生横向滚动加吸附、autoplay、页点、APG 轮播语义 | 还没做 | 做时照新的；桌面上滚动吸附要自己做 |
| 语言包破坏性合并：zoomIn、zoomOut、slideCounter、resetView 代替 lightbox*、chart* 的同义键 | 我们没用这些键（Lightbox、图表没做） | 锁定时重新生成语言包即可 |
| ImageCropper，usePanZoom 加 frame、cover，Upload 的 transform（88671dd1） | 还没做 | 做时照新的 |
| ColorPicker 与颜色工具；ColorField 的色块改成按钮弹出 ColorPicker（613b93bb，破坏性） | 还没做 ColorField | 做 ColorField 时直接照新的 |
| DataTable、TreeDataTable 的 virtualColumns（96efc7ad） | 还没做 DataTable | 做时照新的 |
| 下一步上游做 P2 文档站整改、P2.5 逐个组件精修，做完请我们整体跟进一遍 | | 到时按页重截、逐页对照 |

### 第七批（2026-11-08 通知，master 到 685a7553）

685a7553 的 CI 还在跑，锁定停在 08906695。没有新的 token 与语言包条目。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| 画布 /diagram：节点形状 circle、diamond，连线端点按形状求交，DiagramNode 的 active | 还没做 Diagram | 做时照新的 |
| useFormDialog 的表单 novalidate | 还没做 | 做时照新的（桌面上本来就没有浏览器的校验气泡） |
| LinkUnderlineContext 的值改成函数：LinkUnderlineScope 的 underline 变了里面的链接跟着变 | 已做 | 我们本来就跟着变：Avalonia 是继承的属性、WPF 在作用域变了时重新强制、WinForms 设作用域时重画里面的链接 |
| flow-ts-ui 删掉，流程设计器与审批面板改成文档站 patterns/Workflow 页里的组合 | 还没做这些组件 | 做 Workflow 页时照新的 |

### 第八批（2026-11-08 通知，master 到 58c09e7c，276e52e6 是文档修复）

685a7553 的 CI 失败、58c09e7c 还在排队，锁定仍停在 08906695。语言包会多 formSubmit、formReset、formAddRow、formRemoveRow，绿了锁定时重新生成。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| SchemaForm / SchemaFields：按 useForm 的 zod schema 生成字段（控件映射、meta.group 分组、columns 与 span、对象数组可增删的行），emptyValues、schemaFields、schemaFilters、schemaConditionFields | 还没做 Form（useForm）；桌面上没有 zod | 做 Form 时一起定方案：schema 换成 .NET 这边的描述（特性标注的模型或描述对象），控件映射、分组、栏数、增删行照上游 |

### 第九批（2026-11-08 通知，master 8c2f40a8，阶段 24.4 删旧写法，破坏性）

8c2f40a8 的 CI 绿了之后锁定跟到它（含第七、八批）：语言包多了 FormSubmit、FormReset、FormAddRow、FormRemoveRow；token、图标、配方没变。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| createUi / Provider 去掉 defaults、styles 两个选项，只用 components（`{ Button: { defaults, styles, variants } }`），styles.Tab 别名删 | Avalonia、WPF 的 Provider 是平台的样式与资源字典；WinForms 的 ProviderControl 是按组件分的强类型记录（`Defaults.Set(new ButtonDefaults …)`、`Styles.Set(new ButtonSx …)`），本来就按组件归，不是旧写法的别名 | 不改；以后做变体（variants）时再看要不要并成一个按组件的入口 |
| Skeleton 的 variant → shape（SkeletonVariants 删） | 我们没带 variant | registry 里“上游已废弃”那一条删了 |
| WaveGrid 的 variant → type，TimelineChart 系列 variant → type | 还没做 | 做时照新名 |
| Tones / Tone → TextTones / TextTone | 我们的色调类型本来叫 TextTone | 不涉及 |
| tsx 的 Theme → VarTheme；配方 compound 的 when → match | 当前锁定已经全是 match | ts-inspect 读组合条件改认 match（原来找 when，组合的条件读不出来） |

跟进时顺带：上游把“有内容时显示清空按钮，清空后回到空字符串”挪到了 `describe('Textarea 的 clearable')` 下（TextField 那条改名为“未声明的属性落到 input；外壳用 propsRoot；清空后写回并聚焦 input”）：
三个平台的 Textarea 测试改标 Textarea，TextField 测试上旧名的标记删掉（行为对照 193 条、55 条没对上）。

### 第十批（2026-11-08 通知，master 83ee1aac…4a7a0d4e，阶段 24.1 第五批部件补齐）

这些提交的 CI 还在跑或失败，锁定停在 8c2f40a8。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| 组件内部节点统一带 data-scope / data-part，[data-part] 一律 border-box；部件属性按“默认 < 使用方的 < 组件管的（值、无障碍、事件）”合并 | 网页的标记与盒模型；部件的合并顺序我们本来如此（DESIGN §7） | 不涉及 |
| 新增部件属性：CommentThread、CommentComposer、Checklist、Cascader、KanbanBoard、CalendarBoard、ColorPicker、DiagramNode | 这些组件都还没做 | 做时照新的部件 |
| AppShell、FilterBar、Timeline、Tree、ItemList 等只补了标记 | AppShell 已做，部件属性不变 | 不涉及 |

锁定跟到 95a6c2ee（这一批全部绿了）：生成文件、对照结果都没变。

### 第十一批（2026-11-08 通知，0.13.0 发版）

0.13.0 的发版提交（0f554523）CI 还在跑，锁定停在 95a6c2ee。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| 组件规格 JSON：文档站构建时输出 `/spec/components.json`（`{ package, version, components: [{ name, entry, category, api: { props: [{ name, kind, type, required, default, bindable, values }], parts: { scope, names }, customizable } }] }`，267 个组件） | 现在的 parity.mjs 用 TypeScript 解析上游源码取属性与部件 | 上线后（通知时还是 404）把 parity 改成读这份 JSON：属性、默认值、可绑定、部件逐项比，默认值也进 CI |
| GitHub 的 parity 标签：要上游改的开 issue 打 parity 标签，会话消息只用于通知 | 上游问题的报法变了 | 照办（本文“上游问题”一节已改） |
| P6（/testing 子路径、vtp-ui create 模板） | 网页端的工具 | 不涉及 |
| 部件补齐第六批：Descriptions、Banner、FormErrorSummary、Carousel、QRCode、CharacterCount、Steps、Breadcrumbs、Splitter 等只补 data-scope/data-part 标记 | 网页的标记；CharacterCount 已做，部件属性不变 | 不涉及 |

### 第十二批（2026-11-08 通知，master 9b155f35、c6b0309d：会被裁掉的焦点环画在里面）

这两个提交的 CI 还在排队，锁定停在 95a6c2ee。

| 上游的变化 | 对我们 | 做法 |
|---|---|---|
| Listbox 自己滚动时焦点环画在里面（focusRingStyles.inset） | 核对时发现 Avalonia、WPF 的 Listbox 键盘聚焦时根本没有焦点环（焦点装饰、焦点框都关了；Avalonia 的说明以为由 ScrollArea 画，其实 ScrollArea 只在自己有焦点时画）；WinForms 本来就画在里面 | 已修：Avalonia 借 ScrollArea 里侧 2 像素的那圈（列表 :focus-visible 时显示），WPF 在根上叠一圈 2 像素、键盘聚焦时显示 |
| CodeBlock 的复制按钮焦点环画在里面 | 我们的复制按钮同 IconButton，焦点环本来就压在按钮里面 | 不涉及 |
| Tabs 的标签（列表可横向滚动）焦点环画在里面 | Avalonia、WPF 的标签焦点环在外面一圈（Margin -4）；WinForms 画在里面 | CI 绿后锁定时改成里面 |
| DataTable 表头排序按钮、Splitter 把手 | 还没做 | 做时照新的 |

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

### 阶段 3 完成（合入 master 9d5af3f3）、阶段 4 进行中（2026-11-06 通知，PR #4）

- 阶段 3 补充：Table 与 DataTable 共用表格配方（我们还没做表格，做时一套配方）；新增颜色 token scrim、scrimStrong、onScrim（**已按 9d5af3f3 重新生成**，三个库都有了；以后做 Upload、Lightbox 的遮罩用它们，不写裸色）。
- 我们的 CI 核对的是上游的 master：上游合入后 `--check` 会失败，要按上游 master 重新生成。上游的工作目录常在别的分支上，生成时用 scratchpad 里检出 master 的副本（`git clone --shared` 本地仓库，node_modules 用目录联接指过去），`VUE_TSX_PRO` 指向它，不碰对方的工作区。
- token-gen 现在会删掉上游已经没有的主题文件（`--check` 报"多余"）：阶段 4 去掉 ocean 时 `Themes/Ocean.g.xaml` 跟着删。

阶段 4 已做的，对我们的影响：

| 上游 | 跟进 |
|---|---|
| 去掉 ocean，加高对比暗色（纯黑底白字、文字 7:1、阴影换成 1px 浅色描边、黄色焦点环 #ffd60a），仍是 16 套 | 合入后重新生成（主题登记表、语言包的 themeOcean 都是生成的）；Gallery、inspect 的主题列表读登记表，不用手改。WPF 的阴影取一层做 DropShadowEffect（DESIGN §6），"阴影换成描边"要看生成结果是不是落到边框上，到时核对 |
| 两套高对比的文字都到 7:1；占位文字改用次要文字色；亮色 successText / warningText / infoText 改中间调 | token 取值，重新生成即可 |
| 跟随系统时，系统要求增强对比度就用高对比的亮、暗两套（builtinSystemThemes） | 桌面的对应：Windows 的对比度主题（WPF `SystemParameters.HighContrast`、WinForms `SystemInformation.HighContrast`、Avalonia 平台设置的对比度偏好）。我们还没做"跟随系统"的主题切换，做 ThemeSwitcher / 应用级主题时一起做 |
| ButtonBase 继承字重 | 我们的 ButtonBase（Avalonia、WPF）主题不设 FontWeight，本来就继承；WinForms 的 ButtonBase 还没做 |
| elevated 卡片加 borderMuted 边框 | Card 还没做，做时照做 |
| 删掉 Figma 设计资源；点击波纹保持默认开启 | 不涉及；`Ripple.Enabled` 默认值不变 |
| 接下来：控件高度 24/28/32/36/40、中文字重（600 只给 H1–H4）、着色页面 + 白卡片、标题阶梯 | 控件高度：先把按钮家族里的 max(2.25em, 24px) 收成一处（三个平台各一处），表定了换成按档位查表；字重、页面底色、标题阶梯多半是 token，重新生成后核对按钮的字重设置 |

### 阶段 4 第二批（2026-11-06 通知，分支 feat/style-overhaul c340d084，未合入 master）

先用 scratchpad 里检出 c340d084 的副本跑了一遍生成器：token-gen 认不出 `sizes.control = round(nearest, 2.25em, 4px)`，上游一合入我们的 CI 就会失败。已经让 token-gen 认识 CSS 的 `round()`：系数照旧（`Rune.Size.Control` = 2.25），另生成取整步长 `Rune.Size.ControlRound` = 4（WinForms `Tokens.Size.ControlRound`）；对上游 master 的输出不变。语言包（主题名的增减）、亮色主题的新底色（上游已经算成具体颜色）都能直接生成，合入后重新生成即可。

| 上游 | 我们 | 跟进 |
|---|---|---|
| 控件高度：字号的 2.25 倍取整到 4px（13 / 14 / 16 / 18px → 28 / 32 / 36 / 40px）；字号档位 xs 用 2 倍（12px → 24px）；仍与 minTarget 24px 取大 | 三个平台各一处写死 max(2.25em, 24px)：Avalonia 按钮主题的 MinHeight 与 IconButton 的 Width（`{rune:Em 2.25, Min=24}`）、WPF 的 `EmSize.MinHeight` / `EmSize.Width` + `MinTarget`、WinForms `ButtonCore.Measure` | 收成每个平台一个"控件高度"：`max(round(系数 × 字号 ÷ 步长) × 步长, minTarget)`，系数取 `Rune.Size.Control`（组件的字号档位是 xs 时用 2），步长取 `Rune.Size.ControlRound`；按 CSS 的 round(nearest) 取整（正好一半时往大取）。Avalonia 给 `{rune:Em}` 加取整参数，WPF 的 EmSize 加步长，WinForms 用一个共用方法 |
| 字重：按钮、组件小标题、分组标签、表头等界面文字 600 → 500；标题 H1–H6、数字、页面级大标题仍 600 | 按钮家族主题的 FontWeight 是 Semibold（WinForms 画成粗体） | 合入后改 Medium（WinForms 画成常规——正是上游要避开的"600 在微软雅黑上落成粗体"）；Strong 仍是 600 |
| H6 默认字号改 sm | Heading 还没做 | 做时照做 |
| 亮色系 7 套主题"着色页面 + 白卡片"（background ← 原 surfaceMuted，surface / surfaceRaised ← 白，surfaceMuted 重算），几套主题的图表色调深保 3:1 | token | 合入后重新生成；Gallery、inspect 的页面底色用 `Rune.Brush.Background`，按钮的 surface 样式用 Surface，截图核对白底按钮在着色页面上的效果 |
| Upload 图片墙的小按钮在深色下对比度不够，改成半透明表面色底 | Upload 还没做 | 做时照做 |
| 字体只用系统字体栈，不带 web 字体 | 我们本来就只用系统字体 | 不涉及 |

### 阶段 4 第三批（2026-11-06 通知，分支 feat/style-overhaul 8a14554a，未合入 master）

对 8a14554a 跑了 token-gen、lang-gen、icons-gen 的 `--check`：都能生成，差异只有已知的阶段 4 改动（加上 `durations.lg` 的说明改了字），没有新的 token 组或写法，不用改生成器。

| 上游 | 我们 | 跟进 |
|---|---|---|
| Field 标签字号 sm → md（中等字重），说明文字仍 sm、弱化色；FormSection 字段行距 md → lg | Field、FormSection 还没做 | 做时照做 |
| 新增 `transitions.pop`：锚定的小浮层（菜单、弹出框、选项列表、日期面板）进场 sm(120ms) + enter 缓动、离场 xs(75ms)，缩放 0.96；对话框仍用 scale（md） | 浮层还没做 | 做 Popover / Menu 时把"小浮层进出场"做成一处共用（时长、缓动取 token），对话框另用 scale |
| 文档站默认用霞鹜文楷等宽（独立包 rune-ui-wuxiawenkai，只有 woff2 网页字体）；组件库仍默认系统字体 | 组件库同样默认系统字体；没有文档站，Gallery 是桌面程序 | 不涉及。woff2 是网页分发格式，桌面上要用得另取 TTF 嵌入 Gallery，要做时再定 |

### 0.11 发版前（2026-11-06 通知，PR #4 等 CI 后合入 master）

| 上游 | 我们 | 跟进 |
|---|---|---|
| 主入口不再导出组件内部的样式表；外观定制只走 `components.Xxx` 的 defaults / styles / variants 与部件的 `propsXxx.sx`，照常导出配方与共用样式件 | WinForms 的 `ButtonRecipe` 已是 internal。Avalonia 的 `Rune.ButtonCore` 主题、WPF 的 `Rune.ButtonCore` / `Rune.Recipe.Button` 样式是资源键，XAML 里藏不住 | 不收（相当于上游照常导出的配方）。在 DESIGN 里写明定制入口：按类型的隐式主题 / 样式、部件、调色板；这几个键是组装用的，不保证跨版本稳定。做第 2 包（组件段只用主题设值）时一起写 |
| Tabs、Steps 换逻辑属性：横排、竖排各给一份指示条位置，免得从右到左时物理属性盖掉逻辑属性 | Tabs、Steps 还没做 | 做时注意：Avalonia、WPF 的 `FlowDirection` 会整体镜像布局和 RenderTransform，指示条照常按 Left/Top 平移即可；WinForms 的 `RightToLeft` 不镜像，指示条位置要自己按方向换算 |
| 状态写法换条件别名推迟：`:is(…)` 的优先级排在 `:hover` 前，已选 / 不可用的项悬停时露出悬停色 | Avalonia 样式没有优先级之分，同一优先级里后声明的赢；WPF 的 Trigger 也是后写的赢 | 同一类问题：做有已选、不可用状态的组件时，这两种状态的样式写在 pointerover / IsMouseOver 之后，并各加一条"不可用 + 悬停"的测试 |
| 文档站示例画布改用页面底色，卡片才看得出层级 | 三个平台的 Gallery 窗口已是 `Background`，示例里的卡片用 Surface | 不涉及 |
| 0.11 的使用方变化在 RELEASE.md，剩余工作在 ROADMAP 阶段 24 | — | 合入 master 后按 RELEASE.md 逐条核对，与第二、三批一起重新生成 token、语言包 |

### 0.11 全量清单对照（2026-11-06 通知；GitHub 上 feat/style-overhaul 已到 da5a4970 chore(release): 0.11.0，master 仍是 9d5af3f3）

状态：**已有**＝C# 版已经是这样；**合入后**＝等上游 master 合入，CI 按 GitHub master 核对生成文件，提前生成会让 CI 失败；**待定**＝已列工作包，等用户确认开工；**做时照做**＝组件还没做；**不涉及**＝网页专有。

| 上游 | 状态 | 我们的对应 |
|---|---|---|
| 一.1 按合并来源分段、@layer；宿主不分层的 CSS 盖过组件库 | 待定（第 3 包） | 桌面的对应是 Provider 合成：后面的普通取值替换前面的条件取值。宿主一侧：Avalonia 应用的 Styles 排在 RuneTheme 后、WPF 应用资源里的隐式样式本来就盖过组件库 |
| 一.2 条件别名（_hover、_disabled…） | 不涉及 | 平台用伪类、Trigger；"已选、不可用写在悬停之后"的先后规则见上一节 |
| 一.3 compound 的 `when` → `match` | 不涉及 | recipe-gen 是对照 button-core 手写的取值表，不读上游配方语法 |
| 一.4 `components: { Xxx: { defaults, styles, variants } }` 一个入口 | 待定（第 2、5 包） | Avalonia、WPF：按类型的隐式主题 / 样式；WinForms 要补一个定制入口 |
| 一.5 公开 API 收口（内部样式表不导出，hotStyles、VarTheme） | 已有 | WinForms 配方是 internal；XAML 资源键在 DESIGN 写明不稳定（随第 2 包） |
| 二 variant 只表示外观（Skeleton shape、图表 type） | 做时照做 | Skeleton、Sparkline、TimelineChart、WaveGrid 还没做 |
| 二 `Tone` → `TextTone`；StatusTone 不变 | 待定（第 1 包） | 三个平台各有 `Tone` 记录结构（Typography.cs），属性名 `Tone` 不变；公开类型改名，不留旧名 |
| 二 不改的（Container/Dialog/Drawer size、NavList compact、Result status） | 做时照做 | 组件还没做 |
| 三 去掉 ocean、新增 high-contrast-dark | 合入后 | 重新生成 token 与语言包；token-gen 会删掉 Ocean 的主题文件 |
| 三 跟随系统时按对比度偏好选高对比 | 做时照做 | 做"跟随系统"的主题切换时接平台的高对比设置 |
| 三 scrim、scrimStrong、onScrim | 已有 | 已按 9d5af3f3 生成 |
| 三 去掉 Figma 资源 | 不涉及 | — |
| 四 S2 着色页面 + 白卡片、S1 状态文字中间调、S6 占位文字 11 级 | 合入后 | 都是 token，重新生成；截图核对 Gallery 的白底按钮与 success/warning/info 文字 |
| 四 S3 控件高度 24/28/32/36/40 | 合入后 | 见"阶段 4 第二批"：三个平台各收成一个控件高度计算，取 `Rune.Size.ControlRound` |
| 四 S4 界面文字 600 → 500 | 待定 | 按钮家族主题的 Semibold 改 Medium（不依赖 token，确认开工就做）；Heading 的 H5/H6 还没做 |
| 四 C1 ButtonBase 继承字重 | 已有 | ButtonBase 主题不设 FontWeight；WinForms 没有 ButtonBase |
| 四 C2 elevated 卡片描边、C3 Field / FormSection、transitions.pop | 做时照做 | 见上两节 |
| 四 不改的（阴影分档、display 档、info 色相、波纹默认开） | 已有 | — |
| 五 focusRingStyles.inset / virtual、disclosureStyles.flip / turn、anchoredPanelSx | 做时照做 | 焦点环已是 ButtonBase 的部件，inset / virtual 在第一个要用的组件（列表项、输入框）里加 |
| 五 statusToneCompound（Badge、Alert 色调取共用色表） | 待定（第 4 包） | Badge 的颜色改从调色板表取 |
| 五 Table / DataTable 共用配方、OtpField 用输入框配方 | 做时照做 | — |
| 五 按钮的内容、前后图标、加载指示是部件 | 已有 | `ButtonParts` 的 Content / Icon / IconEnd / Spinner（三个平台） |
| 五 从右到左：展开箭头、Tabs / Steps 逻辑属性；Upload 小按钮底色 | 做时照做 | 见上一节 |
| 六 文档站画布用页面底色、字体切换 | 已有 / 不涉及 | Gallery 已是页面底色；字体切换是网页的 |
| 七 下个版本（部件补齐、状态别名、裸值换 token、截图进 CI） | 跟上游节奏 | 我们的组件都按部件做；裸值换 token 做第 2 包时一起查 |

### 进度

- 2026-11-06：收到计划草案，完成上面的评估。同日上游获批，方向见"已定的方向"。代码未动，等上游各阶段的通知；还没做的组件直接按已定的词表与规则做（新名字、控件高度收成一处、浮层阴影按新规则）。
- 2026-11-06：收到阶段 1 完成的通知，对照三个平台写了上面"阶段 1"的差别与实现计划。按用户的安排（这阶段先做性能），代码等阶段 2 的通知到了再一起动。
- 2026-11-06：收到阶段 2 完成、阶段 3 进行中的通知，列出上面 5 个工作包。其中第 3 包是跨模块的重构（Provider 合成），开工前先请用户确认。
- 2026-11-06：收到阶段 3 合入、阶段 4 进行中的通知。按上游 master 9d5af3f3 重新生成了 token（新增 scrim 三个）；token-gen 能删掉上游没有了的主题文件。Icon 的 `draw`（描线动画）parity 报缺，是早就记下的暂缓项。
- 2026-11-06：收到阶段 4 第二批的通知（分支上，未合入）。提前对分支跑了生成器，让 token-gen 认识 `round()`，避免上游合入后 CI 失败；控件高度、按钮字重的改法记在上面，等合入后与其他对齐工作一起做。
- 2026-11-06：收到阶段 4 第三批的通知（分支上，未合入）。生成器对 8a14554a 不用改；Field、FormSection、小浮层过渡记在上面，组件还没做。
- 2026-11-06：收到 0.11 发版前的通知。样式类本来就没当公开 API 暴露（WinForms 是 internal，XAML 的资源键藏不住，改为在 DESIGN 里写明不稳定），其余记在上面，等合入 master 后一起跟进。
- 2026-11-06：收到 0.11 全量清单，逐项对照写在上面。代码等两件事：上游 master 合入（token、语言包、控件高度），用户确认开工（第 1–5 包、按钮字重）。
- 2026-11-07：收到 0.11.1 发版的通知，锁定从 45c2ad90 跟到 875797ae（0.12.0-dev）。组件库的变化：勾选控件、ColorField、OtpField 的原生 input 继承字体（三个库的 Switch 本来就按文字字号算；Avalonia、WPF 用 `styles.mjs` 对 875797ae 核对过轨道、滑块的高度与颜色。为此 inspect 把 Switch 报成上游的 `choice` 部件——轨道是 input、滑块是 indicator——比对工具把"透明或与底色同色的描边"与"不画描边"算作一样），Calendar 在月份、年份视图里指针点一格后回到日期视图时保留原来的日（`calendar-state.focus`）。token、语言包、图标、配方表都没变。Calendar 还没做，做时按 875797ae 的行为：聚焦一格只换那一格代表的年 / 月，日取原来的（那个月没有就取最后一天）。
