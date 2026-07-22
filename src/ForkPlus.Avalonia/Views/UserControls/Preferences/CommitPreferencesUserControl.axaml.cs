// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/CommitPreferencesUserControl.xaml.cs（108 行）：
//   - public partial class CommitPreferencesUserControl : UserControl
//   - 字段：bool _initialized
//   - Initialize()：
//     * SystemComboBoxItem.Content = PreferencesLocalization.FormatCurrent("System ({0})", CultureInfo.InstalledUICulture.Name)
//     * 按 CommitSpellCheckingMode 选中 ComboBoxItem
//     * 读 ForkPlusSettings.Default 各项填入 TextBox
//     * _initialized = true
//   - TextChanged / SelectionChanged 处理：int.TryParse + 写 ForkPlusSettings.Default +
//     NotificationCenter.Current.RaiseXxxChanged(this, value) 通知其他窗口
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF NotificationCenter.Current.Raise* == spike 移除（NotificationCenter 在 WPF 工程，
//      Core 未抽出 INotificationService，spike 阶段仅写 ForkPlusSettings.Default）
//   2. WPF System.Windows.Controls.SelectionChangedEventArgs == Avalonia.Controls.SelectionChangedEventArgs
//   3. WPF System.Windows.Controls.TextChangedEventArgs == Avalonia.Controls.TextChangedEventArgs
//   4. WPF PreferencesLocalization.FormatCurrent == spike 保留（已 stub 委托 ServiceLocator）
//   5. WPF CultureInfo.InstalledUICulture.Name == spike 保留（System.Globalization）
//   6. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class CommitPreferencesUserControl : UserControl
    {
        // 对照 WPF: private bool _initialized;
        private bool _initialized;

        public CommitPreferencesUserControl()
        {
            InitializeComponent();
        }

        // 对照 WPF: public void Initialize()
        public void Initialize()
        {
            // 对照 WPF: SystemComboBoxItem.Content = PreferencesLocalization.FormatCurrent("System ({0})", CultureInfo.InstalledUICulture.Name);
            SystemComboBoxItem.Content = PreferencesLocalization.FormatCurrent("System ({0})", CultureInfo.InstalledUICulture.Name);

            // 对照 WPF: switch (ForkPlusSettings.Default.CommitSpellCheckingMode) 选中 ComboBoxItem
            switch (ForkPlusSettings.Default.CommitSpellCheckingMode)
            {
                case CommitSpellCheckingMode.Disable:
                    CommintSpellCheckingComboBox.SelectedItem = DisableComboBoxItem;
                    break;
                case CommitSpellCheckingMode.System:
                    CommintSpellCheckingComboBox.SelectedItem = SystemComboBoxItem;
                    break;
                case CommitSpellCheckingMode.English:
                    CommintSpellCheckingComboBox.SelectedItem = EnglishComboBoxItem;
                    break;
            }

            CommitSubjectLowLimitTextBox.Text = ForkPlusSettings.Default.CommitSubjectLowLimit.ToString();
            CommitSubjectHighLimitTextBox.Text = ForkPlusSettings.Default.CommitSubjectHighLimit.ToString();
            PageGuideLinePositionTextBox.Text = ForkPlusSettings.Default.PageGuideLinePosition.ToString();
            CommitMessageRegexTextBox.Text = ForkPlusSettings.Default.CommitMessageRegex;
            _initialized = true;
        }

        // 对照 WPF: private void PageGuideLinePositionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        // spike 版：移除 NotificationCenter.Current.RaisePageGuideLinePositionChanged
        private void PageGuideLinePositionTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_initialized)
            {
                if (!int.TryParse(PageGuideLinePositionTextBox.Text, out int result))
                {
                    result = 72;
                }
                ForkPlusSettings.Default.PageGuideLinePosition = result;
                // spike: NotificationCenter.Current.RaisePageGuideLinePositionChanged(this, result); // 移除
            }
        }

        // 对照 WPF: private void CommintSpellCheckingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        // spike 版：移除 NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged
        private void CommintSpellCheckingComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_initialized)
            {
                if (CommintSpellCheckingComboBox.SelectedItem is ComboBoxItem comboBoxItem)
                {
                    if (comboBoxItem == DisableComboBoxItem)
                    {
                        ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.Disable;
                        // spike: NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.Disable); // 移除
                    }
                    else if (comboBoxItem == SystemComboBoxItem)
                    {
                        ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.System;
                        // spike: NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.System); // 移除
                    }
                    else if (comboBoxItem == EnglishComboBoxItem)
                    {
                        ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.English;
                        // spike: NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.English); // 移除
                    }
                }
            }
        }

        // 对照 WPF: private void CommitSubjectLowLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        // spike 版：移除 NotificationCenter.Current.RaiseCommitSubjectLowLimitChanged
        private void CommitSubjectLowLimitTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            if (!int.TryParse(CommitSubjectLowLimitTextBox.Text, out int result))
            {
                result = 50;
            }
            ForkPlusSettings.Default.CommitSubjectLowLimit = result;
            // spike: NotificationCenter.Current.RaiseCommitSubjectLowLimitChanged(this, result); // 移除
        }

        // 对照 WPF: private void CommitSubjectHighLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        // spike 版：移除 NotificationCenter.Current.RaiseCommitSubjectHighLimitChanged
        private void CommitSubjectHighLimitTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            if (!int.TryParse(CommitSubjectHighLimitTextBox.Text, out int result))
            {
                result = 70;
            }
            ForkPlusSettings.Default.CommitSubjectHighLimit = result;
            // spike: NotificationCenter.Current.RaiseCommitSubjectHighLimitChanged(this, result); // 移除
        }

        // 对照 WPF: private void CommitMessageRegexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        private void CommitMessageRegexTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_initialized)
            {
                ForkPlusSettings.Default.CommitMessageRegex = CommitMessageRegexTextBox.Text ?? "";
            }
        }
    }
}
