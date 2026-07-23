using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Services;

namespace ForkPlus.Jobs
{
	public static class JobMonitorExtensions
	{
		private static readonly Regex GitProgressRegEx = new Regex("(?:remote:\\s*)?([A-z ]+):\\s+(\\d+)%.*?(\\r)?");

		private static readonly string ASCII_EL0 = "[K";

		public static void Append(this JobMonitor monitor, [Null] string path, [Null] GitCommand command)
		{
			string text = command?.ArgumentsString ?? "";
			if (App.OverrideCredentialHelperBt.Length != 0)
			{
				string oldValue = string.Join(" ", App.OverrideCredentialHelperBt) + " ";
				text = text.Replace(oldValue, "");
			}
			if (App.OverrideCredentialHelper.Length != 0)
			{
				string oldValue2 = string.Join(" ", App.OverrideCredentialHelper) + " ";
				text = text.Replace(oldValue2, "");
			}
			text = text.Replace("-c push.default=upstream ", "");
			text = text.Replace(" --progress", "");
			if (path != null)
			{
				monitor.AppendCommandHeader("$ " + path + " " + text + "\n\n");
			}
			else
			{
				monitor.AppendCommandHeader("$ git " + text + "\n\n");
			}
		}

		public static bool HandleGitProgress(this JobMonitor monitor, string line)
		{
			Match match = GitProgressRegEx.Match(line);
			if (match.Success && match.Groups.Count > 3)
			{
				string arg = GetOperationName(match.Groups[1].Value).Trim();
				if (double.TryParse(match.Groups[2].Value, out var result))
				{
					monitor.Update(result, $"{arg}: {result}%");
					return true;
				}
			}
			return false;
		}

		private static string GetOperationName(string operationName)
		{
			if (operationName.StartsWith(ASCII_EL0))
			{
				operationName = operationName.Substring(ASCII_EL0.Length);
			}
			return ServiceLocator.Localization.Current(operationName);
		}
	}
}
