// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/PreferencesLocalization.cs（597 行）：
//   - internal static class PreferencesLocalization
//   - 语言常量：English / SimplifiedChinese / TraditionalChinese / Japanese / Korean /
//     French / German / Spanish
//   - DependencyProperty：OriginalText / OriginalHeader / OriginalContent / OriginalPlaceholder /
//     OriginalToolTip / OriginalTitle（WPF 附加属性，用于 Apply 递归翻译时保存原文）
//   - BuiltInLanguageNames / ExternalLanguages / ExternalDictionaries / ExternalLanguageNames
//   - TranslationCache + TranslationCacheLock（带大小上限的翻译缓存）
//   - LoadedLanguage / LanguageOption 嵌套类
//   - Apply(DependencyObject, string) / ApplyCurrent(DependencyObject)：
//     递归遍历逻辑树，对 TextBlock/HeaderedContentControl/ContentControl/Window/
//     PlaceholderTextBox/FrameworkElement.ToolTip/ToolbarButton/ToolbarDropDownButton
//     翻译 Text/Header/Content/Title/Placeholder/ToolTip
//   - Translate(text, language) / Current(text) / FormatCurrent / MenuHeader / FormatMenuHeader：
//     纯字符串方法，委托到 ServiceLocator.Localization（Phase 0.3a 已迁入 Core）
//   - GetLanguages() / LanguageSortOrder / LoadExternalLanguages / LanguageFilePaths /
//     LanguageDirectories / DecodeTranslations / ApplyRecursive / ApplyElementCore /
//     HasBinding / GetOriginal / Translate(text, dictionary) / TranslatePattern / ReplacePattern
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF DependencyProperty 附加属性 → spike 移除（Avalonia 无 WPF DependencyProperty，
//      Avalonia 用 StyledProperty/AvaloniaProperty，Apply 递归翻译逻辑依赖 WPF 逻辑树遍历，
//      spike 阶段不迁移 Apply 方法，调用方直接用 ServiceLocator.Localization）
//   2. WPF LogicalTreeHelper.GetChildren → spike 移除（Avalonia 用 Visual.LogicalChildren，
//      Apply 递归翻译逻辑 spike 不迁移）
//   3. WPF TextBlock/HeaderedContentControl/ContentControl/Window/PlaceholderTextBox/
//     ToolbarButton/ToolbarDropDownButton → spike 移除（Apply 递归翻译逻辑 spike 不迁移）
//   4. WPF BindingOperations.GetBindingExpressionBase → spike 移除
//   5. 纯字符串方法（Translate/Current/FormatCurrent/MenuHeader/FormatMenuHeader）→
//      委托到 ServiceLocator.Localization（task spec 关键 API：
//      PreferencesLocalization → ServiceLocator.Localization）
//   6. 语言常量 + GetLanguages() → 保留（纯 C#，GetLanguages 委托到 ServiceLocator
//      或返回内置列表，spike 阶段返回内置列表）
//   7. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
//
// spike 简化（task spec：PreferencesLocalization → ServiceLocator.Localization）：
//   - Translate / Current / FormatCurrent / MenuHeader / FormatMenuHeader → 委托到
//     ServiceLocator.Localization（若 null 返回原文）
//   - 语言常量保留（其他文件可能引用）
//   - GetLanguages → 返回内置语言列表（spike 不加载外部 JSON 文件）
//   - Apply / ApplyCurrent / ApplyElement → spike 不迁移（WPF 逻辑树遍历，
//     Avalonia 用 axaml binding + Design.Text 模式替代）
using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    internal static class PreferencesLocalization
    {
        public const string English = "en";
        public const string SimplifiedChinese = "zh-Hans";
        public const string TraditionalChinese = "zh-Hant";
        public const string Japanese = "ja-JP";
        public const string Korean = "ko-KR";
        public const string French = "fr-FR";
        public const string German = "de-DE";
        public const string Spanish = "es-ES";

        private static readonly Dictionary<string, string> BuiltInLanguageNames = new Dictionary<string, string>
        {
            { English, "English" },
            { SimplifiedChinese, "简体中文" },
            { TraditionalChinese, "繁體中文" },
            { Japanese, "日本語" },
            { Korean, "한국어" },
            { French, "Français" },
            { German, "Deutsch" },
            { Spanish, "Español" }
        };

        public sealed class LanguageOption
        {
            public string Code { get; }

            public string DisplayName { get; }

            public LanguageOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }
        }

        // 对照 WPF: public static string Translate(string text, string language)
        // spike 版：委托到 ServiceLocator.Localization，null 时回退返回原文
        public static string Translate(string text, string language)
        {
            ILocalizationService loc = ServiceLocator.Localization;
            return loc != null ? loc.Translate(text, language) : text;
        }

        // 对照 WPF: public static string Current(string text)
        // spike 版：委托到 ServiceLocator.Localization，null 时回退返回原文
        public static string Current(string text)
        {
            ILocalizationService loc = ServiceLocator.Localization;
            return loc != null ? loc.Current(text) : text;
        }

        // 对照 WPF: public static string FormatCurrent(string text, params object[] args)
        // spike 版：委托到 ServiceLocator.Localization，null 时回退 string.Format(Current(text), args)
        public static string FormatCurrent(string text, params object[] args)
        {
            ILocalizationService loc = ServiceLocator.Localization;
            return loc != null ? loc.FormatCurrent(text, args) : string.Format(Current(text), args);
        }

        // 对照 WPF: public static string MenuHeader(string text)
        // spike 版：委托到 ServiceLocator.Localization，null 时回退 Current(text).Replace("_", "__")
        public static string MenuHeader(string text)
        {
            ILocalizationService loc = ServiceLocator.Localization;
            return loc != null ? loc.MenuHeader(text) : Current(text).Replace("_", "__");
        }

        // 对照 WPF: public static string FormatMenuHeader(string text, params object[] args)
        // spike 版：委托到 ServiceLocator.Localization，null 时回退 FormatCurrent(text, args).Replace("_", "__")
        public static string FormatMenuHeader(string text, params object[] args)
        {
            ILocalizationService loc = ServiceLocator.Localization;
            return loc != null ? loc.FormatMenuHeader(text, args) : FormatCurrent(text, args).Replace("_", "__");
        }

        // 对照 WPF: public static LanguageOption[] GetLanguages()
        // spike 版：返回内置语言列表（不加载外部 JSON 文件，ExternalLanguages 逻辑 spike 不迁移）
        public static LanguageOption[] GetLanguages()
        {
            return BuiltInLanguageNames
                .Select(item => new LanguageOption(item.Key, item.Value))
                .OrderBy(option => LanguageSortOrder(option.Code))
                .ThenBy(option => option.DisplayName)
                .ToArray();
        }

        private static int LanguageSortOrder(string language)
        {
            if (language == English) return 0;
            if (language == SimplifiedChinese) return 1;
            if (language == TraditionalChinese) return 2;
            if (language == Japanese) return 3;
            if (language == Korean) return 4;
            if (language == French) return 5;
            if (language == German) return 6;
            if (language == Spanish) return 7;
            return 10;
        }
    }
}
