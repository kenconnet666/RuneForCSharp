using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Rune.Avalonia.Examples;

/// <summary>SlideCaptcha 的示例（vue-tsx-pro 文档站 patterns/SlideCaptcha）：这里在本地自己出题、自己判对错，只为演示；真实应用的图和答案都在服务端。</summary>
static class SlideCaptchaExamples
{
    public static Example[] All =>
    [
        new("SlideCaptcha", "basic", "拼图滑块验证", "拖动滑块让拼图对准缺口，松手把位置、轨迹与时长交给\"服务端\"判定（这里本地判：差 4 像素以内、用时不少于 280ms 算通过；没对准时换一张）。键盘：←/→ 微调、Enter 松手。", Basic),
    ];

    const int W = 320, H = 160, Size = 46, PieceWidth = Size + 11, Tolerance = 4;

    /// <summary>随机色相的渐变加几个圆；缺口的左边缘位置作为答案一起返回。</summary>
    static (SlideChallenge Challenge, int Answer) Make()
    {
        var random = Random.Shared;
        var hue = random.Next(360);
        var x = PieceWidth + 40 + random.Next(W - 2 * PieceWidth - 52);
        var y = 12 + random.Next(H - Size - 24);
        var circles = Enumerable.Range(0, 8).Select(_ => (X: random.Next(W), Y: random.Next(H), R: 10 + random.Next(28), Hue: random.Next(360))).ToList();
        var hole = new RoundedRect(new Rect(x, y, Size, Size), 6);
        void Scene(DrawingContext context)
        {
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Hsl(hue, 0.7, 0.58), 0), new GradientStop(Hsl((hue + 60) % 360, 0.7, 0.4), 1) },
            }, new Rect(0, 0, W, H));
            foreach (var circle in circles) context.DrawEllipse(new SolidColorBrush(Hsl(circle.Hue, 0.8, 0.7), 0.35), null, new Point(circle.X, circle.Y), circle.R, circle.R);
        }
        var background = new RenderTargetBitmap(new PixelSize(W, H));
        using (var context = background.CreateDrawingContext())
        {
            Scene(context);
            context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.32), new Pen(new SolidColorBrush(Colors.White, 0.7)), hole);
        }
        var piece = new RenderTargetBitmap(new PixelSize(PieceWidth, H));
        using (var context = piece.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateTranslation(-x, 0)))
        {
            using (context.PushGeometryClip(new RectangleGeometry(hole.Rect, 6, 6)))
                Scene(context);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.White, 0.9)), hole);
        }
        return (new SlideChallenge(Guid.NewGuid().ToString(), background, piece, W, H, PieceWidth), x);
    }

    static Color Hsl(double hue, double saturation, double lightness) => HslColor.ToRgb(hue, saturation, lightness, 1);

    static Control Basic()
    {
        var captcha = new SlideCaptcha();
        var message = new Small { Tone = TextTone.Muted, Text = "拖动滑块，让拼图对准缺口" };
        var again = new Button { Variant = ButtonVariant.Outline, Size = TextSize.Sm, Content = "再来一次", IsVisible = false };
        var answer = 0;
        void Next()
        {
            (captcha.Challenge, answer) = Make();
            captcha.Status = SlideStatus.Idle;
            message.Text = "拖动滑块，让拼图对准缺口";
            again.IsVisible = false;
        }
        // 真实应用在这里把结果发给服务端，服务端按位置和轨迹判定
        captcha.Released += (_, e) =>
        {
            captcha.Status = SlideStatus.Verifying;
            DispatcherTimer.RunOnce(() =>
            {
                var result = e.Result;
                var ok = Math.Abs(result.X - answer) <= Tolerance && result.Duration >= 280;
                captcha.Status = ok ? SlideStatus.Success : SlideStatus.Error;
                message.Text = $"位置 {result.X}px（答案 {answer}px），用时 {result.Duration}ms，轨迹 {result.Track.Count} 点";
                again.IsVisible = ok;
                if (!ok) DispatcherTimer.RunOnce(Next, TimeSpan.FromMilliseconds(900));
            }, TimeSpan.FromMilliseconds(400));
        };
        captcha.RefreshRequested += (_, _) => Next();
        again.Click += (_, _) => Next();
        Next();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { new Border { Width = 320, Child = captcha }, message, again } };
    }
}
