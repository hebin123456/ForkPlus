// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/AiReviewPreferencesUserControl.xaml.cs（486 行）：
//   - public partial class AiReviewPreferencesUserControl : UserControl
//   - 字段：JobQueue _jobQueue / bool _initialized / List<AiSkillEntry> _skills /
//     TextBox _skillInputTextBox / TextBlock _skillLineNumbers
//   - 构造函数：BuildCustomSkillInputArea（动态构造 Grid + ScrollViewer + TextBox）+
//     AddCustomSkillButton.Click + LoadSkillButton.Click + CtrlEnterHintTextBlock.Text
//   - Initialize()：LoadSkills + 读 ForkPlusSettings.AiReview* 各项 + RefreshModelItems
//   - RefreshModels：JobQueue + OpenAiService.ListModels + Dispatcher.Async
//   - AddSkillButton_Click：从 SkillNameTextBox + SkillInputTextBox 读，加入 _skills，SaveSkills
//   - LoadSkillButton_Click：OpenFileDialog + File.ReadAllText，加入 _skills，SaveSkills
//   - RemoveSkillButton_Click：从 _skills 移除，SaveSkills
//   - SaveSkills / LoadSkills：JsonConvert + ForkPlusSettings.AiDevSkillList
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF 动态构造 BuildCustomSkillInputArea == spike 改用 axaml 静态声明
//      SkillInputTextBox + SkillLineNumbers
//   2. WPF Microsoft.Win32.OpenFileDialog == spike 移除（spike 用 stub 注释）
//   3. WPF JobQueue == spike 移除（spike 不做异步刷新模型）
//   4. WPF OpenAiService.ListModels == spike 移除（spike 不调用 AI 服务）
//   5. WPF Dispatcher.Async == spike 移除
//   6. WPF MessageBox.Show == spike 移除
//   7. WPF SkillListBox.Items.Add(ListBoxItem { Content = StackPanel }) == spike
//      改用 ItemsControl + ItemsSource 绑定（spike 用 Items.Add 直接塞 AiSkillEntry）
//   8. WPF _skillInputTextBox.PreviewKeyDown Ctrl+Enter == spike 用 KeyDown 事件
//   9. WPF LineCount == Avalonia TextBox.Text 按行分割计算
//  10. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Settings;
using Newtonsoft.Json;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class AiReviewPreferencesUserControl : UserControl
    {
        // 对照 WPF: private readonly JobQueue _jobQueue = new JobQueue();
        // spike 版：移除 JobQueue（spike 不做异步刷新模型）

        // 对照 WPF: private bool _initialized;
        private bool _initialized;

        // 对照 WPF: private List<AiSkillEntry> _skills = new List<AiSkillEntry>();
        private List<AiSkillEntry> _skills = new List<AiSkillEntry>();

        // 对照 WPF: private TextBox _skillInputTextBox; private TextBlock _skillLineNumbers;
        // spike 版：axaml 静态声明，无需字段

        public AiReviewPreferencesUserControl()
        {
            InitializeComponent();

            // 对照 WPF: BuildCustomSkillInputArea(); // spike 移除（axaml 静态声明）
            // 对照 WPF: AddCustomSkillButton.Click += AddSkillButton_Click; // axaml 中已绑定 Click
            // 对照 WPF: LoadSkillButton.Click += LoadSkillButton_Click; // axaml 中已绑定 Click

            // 对照 WPF: CtrlEnterHintTextBlock.Text = PreferencesLocalization.Current("Ctrl+Enter to add");
            CtrlEnterHintTextBlock.Text = PreferencesLocalization.Current("Ctrl+Enter to add");

            // spike 版：用 KeyDown 替代 WPF PreviewKeyDown 处理 Ctrl+Enter
            SkillInputTextBox.KeyDown += CustomSkillInputBox_KeyDown;
            UpdateCustomSkillLineNumbers();
        }

        // 对照 WPF: private void CustomSkillInputBox_TextChanged(...)
        private void CustomSkillInputBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateCustomSkillLineNumbers();
        }

        // 对照 WPF: private void UpdateCustomSkillLineNumbers()
        private void UpdateCustomSkillLineNumbers()
        {
            if (SkillInputTextBox == null || SkillLineNumbers == null) return;
            // spike 版：用 Text.Split 计算行数（Avalonia TextBox 无 LineCount 属性）
            string text = SkillInputTextBox.Text ?? "";
            int lineCount = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                sb.AppendLine(i.ToString());
            }
            SkillLineNumbers.Text = sb.ToString();
        }

        // 对照 WPF: private void CustomSkillInputBox_PreviewKeyDown(...)
        // spike 版：用 KeyDown 替代 PreviewKeyDown
        private void CustomSkillInputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                AddSkillButton_Click(sender, null);
            }
        }

        // 对照 WPF: private void AddSkillButton_Click(object sender, RoutedEventArgs e)
        private void AddSkillButton_Click(object? sender, RoutedEventArgs? e)
        {
            string text = (SkillInputTextBox.Text ?? "").Trim();
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
            SkillInputTextBox.Text = "";
            SkillNameTextBox.Text = "";

            if (_initialized)
            {
                SaveSkills();
            }
        }

        // 对照 WPF: private void LoadSkillButton_Click(object sender, RoutedEventArgs e)
        // spike 版：移除 Microsoft.Win32.OpenFileDialog（spike 用 stub 注释）
        private void LoadSkillButton_Click(object? sender, RoutedEventArgs e)
        {
            // spike: Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog { ... };
            //        if (dialog.ShowDialog() == true) { foreach (string fileName in dialog.FileNames) { ... } }
            // spike 版：占位（spike 不弹文件对话框）
        }

        // 对照 WPF: private void RemoveSkillButton_Click(object sender, RoutedEventArgs e)
        private void RemoveSkillButton_Click(object? sender, RoutedEventArgs e)
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

        // 对照 WPF: private void RefreshSkillList()
        // spike 版：用 ItemsControl.ItemsSource 替代 WPF SkillListBox.Items.Add(ListBoxItem)
        private void RefreshSkillList()
        {
            SkillListBox.ItemsSource = null;
            SkillListBox.ItemsSource = _skills;
        }

        // 对照 WPF: private void SaveSkills()
        private void SaveSkills()
        {
            ForkPlusSettings.Default.AiDevSkillList = JsonConvert.SerializeObject(_skills);
        }

        // 对照 WPF: private void LoadSkills()
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

        // 对照 WPF: public void Initialize()
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

        // 对照 WPF: public void Save()
        public void Save()
        {
            if (_initialized)
            {
                SaveSkills();
            }
            SaveCurrentModel();
            ForkPlusSettings.Default.Save();
        }

        // 对照 WPF: private void ServiceUrlTextBox_TextChanged(...)
        private void ServiceUrlTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            ForkPlusSettings.Default.AiReviewServiceUrl = NormalizeUrl(ServiceUrlTextBox.Text);
            if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
            {
                RefreshModels();
            }
        }

        // 对照 WPF: private void ApiKeyTextBox_TextChanged(...)
        private void ApiKeyTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            ForkPlusSettings.Default.AiReviewApiKey = ApiKeyTextBox.Text ?? "";
            if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
            {
                RefreshModels();
            }
        }

        // 对照 WPF: private void AutoFetchModelsCheckBox_Changed(...)
        private void AutoFetchModelsCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            ForkPlusSettings.Default.AiReviewAutoFetchModels = AutoFetchModelsCheckBox.IsChecked.GetValueOrDefault();
            if (ForkPlusSettings.Default.AiReviewAutoFetchModels)
            {
                RefreshModels();
            }
        }

        // 对照 WPF: private void ModelComboBox_SelectionChanged(...)
        private void ModelComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_initialized)
            {
                SaveCurrentModel();
            }
        }

        // 对照 WPF: private void ModelComboBox_LostFocus(...)
        private void ModelComboBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SaveCurrentModel();
            }
        }

        // 对照 WPF: private void RefreshModelsButton_Click(...)
        private void RefreshModelsButton_Click(object? sender, RoutedEventArgs e)
        {
            RefreshModels();
        }

        // 对照 WPF: private void RetryCountTextBox_TextChanged(...)
        private void RetryCountTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            if (!int.TryParse(RetryCountTextBox.Text, out int value))
            {
                value = 3;
            }
            ForkPlusSettings.Default.AiReviewRetryCount = Math.Max(0, value);
        }

        // 对照 WPF: private void TimeoutTextBox_TextChanged(...)
        private void TimeoutTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            if (!int.TryParse(TimeoutTextBox.Text, out int value))
            {
                value = 300;
            }
            ForkPlusSettings.Default.AiReviewTimeoutSeconds = Math.Max(10, value);
        }

        // 对照 WPF: private void RefreshModels()
        // spike 版：移除 JobQueue + OpenAiService.ListModels（spike 不调用 AI 服务）
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
            // spike: WPF 用 JobQueue + OpenAiService.ListModels 异步刷新模型
            //        spike 版占位（spike 不调用 AI 服务）
            SetStatus(PreferencesLocalization.Current("Refresh models manually in production."));
        }

        // 对照 WPF: private void RefreshModelItems(string[] models, string selectedModel)
        private void RefreshModelItems(string[]? models, string? selectedModel)
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

        // 对照 WPF: private void SaveCurrentModel()
        private void SaveCurrentModel()
        {
            ForkPlusSettings.Default.AiReviewSelectedModel = (ModelComboBox.Text ?? "").Trim();
        }

        // 对照 WPF: private bool IsConfigured()
        private bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(ServiceUrlTextBox.Text) && !string.IsNullOrWhiteSpace(ApiKeyTextBox.Text);
        }

        // 对照 WPF: private void SetBusy(bool busy)
        private void SetBusy(bool busy)
        {
            BusyIndicator.IsVisible = busy;
            RefreshModelsButton.IsEnabled = !busy;
        }

        // 对照 WPF: private void SetStatus(string text)
        private void SetStatus(string text)
        {
            StatusTextBlock.Text = text;
        }

        // spike 版：NormalizeUrl（WPF 中也有此辅助方法）
        private static string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            return url.TrimEnd('/');
        }
    }
}
