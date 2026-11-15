using System.ComponentModel.DataAnnotations;

namespace Rune.Avalonia.Tests;

/// <summary>
/// FormController：路径点连接、失焦或提交过之后才显示错误；没写提示时文案跟着语言包；提交、提交失败的标注、连按只提交一次；自定义校验、SetError、Reset；
/// 不属于字段的错误；数组字段；IsDirty；单字段异步校验；局部校验与字段联动；约束。
/// </summary>
public class FormControllerTests
{
    public sealed class Address
    {
        [Required]
        public string City { get; set; } = "";
    }

    public sealed class Item
    {
        [Required]
        public string Name { get; set; } = "";
    }

    public sealed class Signup
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Range(18, 120)]
        public int? Age { get; set; }

        [StringLength(10, ErrorMessage = "昵称太长")]
        public string Nickname { get; set; } = "";

        public string Password { get; set; } = "";

        public string Confirm { get; set; } = "";

        public Address Address { get; set; } = new();

        public List<Item> Items { get; set; } = [];
    }

    static FormController<Signup> Create(Signup? initial = null, Func<Signup, Task>? submit = null) =>
        new(initial ?? new Signup(), () => Lang.ZhCn) { OnSubmit = submit };

    [Fact]
    [Upstream("form", "issue 的路径点连接；失焦或提交过之后才显示错误")]
    public async Task Paths_are_dotted_and_errors_show_after_blur_or_submit()
    {
        var form = Create();
        Assert.False(await form.ValidateAsync());
        Assert.Equal("必填", form.Errors["Address.City"]);
        Assert.Null(form.VisibleError("Email"));
        form.Blur("Email");
        await Task.Yield();
        Assert.Equal("必填", form.VisibleError("Email"));
        Assert.Null(form.VisibleError("Address.City"));
    }

    [Fact]
    [Upstream("form", "没写提示时报错跟随语言包")]
    public async Task Messages_follow_the_language_unless_written()
    {
        var form = Create(new Signup { Email = "x", Age = 3, Nickname = "一二三四五六七八九十十一" });
        await form.ValidateAsync();
        Assert.Equal(("邮箱格式不对", "应在 18 到 120 之间", "昵称太长"), (form.Errors["Email"], form.Errors["Age"], form.Errors["Nickname"]));
        var english = new FormController<Signup>(new Signup(), () => Lang.EnUs);
        await english.ValidateAsync();
        Assert.Equal(Lang.EnUs.FormRequired, english.Errors["Email"]);
    }

    [Fact]
    [Upstream("form", "提交：校验不通过不调用 onSubmit；之后随输入实时更新；通过后 submitting 直到完成")]
    public async Task Submit_validates_then_runs_and_keeps_submitting_until_done()
    {
        var done = new TaskCompletionSource();
        var calls = 0;
        var form = Create(submit: _ =>
        {
            calls++;
            return done.Task;
        });
        Assert.False(await form.SubmitAsync());
        Assert.Equal((0, 1, "必填"), (calls, form.SubmitCount, form.VisibleError("Email")));
        form.SetValue("Email", "a@b.cn");
        form.SetValue("Address.City", "上海");
        await Task.Yield();
        Assert.Null(form.VisibleError("Email"));
        var submitting = form.SubmitAsync();
        await Task.Yield();
        Assert.True(form.IsSubmitting);
        // 进行中再次提交（连按回车）：拿到同一个结果，OnSubmit 只调用一次
        Assert.Same(submitting, form.SubmitAsync());
        done.SetResult();
        Assert.True(await submitting);
        Assert.Equal((1, false), (calls, form.IsSubmitting));
    }

    [Fact]
    [Upstream("form", "onSubmit 抛带码的错误：明细标到字段，没有明细时是 formError，submit 返回 false；别的错误原样抛出")]
    public async Task Submit_exceptions_mark_fields_or_the_form()
    {
        var valid = new Signup { Email = "a@b.cn", Address = new() { City = "上海" } };
        var form = Create(valid, _ => throw new FormSubmitException(new Dictionary<string, string> { ["Email"] = "邮箱已注册" }));
        Assert.False(await form.SubmitAsync());
        Assert.Equal("邮箱已注册", form.VisibleError("Email"));
        var whole = Create(valid, _ => throw new FormSubmitException(form: "数据已被他人修改"));
        Assert.False(await whole.SubmitAsync());
        Assert.Equal("数据已被他人修改", whole.FormError);
        var broken = Create(valid, _ => throw new InvalidOperationException("网络断了"));
        await Assert.ThrowsAsync<InvalidOperationException>(broken.SubmitAsync);
    }

    [Fact]
    [Upstream("form", "自定义 validate 与 schema 合并；setError 写入服务端错误；reset 回到初始值并清空")]
    public async Task Custom_validation_set_error_and_reset()
    {
        var form = new FormController<Signup>(new Signup(), () => Lang.ZhCn)
        {
            Validate = values => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { ["Password"] = "太简单" }),
        };
        await form.ValidateAsync();
        Assert.Equal(("必填", "太简单"), (form.Errors["Email"], form.Errors["Password"]));
        form.SetValue("Email", "a@b.cn");
        form.SetError("Email", "邮箱已注册");
        await form.ValidateAsync();
        Assert.Equal("邮箱已注册", form.Errors["Email"]);
        // 这个字段的值改了：SetError 的错误消失
        form.SetValue("Email", "b@b.cn");
        await form.ValidateAsync();
        Assert.False(form.Errors.ContainsKey("Email"));
        form.Reset();
        Assert.Equal(("", 1, 0), (form.Values.Email, form.ResetCount, form.SubmitCount));
        Assert.Null(form.VisibleError("Password"));
    }

    [Fact]
    [Upstream("form", "不属于字段的错误（refine 没写 path）是 formError：提交过之后才有，提交不通过")]
    public async Task Errors_outside_fields_are_the_form_error()
    {
        var form = new FormController<Signup>(new Signup { Email = "a@b.cn", Address = new() { City = "上海" } }, () => Lang.ZhCn)
        {
            Validate = values => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { [""] = "两次密码不一致" }),
        };
        await form.ValidateAsync();
        Assert.Null(form.FormError);
        Assert.False(await form.SubmitAsync());
        Assert.Equal("两次密码不一致", form.FormError);
    }

    [Fact]
    [Upstream("form", "失焦过的字段随输入更新：改对了错误立刻消失，不用再失焦一次")]
    public async Task Touched_fields_update_as_you_type()
    {
        var form = Create();
        form.Blur("Email");
        await Task.Yield();
        Assert.Equal("必填", form.VisibleError("Email"));
        form.SetValue("Email", "a@b.cn");
        await Task.Yield();
        Assert.Null(form.VisibleError("Email"));
    }

    [Fact]
    [Upstream("form", "array：增删、插入、移动，key 稳定；错误与失焦记录跟着下标移动")]
    public async Task Arrays_keep_keys_and_move_errors()
    {
        var form = Create(new Signup { Items = [new() { Name = "甲" }, new() { Name = "" }] });
        var items = form.Array("Items");
        var keys = items.Keys.ToList();
        await form.ValidateAsync(["Items"]);
        Assert.Equal("必填", form.VisibleError("Items.1.Name"));
        items.Insert(0, new Item { Name = "乙" });
        Assert.Equal([items.Keys[0], keys[0], keys[1]], items.Keys);
        Assert.Equal("必填", form.VisibleError("Items.2.Name"));
        items.Move(2, 0);
        Assert.Equal("必填", form.VisibleError("Items.0.Name"));
        items.Remove(0);
        await form.ValidateAsync();
        Assert.DoesNotContain(form.Errors.Keys, key => key.StartsWith("Items", StringComparison.Ordinal));
        Assert.Equal(["乙", "甲"], form.Values.Items.Select(item => item.Name));
    }

    [Fact]
    [Upstream("form", "dirty：与初始值比较；reset(form.values) 之后以它为准")]
    public void Dirty_compares_with_the_baseline()
    {
        var form = Create();
        Assert.False(form.IsDirty);
        form.SetValue("Email", "a@b.cn");
        Assert.True(form.IsDirty);
        Assert.True(form.IsFieldDirty("Email"));
        Assert.False(form.IsFieldDirty("Nickname"));
        form.Reset(form.Values);
        Assert.False(form.IsDirty);
        Assert.Equal("a@b.cn", form.Values.Email);
    }

    [Fact]
    [Upstream("form", "单字段校验：防抖、只采用最后一次结果；提交时等它完成")]
    public async Task Field_validators_debounce_and_submit_waits()
    {
        var calls = new List<string?>();
        var form = new FormController<Signup>(new Signup { Address = new() { City = "上海" } }, () => Lang.ZhCn)
        {
            FieldValidators = new Dictionary<string, FieldValidator<Signup>>
            {
                ["Email"] = new(async (value, _, cancellation) =>
                {
                    calls.Add((string?)value);
                    await Task.Delay(20, cancellation);
                    return (string?)value == "taken@b.cn" ? "邮箱已注册" : null;
                }) { Debounce = TimeSpan.FromMilliseconds(30) },
            },
        };
        form.SetValue("Email", "t@b.cn");
        form.SetValue("Email", "taken@b.cn");
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(["taken@b.cn"], calls);
        form.Blur("Email");
        Assert.Equal("邮箱已注册", form.Errors["Email"]);
        Assert.False(await form.SubmitAsync());
        form.SetValue("Email", "ok@b.cn");
        Assert.True(await form.SubmitAsync());
        Assert.False(form.IsValidating());
    }

    [Fact]
    [Upstream("form", "validate(paths)：只看这些字段（含子路径），立即显示它们的错误，别的字段不显示")]
    public async Task Partial_validation_only_shows_its_fields()
    {
        var form = Create();
        Assert.False(await form.ValidateAsync(["Address"]));
        Assert.Equal("必填", form.VisibleError("Address.City"));
        Assert.Null(form.VisibleError("Email"));
        form.SetValue("Address.City", "上海");
        Assert.True(await form.ValidateAsync(["Address"]));
    }

    [Fact]
    [Upstream("form", "dependencies：依赖的字段改了，填过的字段重新校验并显示错误；没填过的不打扰")]
    public async Task Dependencies_revalidate_filled_fields()
    {
        var form = new FormController<Signup>(new Signup(), () => Lang.ZhCn)
        {
            Validate = values => Task.FromResult<IReadOnlyDictionary<string, string>?>(values.Confirm != values.Password
                ? new Dictionary<string, string> { ["Confirm"] = "两次密码不一致" }
                : null),
            Dependencies = new Dictionary<string, IReadOnlyList<string>> { ["Confirm"] = ["Password"] },
        };
        form.SetValue("Password", "a");
        await Task.Yield();
        Assert.Null(form.VisibleError("Confirm"));
        form.SetValue("Confirm", "a");
        form.SetValue("Password", "b");
        await Task.Yield();
        Assert.Equal("两次密码不一致", form.VisibleError("Confirm"));
    }

    [Fact]
    public void Constraints_come_from_the_attributes()
    {
        var form = Create();
        Assert.Equal(new FieldConstraints(Required: true), form.Constraints("Email"));
        Assert.Equal(new FieldConstraints(Min: 18, Max: 120), form.Constraints("Age"));
        Assert.Equal(new FieldConstraints(MaxLength: 10), form.Constraints("Nickname"));
        Assert.True(form.Constraints("Items.0.Name").Required);
        Assert.Equal(new FieldConstraints(), form.Constraints("Missing"));
    }
}
