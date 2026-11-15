using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>ErrorBoundary 接住了错误（<see cref="ErrorBoundary.ErrorCaught"/>）。</summary>
public sealed class ErrorCaughtEventArgs(RoutedEvent routedEvent, Exception error, string where) : RoutedEventArgs(routedEvent)
{
    /// <summary>错误。</summary>
    public Exception Error { get; } = error;

    /// <summary>出错的位置：create（建内容时）、layout（排版时）、report（代码交过来的，<see cref="ErrorBoundary.Show"/>）。</summary>
    public string Where { get; } = where;
}

/// <summary>
/// 错误边界（vue-tsx-pro 的 ErrorBoundary）：里面出错时只把这一块换成出错的提示，页面的其余部分照常；重试时里面的内容整个重新建（出错时的状态不可信）。
/// <code>
/// &lt;rune:ErrorBoundary ErrorCaught="OnError" ResetKey="{Binding ReportId}"&gt;
///   &lt;DataTemplate&gt;&lt;local:SalesChart /&gt;&lt;/DataTemplate&gt;
/// &lt;/rune:ErrorBoundary&gt;
/// </code>
/// 内容是模板（<see cref="ContentTemplate"/>）：重试、<see cref="ResetKey"/> 变了时重新建。接得住的是建内容时与排版（Measure、Arrange）时抛的异常；
/// 事件处理、异步里的错误不经过这里（平台的事件不经过组件，同上游接不住定时器、Promise 里的错误），用 <see cref="Show"/> 交给最近的边界（同 react-error-boundary 的 showBoundary）。
/// 接住的错误发 <see cref="ErrorCaught"/>（上报日志），不再往上传。出错时显示 <see cref="Fallback"/>，不写时是 danger 的 Result：标题、错误信息与"重试"按钮，读屏立即念出来。
/// </summary>
public class ErrorBoundary : Control
{
    /// <summary>里面的内容（每次重建时调用）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty = AvaloniaProperty.Register<ErrorBoundary, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>出错时显示什么：拿到错误与重试的方法，返回文字或控件；不写时是 Result。</summary>
    public static readonly StyledProperty<Func<Exception, Action, object?>?> FallbackProperty =
        AvaloniaProperty.Register<ErrorBoundary, Func<Exception, Action, object?>?>(nameof(Fallback));

    /// <summary>这个值变了就清掉错误、重新建里面的内容：换了路由参数、换了数据源时自动恢复（上游的 resetKeys）。</summary>
    public static readonly StyledProperty<object?> ResetKeyProperty = AvaloniaProperty.Register<ErrorBoundary, object?>(nameof(ResetKey));

    /// <summary>接住了错误。</summary>
    public static readonly RoutedEvent<ErrorCaughtEventArgs> ErrorCaughtEvent = RoutedEvent.Register<ErrorBoundary, ErrorCaughtEventArgs>(nameof(ErrorCaught), RoutingStrategies.Direct);

    readonly ContentPresenter presenter = new();

    /// <summary>新建：内容在 ContentPresenter 里。</summary>
    public ErrorBoundary()
    {
        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
    }

    /// <summary>里面的内容。</summary>
    [Content]
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    /// <summary>出错时显示什么。</summary>
    public Func<Exception, Action, object?>? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>变了就重新建。</summary>
    public object? ResetKey { get => GetValue(ResetKeyProperty); set => SetValue(ResetKeyProperty, value); }

    /// <summary>接住的错误；没有时 null。</summary>
    public Exception? Error { get; private set; }

    /// <summary>接住了错误。</summary>
    public event EventHandler<ErrorCaughtEventArgs>? ErrorCaught
    {
        add => AddHandler(ErrorCaughtEvent, value);
        remove => RemoveHandler(ErrorCaughtEvent, value);
    }

    /// <summary>
    /// 把错误交给控件外面最近的边界（事件处理、异步里接到的错误）：那一块换成出错提示。不在边界里时返回 false，由调用方自己处理。
    /// <code>try { await LoadAsync(); } catch (Exception error) when (ErrorBoundary.Show(this, error)) { }</code>
    /// </summary>
    /// <param name="control">出错的控件。</param>
    /// <param name="error">错误。</param>
    public static bool Show(Visual control, Exception error)
    {
        if (control.FindAncestorOfType<ErrorBoundary>(includeSelf: true) is not { } boundary) return false;
        boundary.Catch(error, "report");
        return true;
    }

    /// <summary>清掉错误，重新建里面的内容。</summary>
    public void Retry()
    {
        Error = null;
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentTemplateProperty || change.Property == ResetKeyProperty) Retry();
        else if ((change.Property == FallbackProperty || change.Property == Provider.LangProperty) && Error is not null) Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (presenter.Content is null) Rebuild();
    }

    /// <summary>没有错误时建内容（建的时候出错就接住），有错误时换上出错提示。进了界面才建：属性初始化的先后不定，事件也要先订阅上。</summary>
    void Rebuild()
    {
        if (VisualRoot is null) return;
        if (Error is { } error)
        {
            presenter.Content = Fallback is { } fallback ? fallback(error, Retry) : DefaultFallback(error);
            return;
        }
        try
        {
            presenter.Content = ContentTemplate?.Build(DataContext);
        }
        catch (Exception caught)
        {
            Catch(caught, "create");
        }
    }

    void Catch(Exception error, string where)
    {
        Error = error;
        RaiseEvent(new ErrorCaughtEventArgs(ErrorCaughtEvent, error, where));
        Rebuild();
    }

    /// <summary>默认的出错提示：danger、sm 的 Result，标题、错误信息与"重试"（sm、soft、中性、RotateCcw）；内容是换上来的，读屏不会自己念到，标成立即播报。</summary>
    Control DefaultFallback(Exception error)
    {
        var lang = Provider.GetLang(this);
        var retry = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Icon = Lucide.RotateCcw, Content = lang.ErrorRetry };
        retry.Click += (_, _) => Retry();
        var result = new Result { Name = ErrorBoundaryParts.Fallback, Tone = StatusTone.Danger, Size = TextSize.Sm, Title = lang.ErrorTitle, Description = error.Message, Action = retry };
        AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Assertive);
        return result;
    }

    /// <summary>排版里面的内容；里面抛了异常就接住，换成出错提示再排。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        try
        {
            presenter.Measure(availableSize);
        }
        catch (Exception error) when (Error is null)
        {
            Catch(error, "layout");
            presenter.Measure(availableSize);
        }
        return presenter.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        try
        {
            presenter.Arrange(new Rect(finalSize));
        }
        catch (Exception error) when (Error is null)
        {
            Catch(error, "layout");
            presenter.Measure(finalSize);
            presenter.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}

/// <summary>ErrorBoundary 的部件名。</summary>
public static class ErrorBoundaryParts
{
    /// <summary>默认的出错提示（TS 的 propsFallback）：Result，danger、sm；里面的"重试"是 Button（TS 的 propsRetry）。</summary>
    public const string Fallback = "PART_Fallback";
}
