using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.QuickLaunch
{
	public class PaletteCommandItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon => Application.Current.TryFindResource("ConsoleIcon") as global::Avalonia.Media.IImage;

		public override global::Avalonia.Media.IImage SelectedIcon => Application.Current.TryFindResource("ConsoleEmphasizedIcon") as global::Avalonia.Media.IImage;

		public CommandDescriptor Command { get; }

		public PaletteCommandItem(CommandDescriptor command)
			: base(command, PreferencesLocalization.Translate(command.Name, ForkPlusSettings.Default.UiLanguage), "")
		{
			Command = command;
		}
	}
}
