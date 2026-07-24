using System;
using Avalonia.Input;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	public class DecreaseLayoutScaleCommand : IUICommand, IForkPlusCommand
	{
		private static readonly int Step = 10;

		private static readonly int MinValue = 100;

		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Zoom Out", new Argument[0], delegate
			{
				MainWindow.Commands.DecreaseLayoutScale.Execute();
			})
		};

		public string Title => "Zoom Out";

		public KeyGesture Shortcut => new KeyGesture(Key.OemMinus, KeyModifiers.Control);

		public KeyGesture SecondaryShortcut => new KeyGesture(Key.Subtract, KeyModifiers.Control);

		public void Execute()
		{
			ForkPlusSettings.Default.LayoutScaling = Math.Max(ForkPlusSettings.Default.LayoutScaling - Step, MinValue);
			ServiceLocator.WindowManager.RefreshLayoutScaling();
		}
	}
}
