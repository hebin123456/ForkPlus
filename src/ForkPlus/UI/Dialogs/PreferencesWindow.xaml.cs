using Avalonia.Controls.Selection;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class PreferencesWindow : ForkPlusDialogWindow, ForkPlus.UI.ILocalizableControl
	{
		private bool _initialised;
		private string _appliedLanguage;
		private readonly Dictionary<TabItem, string> _localizedTabLanguages = new Dictionary<TabItem, string>();

		protected override bool ApplyAutomaticLocalization => false;

		public PreferencesWindow()
		{
			base.ShowLogo = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			base.SizeToContent = SizeToContent.WidthAndHeight;
			Initialize();
		}

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

		protected override void OnSubmit()
		{
			base.OnSubmit();
			IntegrationUserControl.Save();
			AiReviewPreferencesUserControl.Save();
			CustomCommandsUserControl.Save();
			ForkPlusSettings.Default.Save();
		}

		private void Initialize()
		{
			GeneralUserControl.Initialize(this);
			CommitUserControl.Initialize();
			AiReviewPreferencesUserControl.Initialize();
			IntegrationUserControl.Initialize(this);
			GitUserControl.Initialize(this);
			CustomCommandsUserControl.InitializeGlobal(this);
			ApplyLocalization();
			_initialised = true;
		}

		public void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			if (_appliedLanguage == language)
			{
				ApplySelectedTabLocalization(language);
				return;
			}
			_appliedLanguage = language;
			_localizedTabLanguages.Clear();
			Title = PreferencesLocalization.Translate("Preferences", language);
			base.SubmitButtonTitle = PreferencesLocalization.Translate("Close", language);
			GeneralTabItem.Header = PreferencesLocalization.Translate("General", language);
			CommitTabItem.Header = PreferencesLocalization.Translate("Commit", language);
			AiReviewTabItem.Header = PreferencesLocalization.Translate("AI Enhancement", language);
			GitTabItem.Header = PreferencesLocalization.Translate("Git", language);
			IntegrationTabItem.Header = PreferencesLocalization.Translate("Integration", language);
			CustomCommandsTab.Header = PreferencesLocalization.Translate("Custom Commands", language);
			ApplySelectedTabLocalization(language);
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialised && e.AddedItems.Count >= 1 && e.AddedItems[0] is TabItem)
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				ApplySelectedTabLocalization(ForkPlusSettings.Default.UiLanguage);
			}
		}

		private void ApplySelectedTabLocalization(string language)
		{
			if (!(PreferencesTabControl.SelectedItem is TabItem selectedTab))
			{
				return;
			}
			if (_localizedTabLanguages.TryGetValue(selectedTab, out string appliedLanguage) && appliedLanguage == language)
			{
				return;
			}
			if (selectedTab.Content is DependencyObject content)
			{
				PreferencesLocalization.Apply(content, language);
				if (selectedTab.Content is IntegrationUserControl integrationUserControl)
				{
					integrationUserControl.ApplyLocalization();
				}
				_localizedTabLanguages[selectedTab] = language;
			}
		}

	}
}
