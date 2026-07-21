using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 EditableTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/EditableTextBlock.cs（110 行）：
    //   - WPF EditableTextBlock : Control（无内置可视树，依赖 ControlTemplate）
    //   - ValueProperty (string) / IsInEditModeProperty (bool) DependencyProperty
    //   - ShowEditor(text, Action<bool,string> callback, centeredHorizontally)
    //     创建 CustomAdorner（WPF AdornerLayer 浮层）+ 内嵌 TextBox
    //     - TextBox.PreviewKeyDown: Return → callback(true, text) / Escape → callback(false, text)
    //     - TextBox.LostKeyboardFocus → callback(true, text)
    //     - LayoutUpdated → Focus()
    //   - HideEditor() 移除 Adorner + IsInEditMode = false
    //   - CreateAdornerTextBox(text, callback) 构造编辑用 TextBox
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 Control → Grid（Avalonia 11 Control 无默认可视树，
    //      spike 用 Grid 作为容器托管 TextBlock + TextBox 两个子控件）
    //   2. WPF AdornerLayer 浮层 → spike 直接 Children.Add(TextBox) 切换显示
    //      （Avalonia 11 没有 WPF AdornerLayer 等价物，spike 用子控件切换实现编辑态）
    //   3. DependencyProperty.Register → StyledProperty<T>.Register
    //   4. WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubble)
    //   5. WPF LostKeyboardFocus → Avalonia LostFocus
    //   6. spike 用 Action<bool, string> 回调签名与 WPF 一致
    //      （true=提交, false=取消）
    //
    // spike 简化：
    //   - Text StyledProperty（task spec 关键 API：Text）
    //   - IsEditing StyledProperty（task spec 关键 API：IsEditing）
    //   - EditStarted / EditCompleted 事件（task spec 关键 API）
    //   - ShowEditor(text, callback) 切换到 TextBox + 自动 Focus
    //   - HideEditor() 切换回 TextBlock
    public class EditableTextBlock : Grid
    {
        // 对照 WPF: ValueProperty (string)
        // spike 版按 task spec 改名为 Text（task spec 关键 API）
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<EditableTextBlock, string>(nameof(Text));

        // 对照 WPF: IsInEditModeProperty (bool)
        // spike 版按 task spec 改名为 IsEditing（task spec 关键 API）
        public static readonly StyledProperty<bool> IsEditingProperty =
            AvaloniaProperty.Register<EditableTextBlock, bool>(nameof(IsEditing));

        // task spec 关键 API：EditStarted / EditCompleted 事件
        public event EventHandler EditStarted;
        public event EventHandler<EditCompletedEventArgs> EditCompleted;

        // spike 版子控件：显示态 TextBlock + 编辑态 TextBox
        private readonly TextBlock _displayTextBlock;
        private readonly TextBox _editTextBox;

        // spike 版编辑回调（对照 WPF: Action<bool, string> editedCallback）
        private Action<bool, string> _editedCallback;

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsEditing
        {
            get => GetValue(IsEditingProperty);
            set => SetValue(IsEditingProperty, value);
        }

        public EditableTextBlock()
        {
            // spike 版子控件初始化
            _displayTextBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _editTextBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsVisible = false, // 默认隐藏，编辑时显示
            };

            // 对照 WPF: TextBox.PreviewKeyDown (Return → 提交 / Escape → 取消)
            _editTextBox.KeyDown += EditTextBox_KeyDown;
            // 对照 WPF: TextBox.LostKeyboardFocus → callback(true, text)
            _editTextBox.LostFocus += EditTextBox_LostFocus;

            Children.Add(_displayTextBlock);
            Children.Add(_editTextBox);
        }

        // 对照 WPF: public void ShowEditor(string text, Action<bool, string> editedCallback, bool centeredHorizontally = false)
        // spike 版简化：centeredHorizontally 参数保留但忽略（spike 不调整布局）
        public void ShowEditor(string text, Action<bool, string> editedCallback, bool centeredHorizontally = false)
        {
            _editedCallback = editedCallback;
            _editTextBox.Text = text;
            _editTextBox.SelectAll();
            _editTextBox.IsVisible = true;
            _displayTextBlock.IsVisible = false;
            IsEditing = true;
            EditStarted?.Invoke(this, EventArgs.Empty);
            _editTextBox.Focus();
        }

        // 对照 WPF: public void HideEditor()
        public void HideEditor()
        {
            _editTextBox.IsVisible = false;
            _displayTextBlock.IsVisible = true;
            IsEditing = false;
        }

        // 对照 WPF: TextBox.PreviewKeyDown
        //   Return → editedCallback(true, text)（提交）
        //   Escape → editedCallback(false, text)（取消）
        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CommitEdit(true);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CommitEdit(false);
            }
        }

        // 对照 WPF: TextBox.LostKeyboardFocus → editedCallback(true, text)
        //   spike 版：失焦时提交（与 WPF 一致）
        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (IsEditing)
            {
                CommitEdit(true);
            }
        }

        // spike 版辅助：提交编辑（true=提交 / false=取消）
        private void CommitEdit(bool commit)
        {
            string text = _editTextBox.Text;
            HideEditor();
            _editedCallback?.Invoke(commit, text);
            EditCompleted?.Invoke(this, new EditCompletedEventArgs(commit, text));
        }

        // spike 版：当 Text 属性变化时同步显示态 TextBlock
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty)
            {
                _displayTextBlock.Text = Text;
            }
        }
    }

    // spike 版 EditCompleted 事件参数（task spec 关键 API：EditCompleted 事件）
    public class EditCompletedEventArgs : EventArgs
    {
        public bool Committed { get; }
        public string Text { get; }

        public EditCompletedEventArgs(bool committed, string text)
        {
            Committed = committed;
            Text = text;
        }
    }
}
