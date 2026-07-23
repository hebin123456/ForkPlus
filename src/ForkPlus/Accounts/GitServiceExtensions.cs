using System;
using System.Text;
using ForkPlus.Properties;
using ForkPlus.Settings;
using ForkPlus.Services;

namespace ForkPlus.Accounts
{
	public static class GitServiceExtensions
	{
		[Null]
		public static string AllowedQueryParametersHint(this GitService service)
		{
			if (service.AllowedQueryParameters.Length == 0)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(ServiceLocator.Localization.Translate("Allowed parameters:", ForkPlusSettings.Default.UiLanguage));
			Func<string, string, SearchQuery.Parameter>[] allowedQueryParameters = service.AllowedQueryParameters;
			foreach (Func<string, string, SearchQuery.Parameter> func in allowedQueryParameters)
			{
				if (func == new Func<string, string, SearchQuery.Parameter>(SearchQuery.Assignee.TryCreate))
				{
					stringBuilder.Append("\nassignee:USERNAME");
				}
				else if (func == new Func<string, string, SearchQuery.Parameter>(SearchQuery.Author.TryCreate))
				{
					stringBuilder.Append("\nauthor:USERNAME");
				}
				else if (func == new Func<string, string, SearchQuery.Parameter>(SearchQuery.Milestone.TryCreate))
				{
					stringBuilder.Append("\nmilestone:MILESTONE");
				}
			}
			return stringBuilder.ToString();
		}
	}
}
