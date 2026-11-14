using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Popconfirm 页的示例（vue-tsx-pro 文档站 overlay/Popconfirm）。成员示例的成败提示用一行状态文字（还没有 Toast）。</summary>
static class PopconfirmExamples
{
    public static Example[] All =>
    [
        new("Popconfirm", "members", "移出成员", "Trigger 同 Popover：放一个按钮。Confirm 的任务进行中确认按钮加载、取消暂不可用，完成后关闭；结果为 false（或失败）时保持打开，可以再试或取消，失败原因由调用方提示（这里写在下面的状态行）。打开时焦点落在“取消”上，误按 Enter 不会执行。浮层以问题为读屏名称、说明为帮助文字。", Members),
        new("Popconfirm", "variants", "色调、图标与位置", "Tone 决定图标与确认按钮的颜色，默认 warning，删除这类用 danger；Icon 换图标，写 null 不显示。Placement 默认在上方，Arrow 加指向触发器的箭头。Canceled 在点取消、点外面、按 Esc 时发。后果严重、要多写几句或填理由的用 AlertDialog / ConfirmAsync。", Variants),
    ];

    sealed record Member(string Name, string Role, bool Owner = false);

    static Control Members()
    {
        Member[] members = [new("林小满", "项目负责人", true), new("韩笑", "设计"), new("周一航", "前端（移出会失败）"), new("许悠", "测试")];
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        var status = new Small { Tone = TextTone.Muted, Text = "移出成员会等接口返回；名字里带“周”的模拟失败" };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);

        // 假的接口：600 毫秒后返回；名字里带“周”的模拟失败
        async Task<bool> Remove(Member member, Control row)
        {
            await Task.Delay(600);
            if (member.Name.Contains('周'))
            {
                status.Text = $"移出失败：没有权限（{member.Name}）";
                // 返回 false：气泡保持打开，可以再试或取消
                return false;
            }
            list.Children.Remove(row);
            status.Text = $"已移出 {member.Name}";
            return true;
        }

        foreach (var member in members)
        {
            var who = new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Sm,
                Children =
                {
                    new Avatar { DisplayName = member.Name, Size = TextSize.Sm },
                    new Flex { Direction = FlexDirection.Column, Children = { new Text { Text = member.Name, Size = TextSize.Sm, Weight = TextWeight.Medium }, new Small { Text = member.Role, Tone = TextTone.Muted } } },
                },
            };
            if (member.Owner) who.Children.Add(new Badge { Content = "负责人", Size = TextSize.Xs, Variant = StatusVariant.Soft, Tone = StatusTone.Accent });
            var row = new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Children = { who } };
            if (member.Owner) row.Children.Add(new Small { Text = "不能移出", Tone = TextTone.Muted });
            else
            {
                var remove = new IconButton { Icon = Lucide.UserMinus, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
                AutomationProperties.SetName(remove, $"移出 {member.Name}");
                row.Children.Add(new Popconfirm
                {
                    Trigger = remove, Title = $"把 {member.Name} 移出项目？", Description = "移出后不再收到项目的待办与通知，已提交的记录保留。",
                    Tone = StatusTone.Danger, ConfirmText = "移出", Confirm = () => Remove(member, row),
                });
            }
            list.Children.Add(row);
        }
        var empty = new Popconfirm
        {
            Trigger = new Button { Content = "清空回收站", Icon = Lucide.Trash, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger },
            Title = "清空回收站？", Description = "回收站里的 12 个文件将被永久删除。", Tone = StatusTone.Danger, Icon = Lucide.Trash, ConfirmText = "清空",
            Placement = FloatingPlacement.TopEnd,
            Confirm = () =>
            {
                status.Text = "回收站已清空";
                return Task.FromResult(true);
            },
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children = { list, new Flex { Justify = FlexJustify.End, Children = { empty } }, status },
        };
    }

    static Control Variants()
    {
        var last = new Small { Tone = TextTone.Muted, Text = "点按钮试试；点外面或按 Esc 等于取消" };
        AutomationProperties.SetLiveSetting(last, AutomationLiveSetting.Polite);
        Popconfirm Ask(Popconfirm popconfirm, string done, string canceled)
        {
            popconfirm.Confirm = () =>
            {
                last.Text = done;
                return Task.FromResult(true);
            };
            popconfirm.Canceled += (_, _) => last.Text = canceled;
            return popconfirm;
        }
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start,
            Children =
            {
                new Flex
                {
                    Gap = Space.Sm, Wrap = FlexWrap.Wrap,
                    Children =
                    {
                        Ask(new Popconfirm
                        {
                            Trigger = new Button { Content = "撤回（warning）", Icon = Lucide.Undo2, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral },
                            Title = "撤回这次提交？", Description = "撤回后审批流程回到草稿，审批人的意见会保留。", ConfirmText = "撤回",
                        }, "已撤回", "取消了撤回"),
                        Ask(new Popconfirm
                        {
                            Trigger = new Button { Content = "群发（accent、箭头）", Icon = Lucide.Send },
                            Title = "发送给全部 248 位客户？", Tone = StatusTone.Accent, Icon = Lucide.Send, ConfirmText = "发送", Placement = FloatingPlacement.Bottom, Arrow = true,
                        }, "已发送", "取消了发送"),
                        Ask(new Popconfirm
                        {
                            Trigger = new Button { Content = "归档（不显示图标）", Icon = Lucide.Archive, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral },
                            Title = "归档这个项目？", Icon = null, Tone = StatusTone.Neutral, ConfirmText = "归档", Placement = FloatingPlacement.Right,
                        }, "已归档", "取消了归档"),
                    },
                },
                last,
            },
        };
    }
}
