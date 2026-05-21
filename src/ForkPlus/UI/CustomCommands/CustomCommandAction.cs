using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.CustomCommands
{
	public abstract class CustomCommandAction
	{
		public static class Keys
		{
			public const string Type = "type";
		}

		public abstract void Execute(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandEnvironment env);
	}
}
