using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CommandTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CommandTextBox.cs（164 行）：
    //   - WPF CommandTextBox : TextBox
    //   - [TemplatePart] PART_LabelsStackPanel (FrameworkElement)
    //   - [TemplatePart] PART_PlaceholderTextBox (FrameworkElement)
    //   - CommandArgumentsCompleted / CommandArgumentChanged 事件
    //   - SetCommandDescriptor(CommandDescriptor) 启动多参数输入流
    //   - MoveNextArgument(CommandProviderItem) 推进到下一参数
    //   - RemoveArgument() Back 键回退上一参数
    //   - PushSection / PopSection：labels StackPanel 增删 Border + TextBlock
    //   - PreferencesLocalization.Translate(text, UiLanguage)
    //   - CommandDescriptor / Argument / CommandProviderItem 类型来自 ForkPlus.UI.Commands
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBox + KeyDown 监听 Enter）：
    //   1. 基类 TextBox → Avalonia.Controls.TextBox（API 一致）
    //   2. WPF [TemplatePart] + OnApplyTemplate → spike 跳过
    //      （Avalonia TextBox 已内置 Watermark，spike 不实现自定义 TemplatePart）
    //   3. WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubble)
    //   4. WPF DependencyObject → Avalonia AvaloniaObject（API 一致）
    //   5. spike 跳过 CommandDescriptor / Argument / CommandProviderItem 类型依赖
    //      （ForkPlus.UI.Commands 命名空间在 WPF 工程，Avalonia 不可访问）
    //      改用 SetArguments(string[]) 公共方法注入参数名列表
    //   6. spike 跳过 PreferencesLocalization（ServiceLocator.Localization 替代）
    //   7. spike 跳过 Theme.CommandTextBox.LabelBackgroundBrush / LabelForegroundBrush
    //      用硬编码颜色 spike 兜底
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBox + KeyDown 监听 Enter 提交
    //   - SetArguments(string[]) 公共方法注入参数名列表
    //   - CommandArgumentsCompleted / CommandArgumentChanged 事件
    //   - Enter 键推进下一参数 / Back 键（CaretIndex=0）回退上一参数
    public class CommandTextBox : TextBox
    {
        // 对照 WPF: public EventHandler<object[]> CommandArgumentsCompleted
        public event EventHandler<object[]> CommandArgumentsCompleted;

        // 对照 WPF: public EventHandler CommandArgumentChanged
        public event EventHandler CommandArgumentChanged;

        // 对照 WPF: private int _activeArgumentIndex = -1
        private int _activeArgumentIndex = -1;

        // 对照 WPF: private Stack<object> _completedArguments = new Stack<object>()
        private readonly Stack<object> _completedArguments = new Stack<object>();

        // 对照 WPF: private string[] _argumentNames（CommandDescriptor.Arguments[].Name）
        // spike 版：用 string[] 简化（替代 CommandDescriptor.Arguments）
        private string[] _argumentNames;

        // spike 版硬编码画刷（替代 WPF Theme.CommandTextBox.LabelBackgroundBrush / LabelForegroundBrush）
        private static readonly IBrush LabelBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        private static readonly IBrush LabelForegroundBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        // 对照 WPF: public Argument CurrentCommandArgument
        // spike 版：返回当前参数名（string 替代 Argument）
        public string CurrentCommandArgument =>
            (_argumentNames != null && _activeArgumentIndex >= 0 && _activeArgumentIndex < _argumentNames.Length)
                ? _argumentNames[_activeArgumentIndex]
                : null;

        // 对照 WPF: public CommandDescriptor CommandDescriptor { get; private set; }
        // spike 版：用 string[] 替代 CommandDescriptor
        public string[] ArgumentNames => _argumentNames;

        public CommandTextBox()
        {
            // 对照 WPF: _textBox.PreviewKeyDown (Key.Back + CaretIndex==0 → RemoveArgument)
            // spike 版：KeyDown 替代 PreviewKeyDown（Avalonia 无 tunneling）
            KeyDown += CommandTextBox_KeyDown;
        }

        // 对照 WPF: public void SetCommandDescriptor(CommandDescriptor commandDescriptor)
        // spike 版简化：用 SetArguments(string[]) 替代 SetCommandDescriptor
        public void SetArguments(string[] argumentNames)
        {
            _argumentNames = argumentNames;
            _activeArgumentIndex = (argumentNames != null && argumentNames.Length > 0) ? 0 : -1;
            _completedArguments.Clear();
            Watermark = CurrentCommandArgument ?? "Command";
            CommandArgumentChanged?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: public void MoveNextArgument(CommandProviderItem currentItem)
        // spike 版简化：用 MoveNextArgument(object) 替代（去掉 CommandProviderItem 类型依赖）
        public void MoveNextArgument(object argumentValue, string title = null)
        {
            if (_argumentNames != null && _activeArgumentIndex >= 0)
            {
                _completedArguments.Push(argumentValue);
                if (_activeArgumentIndex == _argumentNames.Length - 1)
                {
                    // 对照 WPF: CommandArgumentsCompleted?.Invoke(this, _completedArguments.ToArray())
                    CommandArgumentsCompleted?.Invoke(this, _completedArguments.ToArray());
                    return;
                }
                _activeArgumentIndex++;
                Watermark = CurrentCommandArgument;
            }
        }

        // 对照 WPF: private void RemoveArgument()
        private void RemoveArgument()
        {
            if (_activeArgumentIndex > 0)
            {
                _activeArgumentIndex--;
                if (_completedArguments.Count > 0)
                {
                    _completedArguments.Pop();
                }
                Watermark = CurrentCommandArgument;
                CommandArgumentChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_activeArgumentIndex == 0)
            {
                // 对照 WPF: SetCommandDescriptor(null) 清空
                SetArguments(null);
            }
        }

        // 对照 WPF: _textBox.PreviewKeyDown (Key.Back + CaretIndex==0 → RemoveArgument)
        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // spike 版：Enter 键提交当前参数
                MoveNextArgument(Text);
                Clear();
                e.Handled = true;
            }
            else if (e.Key == Key.Back && CaretIndex == 0)
            {
                RemoveArgument();
                e.Handled = true;
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        // spike 版：直接返回原文本（spike 不依赖 Localization）
        private static string Translate(string text)
        {
            return text;
        }
    }
}
