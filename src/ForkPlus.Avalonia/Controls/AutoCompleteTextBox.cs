using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using ForkPlus;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AutoCompleteTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AutoCompleteTextBox.cs（187 行）+
    //         src/ForkPlus/UI/Controls/IAutoCompleteProvider.cs（8 行）+
    //         src/ForkPlus/UI/Controls/AutoCompleteSuggestion.cs（15 行）+
    //         src/ForkPlus/UI/Controls/AutoCompleteSuggestions.cs（15 行）：
    //   - WPF AutoCompleteTextBox : PlaceholderTextBox（自定义 WPF 基类）
    //   - [TemplatePart] Popup（在 ControlTemplate 中声明）
    //   - SetAutocompleteProvider(IAutoCompleteProvider)
    //   - OnTextChanged → DelayedAction<bool>(0.03s) → RefreshSuggestions
    //   - OnIsKeyboardFocusWithinChanged → 失焦时 ClosePopup
    //   - OnPreviewKeyDown：Escape → ClosePopup / Return/Tab → SubmitSelectedSuggestion /
    //     Down → ListBox.SelectNextRow / Up → ListBox.SelectPreviousRow
    //   - RefreshSuggestions → provider.GetSuggestions(text, caretIndex) → OpenPopup
    //   - OpenPopup → 创建 ListBox + Items.Clear + Items.Add + PlacementRectangle
    //   - ClosePopup → TrySetPopupChild(null) + IsOpen=false
    //   - SubmitSelectedSuggestion → text.Replace(range, suggestion) + CaretIndex 更新
    //   - IAutoCompleteProvider 接口：GetSuggestions(string, int) → AutoCompleteSuggestions
    //   - AutoCompleteSuggestions：DropdownPosition + AutoCompleteSuggestion[] (Range + Suggestion)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 PlaceholderTextBox → Avalonia TextBox（内置 Watermark 属性，等价 WPF Placeholder）
    //   2. WPF Popup（ControlTemplate 中声明）→ spike 版代码构造 Popup + ListBox
    //      （task spec: spike 简化用 TextBox + Popup + ListBox 组合）
    //   3. WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubble)
    //   4. WPF OnIsKeyboardFocusWithinChanged → Avalonia LostFocus
    //   5. WPF VisualTreeAttachmentHelper.TrySetPopupChild → spike 用 Popup.Child = listBox
    //   6. IAutoCompleteProvider / AutoCompleteSuggestion / AutoCompleteSuggestions 类型
    //      在 WPF 工程 ForkPlus.UI.Controls 命名空间，Avalonia 工程不可访问
    //      spike 版定义同名本地接口（IAutoCompleteProvider + AutoCompleteSuggestion +
    //      AutoCompleteSuggestions POCO）保持 API 形状一致
    //   7. DelayedAction<T> 零成本复用（来自 ForkPlus.Core）
    //   8. spike 跳过 PlacementRectangle 精确位置计算（用 PlacementTarget=this 兜底）
    //   9. spike 跳过 GetRectFromCharacterIndex（WPF TextBox API，Avalonia 无对应）
    //      DropdownPosition 用 caretIndex 近似
    //
    // spike 简化：
    //   - 继承 TextBox + 内置 Popup + ListBox
    //   - SetAutocompleteProvider(IAutoCompleteProvider) 注入 provider
    //   - TextChanged → 延迟 0.03s → RefreshSuggestions → OpenPopup
    //   - Escape → ClosePopup / Return/Tab → SubmitSelectedSuggestion
    //   - Down/Up → ListBox 选中移动
    public class AutoCompleteTextBox : TextBox
    {
        // 对照 WPF: private const string ElementPopup = "Popup"
        // spike 版不依赖 ControlTemplate，直接代码构造 Popup

        // 对照 WPF: private Popup _popup
        private Popup _popup;

        // 对照 WPF: private ListBox _listBox
        private ListBox _listBox;

        // 对照 WPF: private IAutoCompleteProvider _autoCompleteProvider
        private IAutoCompleteProvider _autoCompleteProvider;

        // 对照 WPF: private readonly DelayedAction<bool> _refreshSuggestions (0.03s)
        private readonly DelayedAction<bool> _refreshSuggestions;

        // 对照 WPF: public bool DisableUpdates { get; set; }
        public bool DisableUpdates { get; set; }

        public AutoCompleteTextBox()
        {
            // 对照 WPF: _refreshSuggestions = new DelayedAction<bool>(RefreshSuggestions, 0.03)
            _refreshSuggestions = new DelayedAction<bool>(RefreshSuggestions, 0.03);

            // spike 版：代码构造 Popup + ListBox（替代 WPF ControlTemplate 中的 PART_Popup）
            _listBox = new ListBox
            {
                MinWidth = 216.0,
                MaxHeight = 5 * 21.0 + 8.0 + 16.0 // 对照 WPF: 5 项最大高度
            };
            _listBox.PointerReleased += (s, e) => SubmitSelectedSuggestion();

            _popup = new Popup
            {
                Child = _listBox,
                PlacementTarget = this,
                PlacementMode = PlacementMode.Bottom,
                IsOpen = false,
            };

            // 对照 WPF: OnPreviewKeyDown (Escape/Return/Tab/Down/Up)
            KeyDown += AutoCompleteTextBox_KeyDown;
            // 对照 WPF: OnIsKeyboardFocusWithinChanged → 失焦时 ClosePopup
            LostFocus += (s, e) => ClosePopup();

            // 对照 WPF: protected override void OnTextChanged(TextChangedEventArgs e)
            // Avalonia 11：TextBox 无可重写的 OnTextChanged(TextChangedEventArgs) 虚方法，
            // 改用 TextChanged 事件订阅（与 FilterTextBox.cs 一致）。
            TextChanged += (s, e) =>
            {
                if (!DisableUpdates)
                {
                    _refreshSuggestions.InvokeWithDelay(true);
                }
            };
        }

        // 对照 WPF: public void SetAutocompleteProvider(IAutoCompleteProvider autoCompleteProvider)
        public void SetAutocompleteProvider(IAutoCompleteProvider autoCompleteProvider)
        {
            _autoCompleteProvider = autoCompleteProvider;
        }

        // 对照 WPF: private void RefreshSuggestions(bool _)
        private void RefreshSuggestions(bool _)
        {
            AutoCompleteSuggestions suggestions = _autoCompleteProvider?.GetSuggestions(Text, CaretIndex);
            if (suggestions != null && suggestions.Suggestions.Length != 0)
            {
                OpenPopup(suggestions);
            }
            else
            {
                ClosePopup();
            }
        }

        // 对照 WPF: private void OpenPopup(AutoCompleteSuggestions autoComplete)
        private void OpenPopup(AutoCompleteSuggestions autoComplete)
        {
            // 对照 WPF: _listBox.Height = Math.Min(count, 5) * 21.0 + 8.0 + 16.0
            _listBox.MaxHeight = Math.Min(autoComplete.Suggestions.Length, 5) * 21.0 + 8.0 + 16.0;

            // 对照 WPF: _listBox.Items.Clear() + foreach Items.Add
            var items = new List<AutoCompleteSuggestion>();
            foreach (AutoCompleteSuggestion s in autoComplete.Suggestions)
            {
                items.Add(s);
            }
            _listBox.ItemsSource = items;
            _popup.IsOpen = true;
        }

        // 对照 WPF: private void ClosePopup()
        private void ClosePopup()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
                _listBox.ItemsSource = null;
            }
        }

        // 对照 WPF: private void SubmitSelectedSuggestion(bool fallbackToFirst = false)
        private void SubmitSelectedSuggestion(bool fallbackToFirst = false)
        {
            AutoCompleteSuggestion selected = _listBox.SelectedItem as AutoCompleteSuggestion;
            if (selected == null && fallbackToFirst)
            {
                // 对照 WPF: _listBox.Items.FirstItem<AutoCompleteSuggestion>()
                var items = _listBox.ItemsSource as List<AutoCompleteSuggestion>;
                if (items != null && items.Count > 0)
                {
                    selected = items[0];
                }
            }
            if (selected != null)
            {
                // 对照 WPF: Text.Replace(range, suggestion) + CaretIndex 更新
                // spike 版简化：直接替换整个 Text（spike 不实现 Range 精确替换）
                Text = selected.Suggestion;
                CaretIndex = selected.Suggestion.Length;
                ClosePopup();
                Focus();
            }
        }

        // 对照 WPF: protected override void OnPreviewKeyDown(KeyEventArgs e)
        private void AutoCompleteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_popup.IsOpen)
                {
                    ClosePopup();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                if (_popup.IsOpen)
                {
                    SubmitSelectedSuggestion(e.Key == Key.Tab);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down && _listBox != null)
            {
                // 对照 WPF: _listBox.SelectNextRow(_listBox.SelectedIndex, loop: true)
                int next = _listBox.SelectedIndex + 1;
                if (next >= _listBox.ItemCount) next = 0;
                if (next < _listBox.ItemCount)
                {
                    _listBox.SelectedIndex = next;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up && _listBox != null)
            {
                // 对照 WPF: _listBox.SelectPreviousRow(_listBox.SelectedIndex, loop: true)
                int prev = _listBox.SelectedIndex - 1;
                if (prev < 0) prev = _listBox.ItemCount - 1;
                if (prev >= 0)
                {
                    _listBox.SelectedIndex = prev;
                    e.Handled = true;
                }
            }
        }
    }
}
