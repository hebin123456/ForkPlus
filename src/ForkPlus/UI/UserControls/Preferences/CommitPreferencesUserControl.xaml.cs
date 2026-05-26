using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class CommitPreferencesUserControl : UserControl
	{
		private bool _initialized;

		public CommitPreferencesUserControl()
		{
			InitializeComponent();
		}

		public void Initialize()
		{
			SystemComboBoxItem.Content = PreferencesLocalization.FormatCurrent("System ({0})", CultureInfo.InstalledUICulture.Name);
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

		private void PageGuideLinePositionTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_initialized)
			{
				if (!int.TryParse(PageGuideLinePositionTextBox.Text, out var result))
				{
					result = 72;
				}
				ForkPlusSettings.Default.PageGuideLinePosition = result;
				NotificationCenter.Current.RaisePageGuideLinePositionChanged(this, result);
			}
		}

		private void CommintSpellCheckingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized)
			{
				ComboBoxItem comboBoxItem = CommintSpellCheckingComboBox.SelectedItem as ComboBoxItem;
				if (comboBoxItem == DisableComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.Disable;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.Disable);
				}
				else if (comboBoxItem == SystemComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.System;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.System);
				}
				else if (comboBoxItem == EnglishComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.English;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.English);
				}
			}
		}

		private void CommitSubjectLowLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!int.TryParse(CommitSubjectLowLimitTextBox.Text, out var result))
			{
				result = 50;
			}
			ForkPlusSettings.Default.CommitSubjectLowLimit = result;
			NotificationCenter.Current.RaiseCommitSubjectLowLimitChanged(this, result);
		}

		private void CommitSubjectHighLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!int.TryParse(CommitSubjectHighLimitTextBox.Text, out var result))
			{
				result = 70;
			}
			ForkPlusSettings.Default.CommitSubjectHighLimit = result;
			NotificationCenter.Current.RaiseCommitSubjectHighLimitChanged(this, result);
		}

		private void CommitMessageRegexTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_initialized)
			{
				ForkPlusSettings.Default.CommitMessageRegex = CommitMessageRegexTextBox.Text ?? "";
			}
		}

	}
}
