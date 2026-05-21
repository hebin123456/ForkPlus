using ForkPlus.UI.Commands;

namespace ForkPlus.UI.QuickLaunch
{
	public interface ICommandProvider
	{
		ArgumentType Type { get; }

		CommandProviderItem[] Items { get; }

		void Refresh(string filterString);
	}
}
