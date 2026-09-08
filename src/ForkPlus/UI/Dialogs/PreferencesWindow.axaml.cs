using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

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
			// Migration note：WPF 原版靠首行 RowDefinition Height=0 隐藏 AddDialogHeader 的占位标题；
			// Avalonia 里 0 高度行的子控件仍溢出渲染（与 [Dialog Title] 与 Tab 行重叠），
			// 直接关掉 Header 生成（与 RepositoryStatisticsWindow 等 5 处同模式）。
			base.ShowHeader = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			base.SizeToContent = global::Avalonia.Controls.SizeToContent.WidthAndHeight;
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
			CredentialsUserControl.Initialize(this);
			CustomCommandsUserControl.InitializeGlobal(this);
			ImportExportUserControl.Initialize(this);
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
			CredentialsTabItem.Header = PreferencesLocalization.Translate("Credentials", language);
			IntegrationTabItem.Header = PreferencesLocalization.Translate("Integration", language);
			CustomCommandsTab.Header = PreferencesLocalization.Translate("Custom Commands", language);
			ImportExportTab.Header = PreferencesLocalization.Translate("Import/Export", language);
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
			if (selectedTab.Content is global::Avalonia.AvaloniaObject content)
			{
				PreferencesLocalization.Apply(content, language);
				if (selectedTab.Content is IntegrationUserControl integrationUserControl)
				{
					integrationUserControl.ApplyLocalization();
				}
				if (selectedTab.Content is CredentialsUserControl credentialsUserControl)
				{
					credentialsUserControl.ApplyLocalization();
				}
				_localizedTabLanguages[selectedTab] = language;
			}
		}


	}
}
