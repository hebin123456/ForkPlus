using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ForkPlus.Git.Commands
{
	public class GetBugtrackerRulesGitCommand
	{
		private static readonly Regex SectionRegex = new Regex("^\\s*\\[(.+?)\\s\"(.+?)\"\\]", RegexOptions.Compiled);

		private static readonly Regex EntryRegex = new Regex("(\\S+)\\s?=\\s?\"(.+)\"", RegexOptions.Compiled);

		public BugtrackerLinkDefinition[] Execute(GitModule gitModule)
		{
			List<BugtrackerLinkDefinition> list = new List<BugtrackerLinkDefinition>();
			if (!gitModule.Settings.ShowBugtrackerLinks)
			{
				return list.ToArray();
			}
			try
			{
				string filePath = gitModule.MakePath(".issuetracker");
				list.AddRange(LoadBugtrackerLinkDefinitions(filePath, Level.Shared));
				string filePath2 = Path.Combine(gitModule.GitDir(), "issuetracker");
				list.AddRange(LoadBugtrackerLinkDefinitions(filePath2, Level.Local));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load bug tracker definitions", ex);
				return list.ToArray();
			}
			return list.ToArray();
		}

		private static BugtrackerLinkDefinition[] LoadBugtrackerLinkDefinitions(string filePath, Level level)
		{
			List<BugtrackerLinkDefinition> list = new List<BugtrackerLinkDefinition>();
			if (!File.Exists(filePath))
			{
				return list.ToArray();
			}
			string text = null;
			string text2 = null;
			string text3 = null;
			foreach (string item in File.ReadAllLines(filePath).Map((string x) => x.Trim()).Filter((string x) => !string.IsNullOrEmpty(x) && !x.StartsWith("#")))
			{
				Match match = SectionRegex.Match(item);
				Match match2 = EntryRegex.Match(item);
				if (match.Success)
				{
					string text4 = null;
					if (match.Groups.Count > 2)
					{
						Group group = match.Groups[1];
						if (group != null && group.Success)
						{
							text4 = group.Value;
							if (text != null && text2 != null && text3 != null)
							{
								list.Add(BugtrackerLinkDefinition.Create(text, level, text2, text3));
							}
							if (text4 != "issuetracker")
							{
								Log.Error("Skip section " + text4);
								text = null;
								continue;
							}
							Group group2 = match.Groups[2];
							if (group2 != null && group2.Success)
							{
								text = group2.Value;
								continue;
							}
							Log.Error("Cannot parse section name in " + item);
							return list.ToArray();
						}
					}
					Log.Error("Cannot parse section name in " + item);
					return list.ToArray();
				}
				if (!match2.Success)
				{
					continue;
				}
				string text5 = null;
				if (match2.Groups.Count > 2)
				{
					Group group3 = match2.Groups[1];
					if (group3 != null && group3.Success)
					{
						text5 = group3.Value;
						string text6 = null;
						Group group4 = match2.Groups[2];
						if (group4 != null && group4.Success)
						{
							text6 = group4.Value;
							if (text5 == "regex")
							{
								text2 = text6.Replace("\\\\", "\\");
							}
							else if (text5 == "url")
							{
								text3 = text6.Replace("\\\\", "\\");
							}
							else
							{
								Log.Error("Unknown section entry: " + text5);
							}
							continue;
						}
						Log.Error("Cannot parse value in " + item);
						return list.ToArray();
					}
				}
				Log.Error("Cannot parse name in " + item);
				return list.ToArray();
			}
			if (text != null && text2 != null && text3 != null)
			{
				list.Add(BugtrackerLinkDefinition.Create(text, level, text2, text3));
			}
			return list.ToArray();
		}
	}
}
