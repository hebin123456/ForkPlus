// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - Application.Current.TryFindResource(key) as ImageSource → Theme.FindImage(key)（4.3-b 门面）
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.QuickLaunch
{
	public class PaletteCommandItem : CommandProviderItem
	{
		public override IImage Icon => Theme.FindImage("ConsoleIcon");

		public override IImage SelectedIcon => Theme.FindImage("ConsoleEmphasizedIcon");

		public CommandDescriptor Command { get; }

		public PaletteCommandItem(CommandDescriptor command)
			: base(command, PreferencesLocalization.Translate(command.Name, ForkPlusSettings.Default.UiLanguage), "")
		{
			Command = command;
		}
	}
}
