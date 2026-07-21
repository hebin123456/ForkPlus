using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.41b：Avalonia 版 AddGitignoreTemplateWindow（真实迁移版，对照 WPF AddGitignoreTemplateWindow.xaml.cs 357 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AddGitignoreTemplateWindow.xaml.cs：
    //   - public partial class AddGitignoreTemplateWindow : ForkPlusDialogWindow
    //   - 静态数据: TemplateMarkers / AlwaysEnabledTemplates / TemplateGroups
    //   - 字段: RepositoryUserControl _repositoryUserControl / string[] _untrackedFiles
    //          / List<(string Name, string Content)> _templates
    //          / List<string> _selectedTemplateNames / Dictionary<string, CheckBox> _checkboxes
    //   - 构造函数 (RepositoryUserControl repositoryUserControl, string[] untrackedFiles)
    //   - IsSubmitAllowed: _selectedTemplateNames.Count > 0
    //   - OnSubmit: File.WriteAllText(gitModule.MakePath(".gitignore"), CombinedSelectedContent()) → CloseWithOk()
    //   - LoadTemplates: 从 ForkPlus.Assets.gitignore.txt 嵌入资源解析模板分块
    //   - BuildTemplateList: 按 TemplateGroups 分组渲染 CheckBox 到 TemplateListPanel
    //   - PreselectTemplates: 根据 _untrackedFiles 后缀 + 路径组件预选模板
    //   - Checkbox_Toggled: 维护 _selectedTemplateNames + UpdatePreview + UpdateSubmitButton
    //   - UpdatePreview: PreviewCodeEditor.Text = CombinedSelectedContent()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //      （本对话框 OnSubmit 不执行 git 命令，仅写文件后 CloseWithOk，
    //       onCompleted 回调签名沿用其它对话框约定，传 null GitCommandResult 表示无 git 操作）
    //   3. spike 基类不提供 GetCommandPreview（本对话框无命令预览）
    //   4. spike 基类不提供 DisableEditableControls（本对话框无可编辑文本控件）
    //   5. Description 用纯文本替代 WPF Run+Hyperlink+Run（Avalonia TextBlock.Inlines
    //      API 与 WPF 差异较大，spike 简化）
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. CodeEditor（AvaloniaEdit.TextEditor 子类）替代 WPF ICSharpCode.AvalonEdit.TextEditor
    public partial class AddGitignoreTemplateWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: TemplateMarkers / AlwaysEnabledTemplates / TemplateGroups
        private static readonly Dictionary<string, (string[] Extensions, string[] PathComponents)> TemplateMarkers =
            new Dictionary<string, (string[], string[])>
            {
                { "C", (new string[1] { ".c" }, new string[0]) },
                { "C++", (new string[6] { ".cpp", ".cc", ".cxx", ".hpp", ".hxx", ".hh" }, new string[0]) },
                { "C#", (new string[2] { ".cs", ".csproj" }, new string[0]) },
                { "Dart", (new string[1] { ".dart" }, new string[0]) },
                { "Go", (new string[1] { ".go" }, new string[0]) },
                { "Java", (new string[1] { ".java" }, new string[0]) },
                { "Kotlin", (new string[2] { ".kt", ".kts" }, new string[0]) },
                { "Node", (new string[4] { ".js", ".ts", ".jsx", ".tsx" }, new string[1] { "node_modules" }) },
                { "Objective-C", (new string[3] { ".m", ".mm", ".h" }, new string[0]) },
                { "Python", (new string[2] { ".py", ".pyc" }, new string[0]) },
                { "Ruby", (new string[2] { ".rb", ".gemspec" }, new string[0]) },
                { "Rust", (new string[1] { ".rs" }, new string[0]) },
                { "Scala", (new string[2] { ".scala", ".sc" }, new string[0]) },
                { "Swift", (new string[2] { ".swift", ".xib" }, new string[0]) },
                { "Android", (new string[1] { ".gradle" }, new string[0]) },
                { "Unity", (new string[3] { ".unity", ".prefab", ".asset" }, new string[0]) },
                { "JetBrains", (new string[1] { ".iws" }, new string[1] { ".idea" }) },
                { "VisualStudio", (new string[5] { ".sln", ".csproj", ".vbproj", ".vcproj", ".vcxproj" }, new string[1] { ".vs" }) },
                { "VS Code", (new string[1] { ".vsix" }, new string[1] { ".vscode" }) },
                { "Xcode", (new string[4] { ".xcodeproj", ".xcworkspace", ".xib", ".xcuserstate" }, new string[1] { "xcuserdata" }) }
            };

        private static readonly HashSet<string> AlwaysEnabledTemplates = new HashSet<string> { "Windows" };

        private static readonly (string Group, HashSet<string> Names)[] TemplateGroups = new (string, HashSet<string>)[3]
        {
            ("OS", new HashSet<string> { "Linux", "macOS", "Windows" }),
            ("IDE", new HashSet<string> { "Android", "JetBrains", "VisualStudio", "VS Code", "Xcode", "Unity" }),
            ("Language", new HashSet<string>
            {
                "C", "C++", "C#", "Dart", "Go", "Java", "Kotlin", "Node", "Objective-C", "Python",
                "Ruby", "Rust", "Scala", "Swift"
            })
        };

        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult> _onCompleted;
        private readonly string[] _untrackedFiles;

        private List<(string Name, string Content)> _templates = new List<(string, string)>();
        private List<string> _selectedTemplateNames = new List<string>();
        private readonly Dictionary<string, CheckBox> _checkboxes = new Dictionary<string, CheckBox>();

        // 构造函数签名与 WPF 不同：RepositoryUserControl 替换为 GitModule + Action 回调
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public AddGitignoreTemplateWindow(
            GitModule gitModule,
            string[] untrackedFiles,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule;
            _untrackedFiles = untrackedFiles ?? Array.Empty<string>();
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle = PreferencesLocalization.Current("Add .gitignore Template");
            DialogTitle = Translate("Add .gitignore Template");
            // 对照 WPF: SubmitButtonTitle = PreferencesLocalization.Current("Add");
            SubmitButtonTitle = Translate("Add");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Add .gitignore Template");

            // 对照 WPF: DescriptionTextBlock.Inlines.Add("Choose " + Hyperlink ".gitignore" + " template for your project")
            // Avalonia spike 版：用纯文本替代（Avalonia TextBlock.Inlines API 与 WPF 差异较大）
            DialogDescription = Translate("Choose .gitignore template for your project");

            LoadTemplates();
            BuildTemplateList();
            PreselectTemplates();
            UpdatePreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed => _selectedTemplateNames.Count > 0;

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (_gitModule == null)
            {
                return;
            }
            string contents = CombinedSelectedContent();
            try
            {
                File.WriteAllText(_gitModule.MakePath(".gitignore"), contents);
            }
            catch (Exception ex)
            {
                SetStatus(ForkPlusDialogStatus.Error, ex.Message);
                return;
            }
            try
            {
                // 本对话框不执行 git 命令，传 null 表示无 git 操作（与其它对话框 onCompleted 约定一致）
                _onCompleted?.Invoke(null);
            }
            catch (Exception ex)
            {
                Log.Error("AddGitignoreTemplateWindow onCompleted callback failed", ex);
            }
            CloseWithOk();
        }

        // 对照 WPF: LoadTemplates（从嵌入资源 ForkPlus.Assets.gitignore.txt 解析模板分块）
        // 注意：Avalonia 工程未包含此嵌入资源（仅在 WPF 工程 src/ForkPlus 中），
        // 运行时 stream 为 null 时直接返回，_templates 为空，对话框结构正常。
        private void LoadTemplates()
        {
            string text;
            try
            {
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string name = "ForkPlus.Assets.gitignore.txt";
                using Stream stream = executingAssembly.GetManifestResourceStream(name);
                if (stream == null)
                {
                    Log.Warn($"Embedded resource '{name}' not found in assembly {executingAssembly.GetName().Name}");
                    return;
                }
                using StreamReader streamReader = new StreamReader(stream);
                text = streamReader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load gitignore templates", ex);
                return;
            }
            int startIndex = 0;
            while (true)
            {
                int num = text.IndexOf("### ", startIndex, StringComparison.Ordinal);
                if (num >= 0)
                {
                    int num2 = num + 4;
                    int num3 = text.IndexOf('\n', num2);
                    if (num3 < 0)
                    {
                        num3 = text.Length;
                    }
                    string item = text.Substring(num2, num3 - num2).Trim();
                    int num4 = text.IndexOf("\n### ", num3, StringComparison.Ordinal);
                    int num5 = (num4 >= 0) ? num4 : text.Length;
                    string item2 = text.Substring(num, num5 - num);
                    _templates.Add((item, item2));
                    startIndex = num5;
                    continue;
                }
                break;
            }
        }

        // 对照 WPF: BuildTemplateList（按 TemplateGroups 分组渲染 CheckBox 到 TemplateListPanel）
        private void BuildTemplateList()
        {
            // 建立 模板名 → 所属分组 的映射
            Dictionary<string, string> nameToGroup = new Dictionary<string, string>();
            foreach (var group in TemplateGroups)
            {
                foreach (var name in group.Names)
                {
                    nameToGroup[name] = group.Group;
                }
            }

            // 按分组收集模板索引
            (string Group, List<int> Indices)[] grouped =
                TemplateGroups.Select(g => (Group: g.Group, Indices: new List<int>())).ToArray();
            for (int j = 0; j < _templates.Count; j++)
            {
                if (nameToGroup.TryGetValue(_templates[j].Name, out var groupName))
                {
                    int idx = Array.FindIndex(grouped, g => g.Group == groupName);
                    if (idx >= 0)
                    {
                        grouped[idx].Indices.Add(j);
                    }
                }
            }

            // 渲染到 TemplateListPanel
            foreach (var (group, indices) in grouped)
            {
                // 分组标题
                TextBlock headerTextBlock = new TextBlock
                {
                    Text = group,
                    FontWeight = FontWeight.Bold,
                    FontSize = 11,
                    Margin = new Thickness(4, 6, 0, 2)
                };
                TemplateListPanel.Children.Add(headerTextBlock);

                // 分组下的 CheckBox
                foreach (int index in indices)
                {
                    string name = _templates[index].Name;
                    CheckBox checkBox = new CheckBox
                    {
                        Content = name,
                        FontSize = 13,
                        Margin = new Thickness(4, 2, 0, 2),
                        Tag = index
                    };
                    // 对照 WPF: checkBox.Checked += Checkbox_Toggled; checkBox.Unchecked += Checkbox_Toggled;
                    // Avalonia 用 IsCheckedChanged 事件
                    checkBox.IsCheckedChanged += Checkbox_Toggled;
                    _checkboxes[name] = checkBox;
                    TemplateListPanel.Children.Add(checkBox);
                }
            }
        }

        // 对照 WPF: Checkbox_Toggled（Checked/Unchecked 事件 → IsCheckedChanged）
        private void Checkbox_Toggled(object? sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox checkBox))
            {
                return;
            }
            int index = (int)checkBox.Tag;
            string item = _templates[index].Name;
            if (checkBox.IsChecked.GetValueOrDefault())
            {
                if (!_selectedTemplateNames.Contains(item))
                {
                    _selectedTemplateNames.Add(item);
                }
            }
            else
            {
                _selectedTemplateNames.Remove(item);
            }
            UpdatePreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: PreselectTemplates（根据 _untrackedFiles 后缀 + 路径组件预选模板）
        private void PreselectTemplates()
        {
            HashSet<string> fileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> pathComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string text in _untrackedFiles)
            {
                int num = text.LastIndexOf('.');
                if (num >= 0)
                {
                    fileExtensions.Add(text.Substring(num));
                }
                string[] array = text.Split('/');
                for (int j = 0; j < array.Length - 1; j++)
                {
                    pathComponents.Add(array[j]);
                }
            }
            HashSet<string> knownNames = new HashSet<string>(_templates.Select(t => t.Name));

            // AlwaysEnabledTemplates 直接预选
            foreach (string alwaysEnabledTemplate in AlwaysEnabledTemplates)
            {
                if (knownNames.Contains(alwaysEnabledTemplate) && !_selectedTemplateNames.Contains(alwaysEnabledTemplate))
                {
                    _selectedTemplateNames.Add(alwaysEnabledTemplate);
                }
            }
            // TemplateMarkers 匹配则预选
            foreach (var kvp in TemplateMarkers)
            {
                string key = kvp.Key;
                var (extensions, pcs) = kvp.Value;
                if (knownNames.Contains(key)
                    && !_selectedTemplateNames.Contains(key)
                    && (extensions.Any(ext => fileExtensions.Contains(ext)) || pcs.Any(pc => pathComponents.Contains(pc))))
                {
                    _selectedTemplateNames.Add(key);
                }
            }

            // 同步 checkbox 状态
            foreach (string selectedTemplateName in _selectedTemplateNames)
            {
                if (_checkboxes.TryGetValue(selectedTemplateName, out var cb))
                {
                    cb.IsChecked = true;
                }
            }
        }

        // 对照 WPF: CombinedSelectedContent
        private string CombinedSelectedContent()
        {
            HashSet<string> hashSet = new HashSet<string>(_selectedTemplateNames);
            List<string> list = new List<string>();
            foreach (var template in _templates)
            {
                if (hashSet.Contains(template.Name))
                {
                    list.Add(template.Content);
                }
            }
            return string.Join("\n\n", list);
        }

        // 对照 WPF: UpdatePreview
        private void UpdatePreview()
        {
            PreviewCodeEditor.Text = CombinedSelectedContent();
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
