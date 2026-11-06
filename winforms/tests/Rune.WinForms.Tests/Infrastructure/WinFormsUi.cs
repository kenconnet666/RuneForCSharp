using System.Diagnostics;

namespace Rune.WinForms.Tests;

/// <summary>
/// WinForms 测试的宿主：一个常驻的 STA 线程跑消息循环（Animator 的定时器依赖它），测试体排到它上面执行。
/// 窗口放在屏幕外：离屏窗口收不到 WM_PAINT，用 <see cref="Paint"/>（DrawToBitmap）代替系统的绘制，
/// 测“哪些控件失效了”而不是“画了几次”——失效就是下一帧要重绘的范围。
/// </summary>
static class WinFormsUi
{
    static readonly Control marshal = Start();
    static readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// 在 UI 线程上执行测试体；测试体里的异常原样抛给测试。一次只投递一个测试体：测试体里的 <see cref="Pump"/>
    /// 会处理排队的消息，别的测试体若已经排在队里就会在里面重入执行，改全局状态（RuneTheme.Current）的测试互相干扰（CI 上踩过）。
    /// </summary>
    public static async Task Run(Action body)
    {
        await gate.WaitAsync();
        try
        {
            var done = new TaskCompletionSource();
            marshal.BeginInvoke(() =>
            {
                try
                {
                    body();
                    done.SetResult();
                }
                catch (Exception e)
                {
                    done.SetException(e);
                }
            });
            await done.Task;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>放进屏幕外的窗口并画一次（登记绘制依赖）。</summary>
    public static Form Show(params Control[] controls)
    {
        var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(-6000, -6000), Size = new Size(800, 600),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        form.Controls.AddRange(controls);
        form.Show();
        Pump();
        Paint(form);
        return form;
    }

    /// <summary>代替 WM_PAINT 把控件（含子控件）画一遍。</summary>
    public static void Paint(Control control)
    {
        using var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
    }

    /// <summary>处理完排队的消息。</summary>
    public static void Pump()
    {
        for (var i = 0; i < 3; i++) Application.DoEvents();
    }

    /// <summary>边处理消息边等一段真实时间（定时器、动画按真实时间推进）。</summary>
    public static void Wait(TimeSpan duration)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < duration)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    static Control Start()
    {
        Control? result = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            result = new Control();
            result.CreateControl();
            ready.Set();
            Application.Run();
        })
        {
            IsBackground = true,
            Name = "WinForms 测试 UI 线程",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return result!;
    }
}

/// <summary>测试用的自绘控件：绘制时读一个信号，记录自己被失效了几次。</summary>
sealed class PaintProbe : Control
{
    readonly Effect paint;

    public PaintProbe(Signal<Color> color)
    {
        Color = color;
        paint = new Effect(Invalidate);
        Size = new Size(40, 20);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public Signal<Color> Color { get; }

    // 字段而不是属性：控件的公开属性会被 WinForms 设计器序列化（WFO1000 要求标注），测试计数用不着
    public int Invalidations;

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        Invalidations++;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var _ = paint.Track();
        using var brush = new SolidBrush(Color.Value);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }
}
