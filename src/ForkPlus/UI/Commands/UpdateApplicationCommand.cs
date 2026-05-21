using System.Threading.Tasks;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class UpdateApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title => string.Empty;

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
		}

		public Task ExecuteAsync(bool silent)
		{
			return Task.CompletedTask;
		}
	}
}
