using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.Dialogs
{
    /// <summary>
    /// Phase 4.1：GoToLineWindow 从 WPF 迁移到 Avalonia。
    ///
    /// 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GoToLineWindow.xaml.cs（38 行）：
    ///   - 继承自 ForkPlusDialogWindow
    ///   - LineNumber 属性（int?）：用户输入的行号，OnSubmit 时解析
    ///   - ShowLogo=false, ShowHeader=false, IsTitleVisible=true
    ///   - Title = "Go To Line"（PreferencesLocalization.Current）
    ///   - SubmitButtonTitle = "Go"（PreferencesLocalization.Current）
    /// </summary>
    public partial class GoToLineWindow : ForkPlusDialogWindow
    {
        /// <summary>用户输入的行号（null 表示无效输入）。</summary>
        public int? LineNumber { get; private set; }

        public GoToLineWindow()
        {
            // 对照 WPF: ShowLogo=false, ShowHeader=false
            // Avalonia 简化基类没有 ShowLogo/ShowHeader，直接设置标题
            DialogTitle = "Go To Line";
            SubmitButtonTitle = "Go";
        }

        protected override void OnSubmit()
        {
            if (LineNumberTextBox != null && int.TryParse(LineNumberTextBox.Text, out int result))
            {
                LineNumber = result;
            }
            else
            {
                LineNumber = null;
            }
            CloseWithOk();
        }
    }
}
