using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>一个示例：Gallery 里的一节，inspect 按 <see cref="Key"/> 找到它。</summary>
/// <param name="Component">组件名，同 vue-tsx-pro。</param>
/// <param name="Id">示例名，同 vue-tsx-pro 文档站上这一节的 id（basic、sizes……），ts-styles 与 inspect 按它配对。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">说明。</param>
/// <param name="Create">新建示例的内容。</param>
/// <param name="Framed">整页示例（AppShell）：效果区不留内边距，内容贴满（TS 的 frame，示例在 iframe 里）。</param>
public sealed record Example(string Component, string Id, string Title, string Description, Func<Control> Create, bool Framed = false)
{
    /// <summary>"组件/示例名"，如 <c>Icon/basic</c>。</summary>
    public string Key => $"{Component}/{Id}";
}

/// <summary>全部示例：组件按 vue-tsx-pro 文档站的分类，示例按页面上的先后。</summary>
public static class ExampleCatalog
{
    /// <summary>全部示例。</summary>
    public static IReadOnlyList<Example> All { get; } = [.. IconExamples.All, .. TextExamples.All, .. ListExamples.All, .. SpinnerExamples.All, .. BadgeExamples.All, .. ButtonExamples.All, .. CardExamples.All, .. ItemListExamples.All, .. AvatarExamples.All, .. ImageExamples.All, .. CarouselExamples.All, .. TreeExamples.All, .. JsonViewerExamples.All, .. VirtualListExamples.All, .. SortableExamples.All, .. DescriptionsExamples.All, .. FieldCompareExamples.All, .. TimelineExamples.All, .. EllipsisExamples.All, .. ChecklistExamples.All, .. DateTimeExamples.All, .. DateFieldExamples.All, .. CalendarExamples.All, .. RangeCalendarExamples.All, .. CalendarBoardExamples.All, .. DatePickerExamples.All, .. DateRangePickerExamples.All, .. TimePickerExamples.All, .. StatExamples.All, .. SparklineExamples.All, .. FigureExamples.All, .. WatermarkExamples.All, .. QRCodeExamples.All, .. MarqueeExamples.All, .. AccordionExamples.All, .. AlertExamples.All, .. ErrorBoundaryExamples.All, .. ToastExamples.All, .. EmptyExamples.All, .. ProgressExamples.All, .. SkeletonExamples.All, .. CheckboxExamples.All, .. ToggleGroupExamples.All, .. TextFieldExamples.All, .. NumberFieldExamples.All, .. OtpFieldExamples.All, .. TagsInputExamples.All, .. ComboboxExamples.All, .. ListboxExamples.All, .. ConditionBuilderExamples.All, .. TransferExamples.All, .. TreeSelectExamples.All, .. CascaderExamples.All, .. PeoplePickerExamples.All, .. ColorExamples.All, .. MentionsExamples.All, .. UploadExamples.All, .. CommentThreadExamples.All, .. SelectExamples.All, .. RatingExamples.All, .. SliderExamples.All, .. ImageCropperExamples.All, .. SignaturePadExamples.All, .. ControlGroupExamples.All, .. CodeBlockExamples.All, .. FlexExamples.All, .. ProseExamples.All, .. SplitterExamples.All, .. ScrollAreaExamples.All, .. FloatButtonExamples.All, .. AffixExamples.All, .. AsideLayoutExamples.All, .. TabsExamples.All, .. TableExamples.All, .. PivotTableExamples.All, .. SelectionBarExamples.All, .. NavListExamples.All, .. BreadcrumbsExamples.All, .. PaginationExamples.All, .. ToolbarExamples.All, .. PopoverExamples.All, .. PopconfirmExamples.All, .. MenuExamples.All, .. DialogExamples.All, .. DrawerExamples.All, .. CommandPaletteExamples.All, .. TourExamples.All, .. AppShellExamples.All, .. AuthLayoutExamples.All, .. ThemeSwitcherExamples.All, .. NotificationCenterExamples.All, .. SlideCaptchaExamples.All, .. StepsFormExamples.All, .. PageHeaderExamples.All, .. ActionBarExamples.All, .. DeadlineBadgeExamples.All, .. KanbanBoardExamples.All, .. FormExamples.All, .. FormDialogExamples.All, .. ProviderExamples.All, .. DocsKitExamples.All];

    /// <summary>按"组件/示例名"找（不分大小写）；没有时为 null。</summary>
    /// <param name="key">如 <c>Icon/basic</c>。</param>
    public static Example? Find(string key) => All.FirstOrDefault(example => string.Equals(example.Key, key, StringComparison.OrdinalIgnoreCase));
}
