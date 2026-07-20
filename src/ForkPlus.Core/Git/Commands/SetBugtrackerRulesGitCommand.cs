using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ForkPlus.Git.Commands
{
	public class SetBugtrackerRulesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, BugtrackerLinkDefinition[] bugtrackers)
		{
			string filePath = gitModule.MakePath(".issuetracker");
			List<BugtrackerLinkDefinition> bugtrackers2 = bugtrackers.CompactMap((BugtrackerLinkDefinition x) => x).Filter((BugtrackerLinkDefinition x) => x.Level == Level.Shared);
			SaveOrRemoveFile(filePath, Serialize(bugtrackers2));
			string filePath2 = Path.Combine(gitModule.GitDir(), "issuetracker");
			List<BugtrackerLinkDefinition> bugtrackers3 = bugtrackers.CompactMap((BugtrackerLinkDefinition x) => x).Filter((BugtrackerLinkDefinition x) => x.Level == Level.Local);
			SaveOrRemoveFile(filePath2, Serialize(bugtrackers3));
			return GitCommandResult.Success();
		}

		private static void SaveOrRemoveFile(string filePath, string content)
		{
			if (content != null)
			{
				try
				{
					File.WriteAllText(filePath, content);
					return;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to write to '" + filePath + "'", ex);
					return;
				}
			}
			if (File.Exists(filePath))
			{
				try
				{
					File.Delete(filePath);
				}
				catch (Exception ex2)
				{
					Log.Error("Failed to delete '" + filePath + "'", ex2);
				}
			}
		}

		private static string Serialize(IReadOnlyList<BugtrackerLinkDefinition> bugtrackers)
		{
			if (bugtrackers.Count == 0)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder(1024);
			stringBuilder.AppendLine("# Integration with Issue Tracker");
			stringBuilder.AppendLine("#");
			stringBuilder.AppendLine("# (note that '\\' need to be escaped).");
			foreach (BugtrackerLinkDefinition bugtracker in bugtrackers)
			{
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("[issuetracker \"" + bugtracker.Name + "\"]");
				string text = bugtracker.RegexString.Replace("\\", "\\\\");
				stringBuilder.AppendLine("  regex = \"" + text + "\"");
				string text2 = bugtracker.UrlString.Replace("\\", "\\\\");
				stringBuilder.AppendLine("  url = \"" + text2 + "\"");
			}
			return stringBuilder.ToString();
		}
	}
}
