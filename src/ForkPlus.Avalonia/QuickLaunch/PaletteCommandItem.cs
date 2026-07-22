// PaletteCommandItem.cs：命令面板命令条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/PaletteCommandItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class PaletteCommandItem : CommandProviderItem
//   - override ImageSource Icon => Application.Current.TryFindResource("ConsoleIcon") as ImageSource
//   - override ImageSource SelectedIcon => Application.Current.TryFindResource("ConsoleEmphasizedIcon") as ImageSource
//   - CommandDescriptor Command { get; }
//   - 构造函数：base(command, PreferencesLocalization.Translate(command.Name, ForkPlusSettings.Default.UiLanguage), "")
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. Application.Current.TryFindResource(key) as ImageSource
//      → GetIconResource(key)（base 类提供的 spike 辅助方法，处理 Avalonia Image 控件 → IImage）
//   4. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
//   5. CommandDescriptor 从 ForkPlus.UI.Commands（WPF）→ spike POCO（同命名空间）

using Avalonia.Media;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class PaletteCommandItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon => Application.Current.TryFindResource("ConsoleIcon") as ImageSource
        public override IImage Icon => GetIconResource("ConsoleIcon");

        // 对照 WPF: public override ImageSource SelectedIcon => Application.Current.TryFindResource("ConsoleEmphasizedIcon") as ImageSource
        public override IImage SelectedIcon => GetIconResource("ConsoleEmphasizedIcon");

        public CommandDescriptor Command { get; }

        public PaletteCommandItem(CommandDescriptor command)
            : base(command, Translate(command.Name), "")
        {
            Command = command;
        }

        // 对照 WPF: PreferencesLocalization.Translate(command.Name, ForkPlusSettings.Default.UiLanguage)
        private static string Translate(string name)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(name, userSettings.UiLanguage);
            }
            return name;
        }
    }
}
