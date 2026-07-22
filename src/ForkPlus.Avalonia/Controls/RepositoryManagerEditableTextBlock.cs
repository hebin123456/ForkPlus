// Avalonia 版 RepositoryManagerEditableTextBlock（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/RepositoryManagerEditableTextBlock.cs（31 行）：
//   - 继承 EditableTextBlock
//   - OnPropertyChanged 监听 IsInEditModeProperty：
//     * 进入编辑模式 → ShowEditor(Value, callback)
//     * 退出编辑模式 → HideEditor() + Focus()
//
// Avalonia 版差异：
//   1. WPF DependencyPropertyChangedEventArgs → Avalonia AvaloniaPropertyChangedEventArgs
//   2. WPF IsInEditModeProperty/IsInEditMode → Avalonia IsEditingProperty/IsEditing
//   3. WPF Value → Avalonia Text
using Avalonia;

namespace ForkPlus.Avalonia.Controls
{
    public class RepositoryManagerEditableTextBlock : EditableTextBlock
    {
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != IsEditingProperty)
            {
                return;
            }
            if (change.NewValue is bool isEditing && isEditing)
            {
                ShowEditor(Text, (bool success, string newString) =>
                {
                    if (success)
                    {
                        Text = newString;
                    }
                    IsEditing = false;
                });
            }
            else
            {
                HideEditor();
                Focus();
            }
        }
    }
}
