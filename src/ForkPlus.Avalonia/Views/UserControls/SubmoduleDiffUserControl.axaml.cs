using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 SubmoduleDiffUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/SubmoduleDiffUserControl.xaml.cs（161 行）：
    //   - Update(RepositoryUserControl, SubmoduleDiffContent, ViewMode)
    //   - RevisionListView（自定义 RevisionListView，显示 submodule commit 列表）
    //   - RevisionHeaderUserControl（显示 src/dst revision 详情）
    //   - OpenSubmoduleButton / UpdateSubmoduleButton
    //   - BehindAheadTextBlock（↓/↑ ahead/behind 计数）
    //   - UncommittedFilesTextBlock（未提交文件数）
    //   - GetBehindAheadString：格式化 ahead/behind 计数
    //   - RefreshUncommittedChangesTextBlock：显示未提交文件列表
    //   - ApplicationThemeChanged 订阅
    //   - DiffControlContainer.IFileDiffControlSubControl 接口
    //   - PreferencesLocalization → ServiceLocator.Localization
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF RepositoryUserControl → object（spike 占位）
    //   - WPF RevisionListView + RevisionsDataSource → 不迁移（spike 用 TextBlock 占位）
    //   - WPF RevisionHeaderUserControl → 不迁移（spike 用 TextBlock 占位）
    //   - WPF SubmoduleDiffContent → task spec 用 Submodule
    //   - WPF Dispatcher.Invoke → Dispatcher.UIThread.Post（Avalonia API 规则）
    //   - WPF Visibility.Collapsed/Visible → IsVisible=false/true
    //   - WPF Image.Show()/Hide()/Collapse() → IsVisible=true/false/false
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / SetSubmodule(Submodule) / Refresh()
    //
    // spike 简化：
    //   - Initialize(object) 方法（task spec 关键 API）
    //   - SetSubmodule(Submodule) 方法（task spec 关键 API，替代 WPF Update 的 SubmoduleDiffContent）
    //   - Refresh() 方法（task spec 关键 API）
    //   - 显示 submodule 名称 + ahead/behind + 未提交文件数
    //   - Open/Update 按钮触发事件回调（调用方处理实际 git 命令）
    public partial class SubmoduleDiffUserControl : UserControl
    {
        // ===== 公共事件（spike 新增）=====
        // 打开 submodule 事件（对照 WPF: OpenSubmoduleButton_Click → Commands.OpenSubmodule）
        public event EventHandler<Submodule> OpenSubmoduleClicked;

        // 更新 submodule 事件（对照 WPF: UpdateSubmoduleButton_Click → UpdateSubmodulesGitCommand）
        public event EventHandler<Submodule> UpdateSubmoduleClicked;

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // 当前 submodule（task spec: SetSubmodule 数据入口）
        public Submodule Submodule { get; private set; }

        // ahead/behind 计数（对照 WPF: content.BehindAheadCount）
        private int _behindCount;
        private int _aheadCount;
        // 未提交文件路径（对照 WPF: content.ChangedFilePaths）
        private string[] _changedFilePaths;

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public SubmoduleDiffUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();

            // 对照 WPF: UpdateSubmoduleButton.Collapse()
            if (UpdateSubmoduleButton != null)
            {
                UpdateSubmoduleButton.IsVisible = false;
            }
        }

        // ===== Initialize(object)（task spec 关键 API）=====
        // 对照 WPF: Update 方法内注入 _repositoryUserControl
        // spike 版：Initialize 方法注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetSubmodule(Submodule)（task spec 关键 API）=====
        // 对照 WPF: Update(RepositoryUserControl, SubmoduleDiffContent, ViewMode)
        //   WPF 版用 SubmoduleDiffContent（含 SrcSha/DstSha/BehindAheadCount/ChangedFilePaths 等）
        //   spike 版用 Submodule + SetBehindAhead/SetChangedFiles 辅助方法
        public void SetSubmodule(Submodule submodule)
        {
            Submodule = submodule;
            if (submodule == null)
            {
                Clear();
                return;
            }

            // 对照 WPF: TitleTextBlock.Text = FormatCurrent("Submodule '{0}' changed", content.Submodule.FriendlyName)
            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = Translate($"Submodule '{submodule.FriendlyName}' changed");
            }
        }

        // ===== SetBehindAhead(int, int)（spike 新增，设置 ahead/behind 计数）=====
        // 对照 WPF: BehindAheadTextBlock.Text = GetBehindAheadString(content.BehindAheadCount)
        public void SetBehindAhead(int behind, int ahead)
        {
            _behindCount = behind;
            _aheadCount = ahead;
            if (BehindAheadTextBlock != null)
            {
                BehindAheadTextBlock.Text = GetBehindAheadString(behind, ahead);
            }
        }

        // ===== SetChangedFiles(string[])（spike 新增，设置未提交文件列表）=====
        // 对照 WPF: RefreshUncommittedChangesTextBlock(content.ChangedFilePaths)
        public void SetChangedFiles(string[] changedFilePaths)
        {
            _changedFilePaths = changedFilePaths ?? Array.Empty<string>();
            RefreshUncommittedChangesTextBlock(_changedFilePaths);
        }

        // ===== ShowUpdateButton(bool)（spike 新增，显示/隐藏 Update 按钮）=====
        // 对照 WPF: UpdateSubmoduleButton.Show() / Collapse()
        public void ShowUpdateButton(bool show)
        {
            if (UpdateSubmoduleButton != null)
            {
                UpdateSubmoduleButton.IsVisible = show;
            }
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: 无独立 Refresh 方法（Update 即刷新）
        // spike 版：Refresh 方法重新渲染当前 submodule 状态
        public void Refresh()
        {
            if (Submodule == null) return;
            // 重新设置标题
            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = Translate($"Submodule '{Submodule.FriendlyName}' changed");
            }
            // 重新设置 ahead/behind
            if (BehindAheadTextBlock != null)
            {
                BehindAheadTextBlock.Text = GetBehindAheadString(_behindCount, _aheadCount);
            }
            // 重新设置未提交文件
            RefreshUncommittedChangesTextBlock(_changedFilePaths);
        }

        // ===== Clear()（spike 新增，清空显示）=====
        public void Clear()
        {
            Submodule = null;
            _behindCount = 0;
            _aheadCount = 0;
            _changedFilePaths = null;
            if (TitleTextBlock != null) TitleTextBlock.Text = "Submodule";
            if (BehindAheadTextBlock != null) BehindAheadTextBlock.Text = "";
            if (UncommittedFilesTextBlock != null) UncommittedFilesTextBlock.IsVisible = false;
            if (UpdateSubmoduleButton != null) UpdateSubmoduleButton.IsVisible = false;
        }

        // ===== OpenSubmoduleButton_Click（对照 WPF）=====
        // WPF: Commands.OpenSubmodule.Execute(repositoryUserControl, parentGitModule, submodule)
        // spike: 触发 OpenSubmoduleClicked 事件
        private void OpenSubmoduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Submodule != null)
            {
                OpenSubmoduleClicked?.Invoke(this, Submodule);
            }
        }

        // ===== UpdateSubmoduleButton_Click（对照 WPF）=====
        // WPF: JobQueue.Add + UpdateSubmodulesGitCommand.Execute
        // spike: 触发 UpdateSubmoduleClicked 事件
        private void UpdateSubmoduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Submodule != null)
            {
                UpdateSubmoduleClicked?.Invoke(this, Submodule);
            }
        }

        // ===== GetBehindAheadString（对照 WPF，完整迁移）=====
        // WPF: behindAheadCount.Left（↓） / Right（↑）
        // spike: behind / ahead 参数
        private static string GetBehindAheadString(int behind, int ahead)
        {
            if (ahead > 0)
            {
                if (behind > 0)
                {
                    return $"{behind}↓ {ahead}↑";
                }
                return $"{ahead}↑";
            }
            if (behind > 0)
            {
                return $"{behind}↓";
            }
            return "";
        }

        // ===== RefreshUncommittedChangesTextBlock（对照 WPF，完整迁移）=====
        // WPF: 显示 "{0} uncommitted file(s)" + ToolTip 文件列表
        // spike: IsVisible=true/false 替代 Show()/Collapse()
        private void RefreshUncommittedChangesTextBlock(string[] changedFilePaths)
        {
            if (UncommittedFilesTextBlock == null) return;
            int count = changedFilePaths?.Length ?? 0;
            if (count > 0)
            {
                // 对照 WPF: FormatCurrent("{0} uncommitted file(s)", num)
                string text = count == 1
                    ? Translate($"{count} uncommitted file")
                    : Translate($"{count} uncommitted files");
                string tooltip = text + ":\n" + string.Join("\n", changedFilePaths);
                UncommittedFilesTextBlock.Text = text;
                // 对照 WPF: UncommittedFilesTextBlock.Show()
                UncommittedFilesTextBlock.IsVisible = true;
                ToolTip.SetTip(UncommittedFilesTextBlock, tooltip);
            }
            else
            {
                UncommittedFilesTextBlock.Text = "";
                // 对照 WPF: UncommittedFilesTextBlock.Collapse()
                UncommittedFilesTextBlock.IsVisible = false;
            }
        }

        // ===== Translate(string)（对照 WPF: PreferencesLocalization）=====
        // WPF: PreferencesLocalization.Current(text) / FormatCurrent
        // spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
