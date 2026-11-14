using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>测试用的命令：记下参数，能不能执行由 <paramref name="canExecute"/> 定。</summary>
sealed class TestCommand(Func<object?, bool>? canExecute = null) : ICommand
{
    public List<object?> Executed { get; } = [];

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => Executed.Add(parameter);
}
