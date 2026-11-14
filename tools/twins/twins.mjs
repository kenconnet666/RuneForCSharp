#!/usr/bin/env node
// 三个库各有一份、除命名空间外逐字相同的源文件（纯计算或纯数据，不用平台类型：FlexLayout、GridLayout、StatusMap、AvatarInitials、ListboxOption、ListState、FloatingPlacement、ScrollAreaVariants、CodeHighlighting、TabsVariants、TableVariants、ItemListVariants、Workdays、MenuVariants、Selection、ToggleGroupVariants、ControlVariants、TextFieldVariants、TextFormat、DesktopMessages、SelectSearch、RemoteOptions、AlertVariants、Hotkey、IconStrokes、ResultStatus、DescriptionsVariants、NumericFormat、SparklineGeometry、ProportionBarVariants、GaugeGeometry，示例项目的 ExampleSources、DocSite）。三个库不共享代码（ROADMAP 决定 2），
// 一致性靠这里检查：改了一份，另外两份照抄。用法：node tools/twins/twins.mjs；有不同时退出码为 1
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../lib/vue.mjs';

/** 一组孪生文件：三个库里的路径 */
const TWINS = [
  ['avalonia/src/Rune.Avalonia/Layout/Flex/FlexLayout.cs', 'wpf/src/Rune.Wpf/Layout/Flex/FlexLayout.cs', 'winforms/src/Rune.WinForms/Layout/Flex/FlexLayout.cs'],
  ['avalonia/src/Rune.Avalonia/Layout/Grid/GridLayout.cs', 'wpf/src/Rune.Wpf/Layout/Grid/GridLayout.cs', 'winforms/src/Rune.WinForms/Layout/Grid/GridLayout.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/StatusMap.cs', 'wpf/src/Rune.Wpf/Utils/Data/StatusMap.cs', 'winforms/src/Rune.WinForms/Utils/Data/StatusMap.cs'],
  ['avalonia/src/Rune.Avalonia/Input/ListState/ListboxOption.cs', 'wpf/src/Rune.Wpf/Input/ListState/ListboxOption.cs', 'winforms/src/Rune.WinForms/Input/ListState/ListboxOption.cs'],
  ['avalonia/src/Rune.Avalonia/Input/ListState/ListState.cs', 'wpf/src/Rune.Wpf/Input/ListState/ListState.cs', 'winforms/src/Rune.WinForms/Input/ListState/ListState.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Select/SelectSearch.cs', 'wpf/src/Rune.Wpf/Input/Select/SelectSearch.cs', 'winforms/src/Rune.WinForms/Input/Select/SelectSearch.cs'],
  ['avalonia/src/Rune.Avalonia/Feedback/Alert/AlertVariants.cs', 'wpf/src/Rune.Wpf/Feedback/Alert/AlertVariants.cs', 'winforms/src/Rune.WinForms/Feedback/Alert/AlertVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Select/RemoteOptions.cs', 'wpf/src/Rune.Wpf/Input/Select/RemoteOptions.cs', 'winforms/src/Rune.WinForms/Input/Select/RemoteOptions.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Hotkey/Hotkey.cs', 'wpf/src/Rune.Wpf/Utils/Hotkey/Hotkey.cs', 'winforms/src/Rune.WinForms/Utils/Hotkey/Hotkey.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Avatar/AvatarInitials.cs', 'wpf/src/Rune.Wpf/Display/Avatar/AvatarInitials.cs', 'winforms/src/Rune.WinForms/Display/Avatar/AvatarInitials.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Floating/FloatingPlacement.cs', 'wpf/src/Rune.Wpf/Utils/Floating/FloatingPlacement.cs', 'winforms/src/Rune.WinForms/Utils/Floating/FloatingPlacement.cs'],
  ['avalonia/src/Rune.Avalonia/Layout/ScrollArea/ScrollAreaVariants.cs', 'wpf/src/Rune.Wpf/Layout/ScrollArea/ScrollAreaVariants.cs', 'winforms/src/Rune.WinForms/Layout/ScrollArea/ScrollAreaVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Base/CodeBlock/CodeHighlighting.cs', 'wpf/src/Rune.Wpf/Base/CodeBlock/CodeHighlighting.cs', 'winforms/src/Rune.WinForms/Base/CodeBlock/CodeHighlighting.cs'],
  ['avalonia/src/Rune.Avalonia/Navigation/Tabs/TabsVariants.cs', 'wpf/src/Rune.Wpf/Navigation/Tabs/TabsVariants.cs', 'winforms/src/Rune.WinForms/Navigation/Tabs/TabsVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Table/TableVariants.cs', 'wpf/src/Rune.Wpf/Table/TableVariants.cs', 'winforms/src/Rune.WinForms/Table/TableVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Display/ItemList/ItemListVariants.cs', 'wpf/src/Rune.Wpf/Display/ItemList/ItemListVariants.cs', 'winforms/src/Rune.WinForms/Display/ItemList/ItemListVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/Workdays.cs', 'wpf/src/Rune.Wpf/Utils/Data/Workdays.cs', 'winforms/src/Rune.WinForms/Utils/Data/Workdays.cs'],
  ['avalonia/src/Rune.Avalonia/Overlay/Menu/MenuVariants.cs', 'wpf/src/Rune.Wpf/Overlay/Menu/MenuVariants.cs', 'winforms/src/Rune.WinForms/Overlay/Menu/MenuVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/Selection.cs', 'wpf/src/Rune.Wpf/Utils/Data/Selection.cs', 'winforms/src/Rune.WinForms/Utils/Data/Selection.cs'],
  ['avalonia/src/Rune.Avalonia/Input/ToggleGroup/ToggleGroupVariants.cs', 'wpf/src/Rune.Wpf/Input/ToggleGroup/ToggleGroupVariants.cs', 'winforms/src/Rune.WinForms/Input/ToggleGroup/ToggleGroupVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Control/ControlVariants.cs', 'wpf/src/Rune.Wpf/Input/Control/ControlVariants.cs', 'winforms/src/Rune.WinForms/Input/Control/ControlVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Input/TextField/TextFieldVariants.cs', 'wpf/src/Rune.Wpf/Input/TextField/TextFieldVariants.cs', 'winforms/src/Rune.WinForms/Input/TextField/TextFieldVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Config/DesktopMessages.cs', 'wpf/src/Rune.Wpf/Config/DesktopMessages.cs', 'winforms/src/Rune.WinForms/Config/DesktopMessages.cs'],
  ['avalonia/src/Rune.Avalonia/Input/TextField/TextFormat.cs', 'wpf/src/Rune.Wpf/Input/TextField/TextFormat.cs', 'winforms/src/Rune.WinForms/Input/TextField/TextFormat.cs'],
  ['avalonia/src/Rune.Avalonia/Base/Icon/IconStrokes.cs', 'wpf/src/Rune.Wpf/Base/Icon/IconStrokes.cs', 'winforms/src/Rune.WinForms/Base/Icon/IconStrokes.cs'],
  ['avalonia/src/Rune.Avalonia/Feedback/Result/ResultStatus.cs', 'wpf/src/Rune.Wpf/Feedback/Result/ResultStatus.cs', 'winforms/src/Rune.WinForms/Feedback/Result/ResultStatus.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Descriptions/DescriptionsVariants.cs', 'wpf/src/Rune.Wpf/Display/Descriptions/DescriptionsVariants.cs', 'winforms/src/Rune.WinForms/Display/Descriptions/DescriptionsVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/NumericFormat.cs', 'wpf/src/Rune.Wpf/Utils/Data/NumericFormat.cs', 'winforms/src/Rune.WinForms/Utils/Data/NumericFormat.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Sparkline/SparklineGeometry.cs', 'wpf/src/Rune.Wpf/Display/Sparkline/SparklineGeometry.cs', 'winforms/src/Rune.WinForms/Display/Sparkline/SparklineGeometry.cs'],
  ['avalonia/src/Rune.Avalonia/Display/ProportionBar/ProportionBarVariants.cs', 'wpf/src/Rune.Wpf/Display/ProportionBar/ProportionBarVariants.cs', 'winforms/src/Rune.WinForms/Display/ProportionBar/ProportionBarVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Gauge/GaugeGeometry.cs', 'wpf/src/Rune.Wpf/Display/Gauge/GaugeGeometry.cs', 'winforms/src/Rune.WinForms/Display/Gauge/GaugeGeometry.cs'],
  ['avalonia/apps/Rune.Avalonia.Examples/ExampleSources.cs', 'wpf/apps/Rune.Wpf.Examples/ExampleSources.cs', 'winforms/apps/Rune.WinForms.Examples/ExampleSources.cs'],
  ['avalonia/apps/Rune.Avalonia.Examples/Site/DocSite.cs', 'wpf/apps/Rune.Wpf.Examples/Site/DocSite.cs', 'winforms/apps/Rune.WinForms.Examples/Site/DocSite.cs'],
];

/** 去掉命名空间那一行，统一换行符 */
const normalize = (path) =>
  readFileSync(join(repoRoot, path), 'utf8')
    .replace(/\r\n/g, '\n')
    .replace(/^namespace [\w.]+;$/m, 'namespace ?;');

let failed = false;
for (const [first, ...others] of TWINS) {
  const expected = normalize(first);
  for (const other of others) {
    if (normalize(other) === expected) continue;
    failed = true;
    console.error(`${other} 和 ${first} 不一样（除命名空间外应当逐字相同）`);
  }
}
if (!failed) console.log(`孪生文件一致（${TWINS.length} 组）`);
process.exitCode = failed ? 1 : 0;
