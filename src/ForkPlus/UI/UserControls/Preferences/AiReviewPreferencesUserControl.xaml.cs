using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using Newtonsoft.Json;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class AiReviewPreferencesUserControl : UserControl
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private bool _initialized;
		private List<AiSkillEntry> _skills = new List<AiSkillEntry>();
		private TextBox _skillInputTextBox;
		private TextBlock _skillLineNumbers;

		public AiReviewPreferencesUserControl()
		{
		 InitializeComponent();

		 // Build line-numbered input area
		 BuildCustomSkillInputArea();

		 AddCustomSkillButton.Click += AddSkillButton_Click;
		 LoadSkillButton.Click += LoadSkillButton_Click;

		 // Localize buttons
		 CtrlEnterHintTextBlock.Text = PreferencesLocalization.Current("Ctrl+Enter to add");
		}

		private void BuildCustomSkillInputArea()
		{
			var innerGrid = new Grid();
			innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
			innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			_skillLineNumbers = new TextBlock
			{
				FontFamily = new FontFamily("Consolas"),
				FontSize = 12,
				Padding = new Thickness(4, 4, 2, 0),
				Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
				Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
				TextWrapping = TextWrapping.NoWrap,
				VerticalAlignment = VerticalAlignment.Top
			};
			Grid.SetColumn(_skillLineNumbers, 0);
			innerGrid.Children.Add(_skillLineNumbers);

			var scrollViewer = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				BorderThickness = new Thickness(0),
				Padding = new Thickness(0),
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			_skillInputTextBox = new TextBox
			{
				AcceptsReturn = true,
				AcceptsTab = true,
				TextWrapping = TextWrapping.NoWrap,
				BorderThickness = new Thickness(0),
				Padding = new Thickness(4, 0, 0, 0),
				FontFamily = new FontFamily("Consolas"),
				FontSize = 12,
				VerticalAlignment = VerticalAlignment.Top,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				MaxWidth = 2000
			};
			_skillInputTextBox.TextChanged += CustomSkillInputBox_TextChanged;
			_skillInputTextBox.PreviewKeyDown += CustomSkillInputBox_PreviewKeyDown;

			scrollViewer.Content = _skillInputTextBox;
			Grid.SetColumn(scrollViewer, 1);
			innerGrid.Children.Add(scrollViewer);

			CustomSkillInputBorder.Child = innerGrid;
			UpdateCustomSkillLineNumbers();
		}

		private void CustomSkillInputBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateCustomSkillLineNumbers();
		}

		private void UpdateCustomSkillLineNumbers()
		{
			if (_skillInputTextBox == null || _skillLineNumbers == null) return;
			int lineCount = _skillInputTextBox.LineCount;
			var sb = new System.Text.StringBuilder();
			for (int i = 1; i <= lineCount; i++)
			{
				sb.AppendLine(i.ToString());
			}
			_skillLineNumbers.Text = sb.ToString();
		}

		private void CustomSkillInputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
			{
				e.Handled = true;
				AddSkillButton_Click(sender, null);
			}
		}

		private void AddSkillButton_Click(object sender, RoutedEventArgs e)
		{
		 string text = (_skillInputTextBox.Text ?? "").Trim();
		 if (string.IsNullOrEmpty(text))
		  return;

		 string name = (SkillNameTextBox.Text ?? "").Trim();
		 if (string.IsNullOrEmpty(name))
		 {
		  // Auto-detect name from first line of content
		  string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		  name = lines.Length > 0 ? lines[0].Trim() : PreferencesLocalization.Current("Unnamed");
		 }
		 if (name.Length > 40) name = name.Substring(0, 40);

		 var existing = _skills.FirstOrDefault(s => s.Name == name);
		 if (existing != null)
		 {
		  existing.Content = text;
		 }
		 else
		 {
		  _skills.Add(new AiSkillEntry { Name = name, Content = text });
		 }

		 RefreshSkillList();
		 _skillInputTextBox.Clear();
		 SkillNameTextBox.Clear();

		 if (_initialized)
		 {
		  SaveSkills();
		 }
		}

		private void LoadSkillButton_Click(object sender, RoutedEventArgs e)
		{
		 Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		 {
		  Title = PreferencesLocalization.Current("Select skill file"),
		  Filter = PreferencesLocalization.Current("Text files (*.md;*.txt)") + "|*.md;*.txt|" + PreferencesLocalization.Current("All files (*.*)") + "|*.*",
		  CheckFileExists = true,
		  Multiselect = true
		 };
		 if (dialog.ShowDialog() == true)
		 {
		  int loadedCount = 0;
		  foreach (string fileName in dialog.FileNames)
		  {
		   try
		   {
		    string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(fileName);
		    string content = System.IO.File.ReadAllText(fileName);
		    if (string.IsNullOrWhiteSpace(content))
		     continue;

		    var existing = _skills.FirstOrDefault(s => s.Name == fileNameOnly);
		    if (existing != null)
		    {
		     existing.Content = content;
		    }
		    else
		    {
		     _skills.Add(new AiSkillEntry { Name = fileNameOnly, Content = content });
		    }
		    loadedCount++;
		   }
		   catch (Exception ex)
		   {
		    MessageBox.Show(PreferencesLocalization.FormatCurrent("Failed to load file '{0}': {1}", fileName, ex.Message));
		   }
		  }
			if (loadedCount > 0)
			{
				RefreshSkillList();
				if (_initialized)
				{
					SaveSkills();
				}
			}
		  }
		 }

		private void RemoveSkillButton_Click(object sender, RoutedEventArgs e)
		{
		 if (sender is Button button && button.Tag is AiSkillEntry entry)
		 {
		  _skills.Remove(entry);
		  RefreshSkillList();
		  if (_initialized)
		  {
		   SaveSkills();
		  }
		 }
		}

		private void RefreshSkillList()
		{
			SkillListBox.Items.Clear();

			foreach (var skill in _skills)
			{
				var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };

				var removeButton = new Button
				{
					Content = "×",
					Width = 20,
					Height = 20,
					FontSize = 12,
					Padding = new Thickness(0),
					Margin = new Thickness(0, 0, 4, 0),
					Tag = skill,
					ToolTip = PreferencesLocalization.Current("Remove this skill")
				};
				removeButton.Click += RemoveSkillButton_Click;
				panel.Children.Add(removeButton);

				// Show name + single-line preview
				string preview = skill.Content.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
				if (preview.Length > 80) preview = preview.Substring(0, 80) + "...";

				var textBlock = new TextBlock
				{
					Text = $"{skill.Name} — {preview}",
					FontSize = 12,
					VerticalAlignment = VerticalAlignment.Center,
					TextTrimming = TextTrimming.CharacterEllipsis
				};
				panel.Children.Add(textBlock);

				SkillListBox.Items.Add(new ListBoxItem { Content = panel });
			}
		}

		private static string AddLineNumbers(string content)
		{
		 if (string.IsNullOrEmpty(content))
		  return "";

		 string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
		 int pad = lines.Length.ToString().Length;
		 return string.Join("\n", lines.Select((line, idx) => (idx + 1).ToString().PadLeft(pad) + " | " + line));
		}

		private void SaveSkills()
		{
		 ForkPlusSettings.Default.AiDevSkillList = JsonConvert.SerializeObject(_skills);
		}

		private void LoadSkills()
		{
		 try
		 {
		  string json = ForkPlusSettings.Default.AiDevSkillList ?? "[]";
		  _skills = JsonConvert.DeserializeObject<List<AiSkillEntry>>(json) ?? new List<AiSkillEntry>();
		 }
		 catch
		 {
		  _skills = new List<AiSkillEntry>();
		 }
		 RefreshSkillList();
		}

		public void Initialize()
		{
			LoadSkills();
			ServiceUrlTextBox.Text = ForkPlusSettings.Default.AiReviewServiceUrl;
			ApiKeyTextBox.Text = ForkPlusSettings.Default.AiReviewApiKey;
			AutoFetchModelsCheckBox.IsChecked = ForkPlusSettings.Default.AiReviewAutoFetchModels;
			RetryCountTextBox.Text = ForkPlusSettings.Default.AiReviewRetryCount.ToString();
			TimeoutTextBox.Text = ForkPlusSettings.Default.AiReviewTimeoutSeconds.ToString();
			RefreshModelItems(ForkPlusSettings.Default.AiReviewModels, ForkPlusSettings.Default.AiReviewSelectedModel);
			_initialized = true;
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels && ForkPlusSettings.Default.AiReviewModels.Length == 0 && IsConfigured())
			{
				RefreshModels();
			}
		}

		public void Save()
		{
			if (_initialized)
			{
				SaveSkills();
			}
			SaveCurrentModel();
			ForkPlusSettings.Default.Save();
		}

		private void ServiceUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewServiceUrl = NormalizeUrl(ServiceUrlTextBox.Text);
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewApiKey = ApiKeyTextBox.Text ?? "";
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void AutoFetchModelsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewAutoFetchModels = AutoFetchModelsCheckBox.IsChecked.GetValueOrDefault();
			if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
			{
				RefreshModels();
			}
		}

		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized)
			{
				SaveCurrentModel();
			}
		}

		private void ModelComboBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (_initialized)
			{
				SaveCurrentModel();
			}
		}

		private void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
		{
			RefreshModels();
		}

		private void RetryCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			if (!int.TryParse(RetryCountTextBox.Text, out int value))
			{
				value = 3;
			}
			ForkPlusSettings.Default.AiReviewRetryCount = Math.Max(0, value);
		}

		private void TimeoutTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			if (!int.TryParse(TimeoutTextBox.Text, out int value))
			{
				value = 300;
			}
			ForkPlusSettings.Default.AiReviewTimeoutSeconds = Math.Max(10, value);
		}

		private void RefreshModels()
		{
			SaveCurrentModel();
			ForkPlusSettings.Default.AiReviewServiceUrl = NormalizeUrl(ServiceUrlTextBox.Text);
			ForkPlusSettings.Default.AiReviewApiKey = ApiKeyTextBox.Text ?? "";
			if (!IsConfigured())
			{
				SetStatus("Set service URL and API key first.");
				return;
			}
			SetBusy(true);
			SetStatus("Refreshing models...");
			string selectedModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			_jobQueue.Add(PreferencesLocalization.Current("Refresh AI models"), delegate
			{
				var response = OpenAiService.CreateFromAiReviewSettings().ListModels();
				Dispatcher.Async(delegate
				{
					SetBusy(false);
					if (!response.Succeeded)
					{
						SetStatus(response.Error.FriendlyMessage);
						return;
					}
					ForkPlusSettings.Default.AiReviewModels = response.Result;
					if (string.IsNullOrWhiteSpace(selectedModel) && response.Result.Length > 0)
					{
						ForkPlusSettings.Default.AiReviewSelectedModel = response.Result[0];
						selectedModel = response.Result[0];
					}
					RefreshModelItems(response.Result, selectedModel);
					SetStatus(PreferencesLocalization.FormatCurrent("Detected {0} models.", response.Result.Length));
				});
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		private void RefreshModelItems(string[] models, string selectedModel)
		{
			ModelComboBox.Items.Clear();
			foreach (string model in models ?? new string[0])
			{
				ModelComboBox.Items.Add(model);
			}
			if (!string.IsNullOrWhiteSpace(selectedModel))
			{
				ModelComboBox.Text = selectedModel;
			}
			else if (ModelComboBox.Items.Count > 0)
			{
				ModelComboBox.SelectedIndex = 0;
			}
		}

		private void SaveCurrentModel()
		{
			ForkPlusSettings.Default.AiReviewSelectedModel = (ModelComboBox.Text ?? "").Trim();
		}

		private bool IsConfigured()
		{
			return !string.IsNullOrWhiteSpace(ServiceUrlTextBox.Text) && !string.IsNullOrWhiteSpace(ApiKeyTextBox.Text);
		}

		private void SetBusy(bool busy)
		{
			BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
			RefreshModelsButton.IsEnabled = !busy;
		}

		private void SetStatus(string text)
		{
			StatusTextBlock.Text = PreferencesLocalization.Current(text);
		}

		private static string NormalizeUrl(string url)
		{
			string normalized = (url ?? "").Trim().TrimEnd('/');
			if (normalized.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/models".Length);
			}
			if (normalized.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1/chat/completions".Length);
			}
			if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(0, normalized.Length - "/v1".Length);
			}
			return normalized;
		}
	}
}
