using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 RescanRepositoriesWindow（真实迁移版，对照 WPF RescanRepositoriesWindow.xaml.cs 67 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RescanRepositoriesWindow.xaml.cs：
    //   - public partial class RescanRepositoriesWindow : ForkPlusDialogWindow
    //   - 字段: bool _isCloseAllowed = true
    //   - 构造函数 ():
    //     * DialogTitle = PreferencesLocalization.Current("Rescan repositories in source folder")
    //     * text = RepositoryManager.Instance.SourceDirs.Map(x => "'" + x + "'").Joined(", ")
    //     * DialogDescription = PreferencesLocalization.FormatCurrent(
    //         "Do you want to rescan repositories in '{0}'?", text)
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Rescan")
    //     * ResetDeletedCheckbox.IsChecked = false
    //   - OnSubmit (async void):
    //     * _isCloseAllowed = false
    //     * reset = ResetDeletedCheckbox.IsChecked.GetValueOrDefault()
    //     * DisableEditableControls()
    //     * SetStatus(InProgress, "Scanning repositories...")
    //     * await Task.Run(() => new RescanUserRepositoriesCommand().Execute(reset))
    //     * if (reset) ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = null
    //     * _isCloseAllowed = true
    //     * SetStatus(Success, "Done")
    //     * await Task.Delay(1500)
    //     * CloseWithOk()
    //   - OnClosing: if (!_isCloseAllowed) e.Cancel = true
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 版注入 string[]? sourceDirs 参数替代 RepositoryManager.Instance.SourceDirs
    //      （RepositoryManager 在 WPF 工程，Avalonia 工程不可访问；null 时用空数组）
    //   3. RescanUserRepositoriesCommand 在 WPF 工程 ForkPlus.UI.Commands，spike 版接受
    //      Action<bool>? onRescan 回调替代（调用方决定具体 rescan 逻辑）
    //   4. spike 基类不提供 DisableEditableControls，手动禁用 ResetDeletedCheckbox
    //   5. ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = null 步骤省略
    //      （WPF-only 设置，spike 不接入）
    //   6. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Translate/FormatCurrent
    //   7. OnClosing override → Closing += 事件（Avalonia 11 用事件而非 override，参数 CancelEventArgs）
    //   8. async void OnSubmit → spike 版保留 async void（基类 OnSubmit 非 async，子类重写时可 async）
    //   9. SourceDirs.Map(...).Joined(", ") → string.Join(", ", sourceDirs.Select(x => "'" + x + "'"))
    public partial class RescanRepositoriesWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: private bool _isCloseAllowed = true
        private bool _isCloseAllowed = true;

        // spike 版字段：替代 RepositoryManager.Instance.SourceDirs / RescanUserRepositoriesCommand
        private readonly string[] _sourceDirs;
        private readonly Action<bool>? _onRescan;

        // 构造函数签名与 WPF 不同：注入 sourceDirs + onRescan 回调
        // （WPF 无参构造内部访问 RepositoryManager.Instance + new RescanUserRepositoriesCommand()，
        //  Avalonia spike 版通过参数注入解耦）
        public RescanRepositoriesWindow(string[]? sourceDirs = null, Action<bool>? onRescan = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _sourceDirs = sourceDirs ?? Array.Empty<string>();
            _onRescan = onRescan;

            // 对照 WPF: DialogTitle = PreferencesLocalization.Current("Rescan repositories in source folder")
            string title = Translate("Rescan repositories in source folder");
            Title = title;
            DialogTitle = title;

            // 对照 WPF: string text = RepositoryManager.Instance.SourceDirs.Map(x => "'" + x + "'").Joined(", ")
            // Avalonia: LINQ Select + string.Join
            string sourceDirsText = string.Join(", ", System.Linq.Enumerable.Select(_sourceDirs, (string x) => "'" + x + "'"));

            // 对照 WPF: DialogDescription = PreferencesLocalization.FormatCurrent("Do you want to rescan repositories in '{0}'?", text)
            DialogDescription = FormatTranslate("Do you want to rescan repositories in '{0}'?", sourceDirsText);

            // 对照 WPF: SubmitButtonTitle = PreferencesLocalization.Current("Rescan")
            SubmitButtonTitle = Translate("Rescan");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: ResetDeletedCheckbox.IsChecked = false
            ResetDeletedCheckbox.IsChecked = false;

            // 对照 WPF: protected override void OnClosing(CancelEventArgs e)
            // Avalonia 11: Closing 事件（参数 CancelEventArgs，可取消）
            Closing += RescanRepositoriesWindow_Closing;
        }

        // 对照 WPF: protected override async void OnSubmit()
        protected override async void OnSubmit()
        {
            try
            {
                _isCloseAllowed = false;
                bool reset = ResetDeletedCheckbox.IsChecked.GetValueOrDefault();
                DisableEditableControls();
                SetStatus(ForkPlusDialogStatus.InProgress, "Scanning repositories...");

                // 对照 WPF: await Task.Run(delegate { new RescanUserRepositoriesCommand().Execute(reset); });
                // spike 版：调用注入的 onRescan 回调替代 RescanUserRepositoriesCommand
                await Task.Run(delegate
                {
                    _onRescan?.Invoke(reset);
                });

                // 对照 WPF: if (reset) ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = null;
                // spike 版省略此步（WPF-only 设置）

                _isCloseAllowed = true;
                SetStatus(ForkPlusDialogStatus.Success, "Done");
                await Task.Delay(1500);
                CloseWithOk();
            }
            catch (Exception ex)
            {
                Log.Error("OnSubmit failed", ex);
            }
        }

        // 对照 WPF: protected override void OnClosing(CancelEventArgs e)
        // Avalonia 11: Closing 事件（不是 override）
        private void RescanRepositoriesWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_isCloseAllowed)
            {
                e.Cancel = true;
            }
        }

        // spike 版：CheckBox IsCheckedChanged 事件处理（spike axaml 声明事件，spike 版无逻辑）
        // 对照 WPF: ResetDeletedCheckbox 无 Checked/Unchecked 事件，spike 版亦无逻辑，仅 stub 满足 axaml 引用
        public void ResetDeletedCheckbox_Changed(object? sender, RoutedEventArgs e)
        {
            // spike 版：无需响应（WPF 版仅在 OnSubmit 时读取 IsChecked）
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            ResetDeletedCheckbox.IsEnabled = false;
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
