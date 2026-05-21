using System.Windows;
using System.Windows.Media;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.QuickLaunch
{
	public class PaletteCommandItem : CommandProviderItem
	{
		public override ImageSource Icon => Application.Current.TryFindResource("ConsoleIcon") as ImageSource;

		public override ImageSource SelectedIcon => Application.Current.TryFindResource("ConsoleEmphasizedIcon") as ImageSource;

		public CommandDescriptor Command { get; }

		public PaletteCommandItem(CommandDescriptor command)
			: base(command, PreferencesLocalization.Translate(command.Name, ForkPlusSettings.Default.UiLanguage), "")
		{
			Command = command;
		}
	}
}
