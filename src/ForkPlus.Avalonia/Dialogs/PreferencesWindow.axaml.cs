using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 PreferencesWindow（真实迁移版，对照 WPF PreferencesWindow.xaml.cs 117 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PreferencesWindow.xaml.cs：
    //   - public partial class PreferencesWindow : ForkPlusDialogWindow, ILocalizableControl
    //   - 字段：bool _initialised / string _appliedLanguage /
    //     Dictionary<TabItem, string> _localizedTabLanguages
    //   - ApplyAutomaticLocalization = false
    //   - 构造函数：
    //     * ShowLogo=false
    //     * ShowCancelButton=false
    //     * SubmitButtonTitle=PreferencesLocalization.Current("Close")
    //     * SizeToContent=WidthAndHeight
    //     * Initialize() 调用各 UserControl 的 Initialize / Save 方法
    //       （GeneralUserControl / CommitUserControl / AiReviewPreferencesUserControl /
    //       IntegrationUserControl / GitUserControl / CustomCommandsUserControl）
    //   - OnKeyDown: ESC → OnCancel + e.Handled=true
    //   - OnSubmit: 调用各 UserControl 的 Save() + ForkPlusSettings.Default.Save()
    //   - Initialize: 调用各 UserControl 的 Initialize(this) / InitializeGlobal(this)
    //   - ApplyLocalization: 翻译 Title + SubmitButtonTitle + 6 个 TabItem.Header
    //   - TabControl_SelectionChanged: SetStatus(None, "") + ApplySelectedTabLocalization
    //   - ApplySelectedTabLocalization: 对选中 Tab 的 Content 调 PreferencesLocalization.Apply
    //     （IntegrationUserControl 还需 integrationUserControl.ApplyLocalization()）
    //   - Translate: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 构造函数注入 (Action? onSavePreferences) 替代直接调用 6 个子 UserControl 的 Save
    //   3. spike 版用 TabControl + 6 个空 TabItem（General / Commit / AI Enhancement / Git /
    //      Integration / Custom Commands），每个 Tab 内容用占位 TextBlock "Configure ..."
    //      （实际 UserControl 留待迁移）
    //   4. spike 版 OnSubmit 调用注入的 onSavePreferences?.Invoke() +
    //      ForkPlusSettings.Default.Save()
    //   5. spike 版完整保留 ApplyLocalization / TabControl_SelectionChanged /
    //      ApplySelectedTabLocalization 逻辑
    //   6. spike 版省略 ApplyAutomaticLocalization=false（基类无此属性，spike 不接入）
    //   7. spike 版省略 ILocalizableControl 接口（依赖 PreferencesLocalization.Apply，
    //      spike 不迁移 Apply，子类自己 Translate）
    //   8. spike 版省略 IntegrationUserControl.ApplyLocalization()（依赖 IntegrationUserControl，
    //      spike 不迁移）
    //   9. SizeToContent=WidthAndHeight → Avalonia SizeToContent="Width"（Height 不与 TabControl
    //      兼容，spike 版固定 Height=560）
    //  10. OnKeyDown ESC → OnCancel（spike 版用 KeyDown 事件，Key.Escape）
    //  11. SelectionChangedEventArgs → Avalonia.Controls.SelectionChangedEventArgs
    //  12. PreferencesLocalization → ServiceLocator.Localization.Translate
    public partial class PreferencesWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: private bool _initialised;
        private bool _initialised;

        // 对照 WPF: private string _appliedLanguage;
        private string? _appliedLanguage;

        // 对照 WPF: private readonly Dictionary<TabItem, string> _localizedTabLanguages
        private readonly Dictionary<TabItem, string> _localizedTabLanguages = new Dictionary<TabItem, string>();

        private readonly Action? _onSavePreferences;

        // 构造函数签名与 WPF 不同：注入 Action? onSavePreferences 回调替代 6 个子 UserControl 的 Save 调用
        public PreferencesWindow(Action? onSavePreferences = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _onSavePreferences = onSavePreferences;

            // 对照 WPF: base.ShowLogo = false;
            // spike 版基类不提供 ShowLogo，子类 axaml 中不放 Logo Image 即等价

            // 对照 WPF: base.ShowCancelButton = false;
            ShowCancelButton = false;

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
            SubmitButtonTitle = Translate("Close");

            // 对照 WPF: base.SizeToContent = SizeToContent.WidthAndHeight;
            // spike 版：axaml 中已设置 SizeToContent="Width" + 固定 Height=560

            // 对照 WPF: Initialize();
            Initialize();
        }

        // 对照 WPF: protected override void OnKeyDown(KeyEventArgs e)
        // spike 版：基类 ForkPlusDialogWindow 已在 KeyDown 中处理 ESC（仅当 ShowFooter && ShowCancelButton）
        // spike 版 ShowCancelButton=false，所以基类不会自动触发 OnCancel，这里手动处理
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

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            base.OnSubmit();

            // 对照 WPF: IntegrationUserControl.Save();
            //           AiReviewPreferencesUserControl.Save();
            //           CustomCommandsUserControl.Save();
            //           ForkPlusSettings.Default.Save();
            // spike 版：通过回调通知调用方保存 6 个子 UserControl 的设置
            try { _onSavePreferences?.Invoke(); }
            catch (Exception ex) { Log.Error("PreferencesWindow onSavePreferences callback failed", ex); }

            ForkPlusSettings.Default.Save();
        }

        // 对照 WPF: private void Initialize()
        // spike 版：6 个子 UserControl 未迁移，方法体仅设置 _initialised=true + ApplyLocalization
        private void Initialize()
        {
            // GeneralUserControl.Initialize(this);
            // CommitUserControl.Initialize();
            // AiReviewPreferencesUserControl.Initialize();
            // IntegrationUserControl.Initialize(this);
            // GitUserControl.Initialize(this);
            // CustomCommandsUserControl.InitializeGlobal(this);
            ApplyLocalization();
            _initialised = true;
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            // 对照 WPF: string language = ForkPlusSettings.Default.UiLanguage;
            string language = ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage;
            if (_appliedLanguage == language)
            {
                ApplySelectedTabLocalization(language);
                return;
            }
            _appliedLanguage = language;
            _localizedTabLanguages.Clear();

            // 对照 WPF: Title = PreferencesLocalization.Translate("Preferences", language);
            Title = Translate("Preferences");

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Translate("Close", language);
            SubmitButtonTitle = Translate("Close");

            // 对照 WPF: 6 个 TabItem.Header 翻译
            // spike 版：TabItem.Header 是 object 类型，Avalonia 直接赋字符串即可
            GeneralTabItem.Header = Translate("General");
            CommitTabItem.Header = Translate("Commit");
            AiReviewTabItem.Header = Translate("AI Enhancement");
            GitTabItem.Header = Translate("Git");
            IntegrationTabItem.Header = Translate("Integration");
            CustomCommandsTabItem.Header = Translate("Custom Commands");

            ApplySelectedTabLocalization(language);
        }

        // 对照 WPF: private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        public void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // 对照 WPF: if (_initialised && e.AddedItems.Count >= 1 && e.AddedItems[0] is TabItem)
            if (_initialised
                && e.AddedItems != null
                && e.AddedItems.Count >= 1
                && e.AddedItems[0] is TabItem)
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                ApplySelectedTabLocalization(ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage);
            }
        }

        // 对照 WPF: private void ApplySelectedTabLocalization(string language)
        private void ApplySelectedTabLocalization(string language)
        {
            // 对照 WPF: if (!(PreferencesTabControl.SelectedItem is TabItem selectedTab)) return;
            if (!(PreferencesTabControl.SelectedItem is TabItem selectedTab)) return;

            // 对照 WPF: if (_localizedTabLanguages.TryGetValue(selectedTab, out string appliedLanguage) && appliedLanguage == language) return;
            if (_localizedTabLanguages.TryGetValue(selectedTab, out string? appliedLanguage) && appliedLanguage == language)
            {
                return;
            }

            // 对照 WPF: if (selectedTab.Content is DependencyObject content)
            //           { PreferencesLocalization.Apply(content, language); ... }
            // spike 版：spike 不迁移 PreferencesLocalization.Apply，6 个 TabItem 的 Content 是
            // 占位 TextBlock（axaml 中已硬编码英文字符串），spike 版仅记录 _localizedTabLanguages
            _localizedTabLanguages[selectedTab] = language;
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
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
