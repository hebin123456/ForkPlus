using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace ForkPlus.Avalonia.Views.Dialogs
{
    /// <summary>
    /// Avalonia 版 ForkPlusDialogWindow 简化基类（Phase 4.0）。
    ///
    /// 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ForkPlusDialogWindow.cs（770 行）：
    ///   - 继承自 CustomWindow（自定义标题栏）
    ///   - 命令预览区域 / ForkPlusDialogFooter / Logo / 警告图标 / 主题切换 / 焦点管理
    ///
    /// 本简化版实现：
    ///   - 继承自 Avalonia Controls.Window（用系统标题栏）
    ///   - Grid 3 行布局：Header / Content / Footer
    ///   - DialogTitle / DialogDescription 属性
    ///   - SubmitButton / CancelButton（默认显示）
    ///   - OnSubmit / OnCancel 虚方法（子类重写）
    ///   - Escape 键关闭
    ///   - SubmitButtonTitle / CancelButtonTitle 属性
    ///   - ShowSubmitButton / ShowCancelButton 属性
    ///
    /// 用法（子类 XAML）：
    ///   &lt;dialogs:ForkPlusDialogWindow xmlns="https://github.com/avaloniaui"
    ///       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    ///       xmlns:dialogs="clr-namespace:ForkPlus.Avalonia.Views.Dialogs;assembly=ForkPlus.Avalonia"
    ///       x:Class="ForkPlus.Avalonia.Views.Dialogs.GoToLineWindow"
    ///       Width="250" Height="120"&gt;
    ///       &lt;!-- Content 放在 DialogContentContainer 里（通过 Content 属性）--&gt;
    ///   &lt;/dialogs:ForkPlusDialogWindow&gt;
    /// </summary>
    public partial class ForkPlusDialogWindow : Window
    {
        // ===== 公共属性 =====

        /// <summary>对话框标题（显示在 Header 区域，与窗口 Title 同步）。</summary>
        public string DialogTitle
        {
            get => DialogTitleTextBlock?.Text ?? Title;
            set
            {
                Title = value;
                if (DialogTitleTextBlock != null)
                {
                    DialogTitleTextBlock.Text = value;
                }
            }
        }

        /// <summary>对话框描述（Header 区域副标题，默认不显示）。</summary>
        public string DialogDescription
        {
            get => DialogDescriptionTextBlock?.Text ?? "";
            set
            {
                if (DialogDescriptionTextBlock != null)
                {
                    DialogDescriptionTextBlock.Text = value;
                    DialogDescriptionTextBlock.IsVisible = !string.IsNullOrEmpty(value);
                }
            }
        }

        /// <summary>Submit 按钮文字（默认 "OK"）。</summary>
        public string SubmitButtonTitle
        {
            get => SubmitButton?.Content?.ToString() ?? "OK";
            set
            {
                if (SubmitButton != null)
                {
                    SubmitButton.Content = value;
                }
            }
        }

        /// <summary>Cancel 按钮文字（默认 "Cancel"）。</summary>
        public string CancelButtonTitle
        {
            get => CancelButton?.Content?.ToString() ?? "Cancel";
            set
            {
                if (CancelButton != null)
                {
                    CancelButton.Content = value;
                }
            }
        }

        /// <summary>是否显示 Submit 按钮（默认 true）。</summary>
        public bool ShowSubmitButton
        {
            get => SubmitButton?.IsVisible ?? true;
            set
            {
                if (SubmitButton != null)
                {
                    SubmitButton.IsVisible = value;
                }
            }
        }

        /// <summary>是否显示 Cancel 按钮（默认 true）。</summary>
        public bool ShowCancelButton
        {
            get => CancelButton?.IsVisible ?? true;
            set
            {
                if (CancelButton != null)
                {
                    CancelButton.IsVisible = value;
                }
            }
        }

        /// <summary>子类重写：是否允许提交（默认 true，操作进行中返回 false）。</summary>
        protected virtual bool IsSubmitAllowed => true;

        /// <summary>子类重写：是否自动应用本地化（默认 true）。</summary>
        protected virtual bool ApplyAutomaticLocalization => true;

        // ===== 构造函数 =====

        public ForkPlusDialogWindow()
        {
            InitializeComponent();
            Closed += ForkPlusDialogWindow_Closed;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ===== 事件处理 =====

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            OnSubmit();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            OnCancel();
        }

        /// <summary>子类重写：提交逻辑（默认 Close）。</summary>
        protected virtual void OnSubmit()
        {
            CloseWithOk();
        }

        /// <summary>子类重写：取消逻辑（默认 Close）。</summary>
        protected virtual void OnCancel()
        {
            Close();
        }

        /// <summary>以 OK 结果关闭窗口。</summary>
        protected void CloseWithOk()
        {
            Close(true);
        }

        /// <summary>更新 Submit 按钮可用状态。</summary>
        protected void UpdateSubmitButton()
        {
            if (SubmitButton != null)
            {
                SubmitButton.IsEnabled = IsSubmitAllowed;
            }
        }

        // ===== Escape 键关闭（对照 WPF OnKeyDown）=====
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel();
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }

        private void ForkPlusDialogWindow_Closed(object sender, EventArgs e)
        {
            Closed -= ForkPlusDialogWindow_Closed;
        }
    }
}
