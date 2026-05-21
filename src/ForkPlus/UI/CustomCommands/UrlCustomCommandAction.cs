using System;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.CustomCommands
{
	public class UrlCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "url";

			public const string Url = "url";
		}

		public string Url { get; }

		public UrlCustomCommandAction(string url)
		{
			Url = url;
		}

		public override void Execute(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandEnvironment env)
		{
			Log.Info("Run url custom action for '" + customCommandName + "'");
			new Uri(env.ReplaceVariablesWithValues(Url, urlEncode: true)).OpenInBrowser();
		}
	}
}
