using System;
using System.Windows;
using System.Windows.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	public class IncreaseLayoutScaleCommand : IUICommand, IForkPlusCommand
	{
		private static readonly int Step = 10;

		private static readonly int MaxValue = 200;

		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Zoom In", new Argument[0], delegate
			{
				MainWindow.Commands.IncreaseLayoutScale.Execute();
			})
		};

		public string Title => "Zoom In";

		public KeyGesture Shortcut => new KeyGesture(Key.OemPlus, ModifierKeys.Control);

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ForkPlusSettings.Default.LayoutScaling = Math.Min(ForkPlusSettings.Default.LayoutScaling + Step, MaxValue);
			Application.Current.RefreshLayoutScaling();
		}
	}
}
