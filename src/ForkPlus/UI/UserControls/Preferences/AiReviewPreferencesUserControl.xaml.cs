// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls + using Avalonia.Controls.Primitives（ScrollBarVisibility）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（FontFamily/SolidColorBrush/Color/TextWrapping/TextTrimming）
// - 新增 using Avalonia.Input（Key/KeyEventArgs/KeyModifiers）、Avalonia.Layout（Orientation/VerticalAlignment/HorizontalAlignment）、Avalonia.Platform.Storage（文件选择器）、Avalonia.VisualTree（GetVisualRoot）
// - PreviewKeyDown → KeyDown（Avalonia 无 PreviewKeyDown；KeyDown 即隧道+冒泡）；e.Key + Keyboard.Modifiers → e.Key + e.KeyModifiers.HasFlag(KeyModifiers.Control)
// - Microsoft.Win32.OpenFileDialog(Multiselect) → 内联 Avalonia StorageProvider.OpenFilePickerAsync(AllowMultiple=true)（保留原 Text files / All files 双过滤器；同步阻塞 GetAwaiter().GetResult()，参考 OpenDialog shim）
// - MessageBox.Show(msg) → new MessageBoxWindow(title, msg, "OK", showCancelButton:false).ShowDialog()
// - TextBox.LineCount（WPF 专属）→ 按 Text 中 '\n' 计数 +1
// - TextBox.Clear()（WPF 专属）→ Text = ""
// - BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed → BusyIndicator.IsVisible = busy
// - Dispatcher.Async（this.Dispatcher.Async 扩展，参考 DispatcherExtension）保持不变
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
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
			// 阶段 4.5：WPF PreviewKeyDown → Avalonia KeyDown（无 PreviewKeyDown；KeyDown 覆盖隧道+冒泡）。
			_skillInputTextBox.KeyDown += CustomSkillInputBox_KeyDown;

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
			// 阶段 4.5：WPF TextBox.LineCount（专属属性）→ 按 Text 中 '\n' 计数 +1（空文本算 1 行，与 WPF 一致）。
			int lineCount = (_skillInputTextBox.Text ?? "").Count(c => c == '\n') + 1;
			var sb = new System.Text.StringBuilder();
			for (int i = 1; i <= lineCount; i++)
			{
				sb.AppendLine(i.ToString());
			}
			_skillLineNumbers.Text = sb.ToString();
		}

		// 阶段 4.5：WPF PreviewKeyDown + System.Windows.Input.KeyEventArgs/Key/Keyboard.Modifiers/ModifierKeys
		// → Avalonia KeyDown + Avalonia.Input.KeyEventArgs/Key/e.KeyModifiers（HasFlag(KeyModifiers.Control)）。
		private void CustomSkillInputBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
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
		 // 阶段 4.5：WPF TextBox.Clear()（专属方法）→ Text = ""。
		 _skillInputTextBox.Text = "";
		 SkillNameTextBox.Text = "";

		 if (_initialized)
		 {
		  SaveSkills();
		 }
		}

		private void LoadSkillButton_Click(object sender, RoutedEventArgs e)
		{
		 // 阶段 4.5：WPF Microsoft.Win32.OpenFileDialog(Multiselect=true, Filter 双过滤器)
		 // → Avalonia StorageProvider.OpenFilePickerAsync(AllowMultiple=true, FileTypeFilter 双类型)。
		 // 原始 dialog.ShowDialog() 无 owner，用 GetVisualRoot() as Window ?? MainWindow.Instance 取宿主窗口。
		 // StorageProvider API 异步，事件处理器在 UI 线程，用 GetAwaiter().GetResult() 同步阻塞（参考 OpenDialog shim）。
		 string[] fileNames = PickSkillFiles();
		 if (fileNames != null && fileNames.Length > 0)
		 {
		  int loadedCount = 0;
		  foreach (string fileName in fileNames)
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
		    // 阶段 4.5：WPF MessageBox.Show(msg) → MessageBoxWindow(title, msg, "OK", showCancelButton:false).ShowDialog()。
		    new MessageBoxWindow(PreferencesLocalization.Current("Error"),
			    PreferencesLocalization.FormatCurrent("Failed to load file '{0}': {1}", fileName, ex.Message),
			    PreferencesLocalization.Current("OK"), showCancelButton: false).ShowDialog();
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

		// 阶段 4.5：Avalonia 多选文件选择器封装（等价 WPF OpenFileDialog.Multiselect + 双 Filter）。
		private string[] PickSkillFiles()
		{
			try
			{
				Window owner = (this.GetVisualRoot() as Window) ?? MainWindow.Instance;
				IStorageProvider storage = owner?.StorageProvider;
				if (storage == null)
				{
					return null;
				}
				FilePickerOpenOptions options = new FilePickerOpenOptions
				{
					Title = PreferencesLocalization.Current("Select skill file"),
					AllowMultiple = true,
					FileTypeFilter = new[]
					{
						new FilePickerFileType(PreferencesLocalization.Current("Text files (*.md;*.txt)")) { Patterns = new[] { "*.md", "*.txt" } },
						new FilePickerFileType(PreferencesLocalization.Current("All files (*.*)")) { Patterns = new[] { "*.*" } }
					}
				};
				IReadOnlyList<IStorageFile> results = storage.OpenFilePickerAsync(options).GetAwaiter().GetResult();
				if (results == null || results.Count == 0)
				{
					return null;
				}
				return results.Select(f => f.Path.LocalPath).ToArray();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show skill file picker", ex);
				return null;
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
			// 阶段 4.5：WPF UIElement.Visibility(Visible/Collapsed) → Avalonia Control.IsVisible(bool)。
			BusyIndicator.IsVisible = busy;
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
