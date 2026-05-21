using ForkPlus.UI.Commands;

namespace ForkPlus.UI.QuickLaunch
{
	public class DummyCommandProvider : ICommandProvider
	{
		public ArgumentType Type => ArgumentType.Default;

		public CommandProviderItem[] Items => new CommandProviderItem[0];

		public void Refresh(string filter)
		{
		}
	}
}
