using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.CustomCommands
{
	public class CancelCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "cancel";
		}

		public override void Execute(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandEnvironment env)
		{
		}
	}
}
