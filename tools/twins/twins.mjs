#!/usr/bin/env node
// 三个库各有一份、除命名空间外逐字相同的源文件（纯计算或纯数据，不用平台类型：FlexLayout、GridLayout、StatusMap、AvatarInitials、ListboxOption、ListState、FloatingPlacement、ScrollAreaVariants、CodeHighlighting、TabsVariants、TableVariants、ItemListVariants、Workdays、MenuVariants、Selection、ToggleGroupVariants、ControlVariants、TextFieldVariants、TextFormat、DesktopMessages、SelectSearch、RemoteOptions、AlertVariants、Hotkey、IconStrokes、ResultStatus、DescriptionsVariants、NumericFormat、SparklineGeometry、ProportionBarVariants、GaugeGeometry、StepsVariants、PaginationItems、NumberRules、AttachedVariants、ProgressBands、FigureVariants、WatermarkTiles、LoadingBarController、MarqueeVariants、OpenChangeReason、HoverIntent、NativeSelectGroups、RatingScale、SliderScale、QrEncoder、SplitterScale、TagsText、DrawerVariants、PanZoom、ImageVariants、ImageCropGeometry、ToasterController、SignatureStrokes、TreeData、JsonViewerNodes、VirtualScroll、InfiniteList、TableQuery、Sheet、TableCsv、SchemaModel、SortableModel、FloatingGeometry、FormController、DateSegments、DateFieldRange、CalendarState、CalendarText、QuarterState、RangePreset、TimeSlots、FileAccept、FieldComparison、TableColumns、Pivot、ChecklistModel、TimeText、NowClock、VideoPage（在可选包 Rune.*.Web 里），示例项目的 ExampleSources、DocSite）。三个库不共享代码（ROADMAP 决定 2），
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
  ['avalonia/src/Rune.Avalonia/Utils/Data/Area/Area.cs', 'wpf/src/Rune.Wpf/Utils/Data/Area/Area.cs', 'winforms/src/Rune.WinForms/Utils/Data/Area/Area.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/ColorMath.cs', 'wpf/src/Rune.Wpf/Utils/Data/ColorMath.cs', 'winforms/src/Rune.WinForms/Utils/Data/ColorMath.cs'],
  ['avalonia/src/Rune.Avalonia/Input/PeoplePicker/People.cs', 'wpf/src/Rune.Wpf/Input/PeoplePicker/People.cs', 'winforms/src/Rune.WinForms/Input/PeoplePicker/People.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Mentions/MentionParsing.cs', 'wpf/src/Rune.Wpf/Input/Mentions/MentionParsing.cs', 'winforms/src/Rune.WinForms/Input/Mentions/MentionParsing.cs'],
  ['avalonia/src/Rune.Avalonia/Input/ConditionBuilder/Condition.cs', 'wpf/src/Rune.Wpf/Input/ConditionBuilder/Condition.cs', 'winforms/src/Rune.WinForms/Input/ConditionBuilder/Condition.cs'],
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
  ['avalonia/src/Rune.Avalonia/Display/Figure/FigureVariants.cs', 'wpf/src/Rune.Wpf/Display/Figure/FigureVariants.cs', 'winforms/src/Rune.WinForms/Display/Figure/FigureVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Feedback/Progress/ProgressBands.cs', 'wpf/src/Rune.Wpf/Feedback/Progress/ProgressBands.cs', 'winforms/src/Rune.WinForms/Feedback/Progress/ProgressBands.cs'],
  ['avalonia/src/Rune.Avalonia/Feedback/LoadingBar/LoadingBarController.cs', 'wpf/src/Rune.Wpf/Feedback/LoadingBar/LoadingBarController.cs', 'winforms/src/Rune.WinForms/Feedback/LoadingBar/LoadingBarController.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Floating/HoverIntent.cs', 'wpf/src/Rune.Wpf/Utils/Floating/HoverIntent.cs', 'winforms/src/Rune.WinForms/Utils/Floating/HoverIntent.cs'],
  ['avalonia/src/Rune.Avalonia/Overlay/OpenChangeReason.cs', 'wpf/src/Rune.Wpf/Overlay/OpenChangeReason.cs', 'winforms/src/Rune.WinForms/Overlay/OpenChangeReason.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Checklist/ChecklistModel.cs', 'wpf/src/Rune.Wpf/Display/Checklist/ChecklistModel.cs', 'winforms/src/Rune.WinForms/Display/Checklist/ChecklistModel.cs'],
  ['avalonia/src/Rune.Avalonia/Date/TimeText.cs', 'wpf/src/Rune.Wpf/Date/TimeText.cs', 'winforms/src/Rune.WinForms/Date/TimeText.cs'],
  ['avalonia/src/Rune.Avalonia/Date/NowClock.cs', 'wpf/src/Rune.Wpf/Date/NowClock.cs', 'winforms/src/Rune.WinForms/Date/NowClock.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Slider/SliderScale.cs', 'wpf/src/Rune.Wpf/Input/Slider/SliderScale.cs', 'winforms/src/Rune.WinForms/Input/Slider/SliderScale.cs'],
  ['avalonia/src/Rune.Avalonia/Display/QRCode/QrEncoder.cs', 'wpf/src/Rune.Wpf/Display/QRCode/QrEncoder.cs', 'winforms/src/Rune.WinForms/Display/QRCode/QrEncoder.cs'],
  ['avalonia/src/Rune.Avalonia/Layout/Splitter/SplitterScale.cs', 'wpf/src/Rune.Wpf/Layout/Splitter/SplitterScale.cs', 'winforms/src/Rune.WinForms/Layout/Splitter/SplitterScale.cs'],
  ['avalonia/src/Rune.Avalonia/Input/TagsInput/TagsText.cs', 'wpf/src/Rune.Wpf/Input/TagsInput/TagsText.cs', 'winforms/src/Rune.WinForms/Input/TagsInput/TagsText.cs'],
  ['avalonia/src/Rune.Avalonia/Overlay/Drawer/DrawerVariants.cs', 'wpf/src/Rune.Wpf/Overlay/Drawer/DrawerVariants.cs', 'winforms/src/Rune.WinForms/Overlay/Drawer/DrawerVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/PanZoom/PanZoom.cs', 'wpf/src/Rune.Wpf/Utils/PanZoom/PanZoom.cs', 'winforms/src/Rune.WinForms/Utils/PanZoom/PanZoom.cs'],
  ['avalonia/src/Rune.Avalonia/Input/ImageCropper/ImageCropGeometry.cs', 'wpf/src/Rune.Wpf/Input/ImageCropper/ImageCropGeometry.cs', 'winforms/src/Rune.WinForms/Input/ImageCropper/ImageCropGeometry.cs'],
  ['avalonia/src/Rune.Avalonia/Feedback/Toast/ToasterController.cs', 'wpf/src/Rune.Wpf/Feedback/Toast/ToasterController.cs', 'winforms/src/Rune.WinForms/Feedback/Toast/ToasterController.cs'],
  ['avalonia/src/Rune.Avalonia/Input/SignaturePad/SignatureStrokes.cs', 'wpf/src/Rune.Wpf/Input/SignaturePad/SignatureStrokes.cs', 'winforms/src/Rune.WinForms/Input/SignaturePad/SignatureStrokes.cs'],
  ['avalonia/src/Rune.Avalonia/Display/JsonViewer/JsonViewerNodes.cs', 'wpf/src/Rune.Wpf/Display/JsonViewer/JsonViewerNodes.cs', 'winforms/src/Rune.WinForms/Display/JsonViewer/JsonViewerNodes.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Virtual/VirtualScroll.cs', 'wpf/src/Rune.Wpf/Utils/Virtual/VirtualScroll.cs', 'winforms/src/Rune.WinForms/Utils/Virtual/VirtualScroll.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Request/InfiniteList.cs', 'wpf/src/Rune.Wpf/Utils/Request/InfiniteList.cs', 'winforms/src/Rune.WinForms/Utils/Request/InfiniteList.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Request/TableQuery.cs', 'wpf/src/Rune.Wpf/Utils/Request/TableQuery.cs', 'winforms/src/Rune.WinForms/Utils/Request/TableQuery.cs'],
  ['avalonia/src/Rune.Avalonia/Table/Sheet/Sheet.cs', 'wpf/src/Rune.Wpf/Table/Sheet/Sheet.cs', 'winforms/src/Rune.WinForms/Table/Sheet/Sheet.cs'],
  ['avalonia/src/Rune.Avalonia/Table/Sheet/TableCsv.cs', 'wpf/src/Rune.Wpf/Table/Sheet/TableCsv.cs', 'winforms/src/Rune.WinForms/Table/Sheet/TableCsv.cs'],
  ['avalonia/src/Rune.Avalonia/Form/Schema/SchemaModel.cs', 'wpf/src/Rune.Wpf/Form/Schema/SchemaModel.cs', 'winforms/src/Rune.WinForms/Form/Schema/SchemaModel.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Sortable/SortableModel.cs', 'wpf/src/Rune.Wpf/Utils/Sortable/SortableModel.cs', 'winforms/src/Rune.WinForms/Utils/Sortable/SortableModel.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Calendar/CalendarState.cs', 'wpf/src/Rune.Wpf/Date/Calendar/CalendarState.cs', 'winforms/src/Rune.WinForms/Date/Calendar/CalendarState.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Calendar/CalendarText.cs', 'wpf/src/Rune.Wpf/Date/Calendar/CalendarText.cs', 'winforms/src/Rune.WinForms/Date/Calendar/CalendarText.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Calendar/QuarterState.cs', 'wpf/src/Rune.Wpf/Date/Calendar/QuarterState.cs', 'winforms/src/Rune.WinForms/Date/Calendar/QuarterState.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Pickers/RangePreset.cs', 'wpf/src/Rune.Wpf/Date/Pickers/RangePreset.cs', 'winforms/src/Rune.WinForms/Date/Pickers/RangePreset.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Pickers/TimeSlots.cs', 'wpf/src/Rune.Wpf/Date/Pickers/TimeSlots.cs', 'winforms/src/Rune.WinForms/Date/Pickers/TimeSlots.cs'],
  ['avalonia/src/Rune.Avalonia/Input/FileDrop/FileAccept.cs', 'wpf/src/Rune.Wpf/Input/FileDrop/FileAccept.cs', 'winforms/src/Rune.WinForms/Input/FileDrop/FileAccept.cs'],
  ['avalonia/src/Rune.Avalonia/Table/Columns/TableColumns.cs', 'wpf/src/Rune.Wpf/Table/Columns/TableColumns.cs', 'winforms/src/Rune.WinForms/Table/Columns/TableColumns.cs'],
  ['avalonia/src/Rune.Avalonia/Table/Columns/Pivot.cs', 'wpf/src/Rune.Wpf/Table/Columns/Pivot.cs', 'winforms/src/Rune.WinForms/Table/Columns/Pivot.cs'],
  ['avalonia/src/Rune.Avalonia/Date/DateField/DateFieldRange.cs', 'wpf/src/Rune.Wpf/Date/DateField/DateFieldRange.cs', 'winforms/src/Rune.WinForms/Date/DateField/DateFieldRange.cs'],
  ['avalonia/src/Rune.Avalonia/Date/Segments/DateSegments.cs', 'wpf/src/Rune.Wpf/Date/Segments/DateSegments.cs', 'winforms/src/Rune.WinForms/Date/Segments/DateSegments.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Floating/FloatingGeometry.cs', 'wpf/src/Rune.Wpf/Utils/Floating/FloatingGeometry.cs', 'winforms/src/Rune.WinForms/Utils/Floating/FloatingGeometry.cs'],
  ['avalonia/src/Rune.Avalonia/Form/Controller/FormController.cs', 'wpf/src/Rune.Wpf/Form/Controller/FormController.cs', 'winforms/src/Rune.WinForms/Form/Controller/FormController.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Data/TreeData.cs', 'wpf/src/Rune.Wpf/Utils/Data/TreeData.cs', 'winforms/src/Rune.WinForms/Utils/Data/TreeData.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Image/ImageVariants.cs', 'wpf/src/Rune.Wpf/Display/Image/ImageVariants.cs', 'winforms/src/Rune.WinForms/Display/Image/ImageVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Display/FieldCompare/FieldComparison.cs', 'wpf/src/Rune.Wpf/Display/FieldCompare/FieldComparison.cs', 'winforms/src/Rune.WinForms/Display/FieldCompare/FieldComparison.cs'],
  ['avalonia/src/Rune.Avalonia/Input/Rating/RatingScale.cs', 'wpf/src/Rune.Wpf/Input/Rating/RatingScale.cs', 'winforms/src/Rune.WinForms/Input/Rating/RatingScale.cs'],
  ['avalonia/src/Rune.Avalonia/Input/NativeSelect/NativeSelectGroups.cs', 'wpf/src/Rune.Wpf/Input/NativeSelect/NativeSelectGroups.cs', 'winforms/src/Rune.WinForms/Input/NativeSelect/NativeSelectGroups.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Marquee/MarqueeVariants.cs', 'wpf/src/Rune.Wpf/Display/Marquee/MarqueeVariants.cs', 'winforms/src/Rune.WinForms/Display/Marquee/MarqueeVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Display/Watermark/WatermarkTiles.cs', 'wpf/src/Rune.Wpf/Display/Watermark/WatermarkTiles.cs', 'winforms/src/Rune.WinForms/Display/Watermark/WatermarkTiles.cs'],
  ['avalonia/src/Rune.Avalonia.Web/VideoPage.cs', 'wpf/src/Rune.Wpf.Web/VideoPage.cs', 'winforms/src/Rune.WinForms.Web/VideoPage.cs'],
  ['avalonia/src/Rune.Avalonia/Utils/Style/AttachedVariants.cs', 'wpf/src/Rune.Wpf/Utils/Style/AttachedVariants.cs', 'winforms/src/Rune.WinForms/Utils/Style/AttachedVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Navigation/Steps/StepsVariants.cs', 'wpf/src/Rune.Wpf/Navigation/Steps/StepsVariants.cs', 'winforms/src/Rune.WinForms/Navigation/Steps/StepsVariants.cs'],
  ['avalonia/src/Rune.Avalonia/Navigation/Pagination/PaginationItems.cs', 'wpf/src/Rune.Wpf/Navigation/Pagination/PaginationItems.cs', 'winforms/src/Rune.WinForms/Navigation/Pagination/PaginationItems.cs'],
  ['avalonia/src/Rune.Avalonia/Input/NumberField/NumberRules.cs', 'wpf/src/Rune.Wpf/Input/NumberField/NumberRules.cs', 'winforms/src/Rune.WinForms/Input/NumberField/NumberRules.cs'],
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
