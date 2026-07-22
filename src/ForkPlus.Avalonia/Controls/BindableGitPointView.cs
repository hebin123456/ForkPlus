// Avalonia 版 BindableGitPointView（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/BindableGitPointView.cs（31 行）：
//   - 继承 GitPointView，添加 GitPoint 依赖属性
//   - OnPropertyChanged 监听 GitPointProperty 变化 → 设置 base.Value
//
// Avalonia 版差异：
//   1. WPF DependencyProperty → Avalonia StyledProperty<T>
//   2. WPF DependencyPropertyChangedEventArgs → Avalonia AvaloniaPropertyChangedEventArgs<T>
//   3. WPF GetValue/SetValue → Avalonia GetValue/SetValue（StyledProperty）
using Avalonia;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    public class BindableGitPointView : GitPointView
    {
        public static readonly StyledProperty<IGitPoint?> GitPointProperty =
            AvaloniaProperty.Register<BindableGitPointView, IGitPoint?>(nameof(GitPoint));

        public IGitPoint? GitPoint
        {
            get => GetValue(GitPointProperty);
            set => SetValue(GitPointProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == GitPointProperty)
            {
                Value = change.NewValue as IGitPoint;
            }
        }
    }
}
