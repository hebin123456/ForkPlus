using System.Diagnostics;

namespace ForkPlus.Git.Commands
{
	internal class GetGitFlowSettingsGitCommand
	{
		public GitCommandResult<GitFlowSettings> Execute(GitConfig gitConfig)
		{
			string text = null;
			string text2 = null;
			string text3 = null;
			string text4 = null;
			string text5 = null;
			string versionTag = null;
			GitConfig.Section[] sections = gitConfig.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (section.Name != "gitflow")
				{
					continue;
				}
				if (section.Subsection == "branch")
				{
					GitConfig.Variable[] variables = section.Variables;
					for (int j = 0; j < variables.Length; j++)
					{
						GitConfig.Variable variable = variables[j];
						if (variable.Name == "master")
						{
							text = variable.Value;
						}
						else if (variable.Name == "develop")
						{
							text2 = variable.Value;
						}
					}
				}
				else
				{
					if (!(section.Subsection == "prefix"))
					{
						continue;
					}
					GitConfig.Variable[] variables = section.Variables;
					for (int j = 0; j < variables.Length; j++)
					{
						GitConfig.Variable variable2 = variables[j];
						if (variable2.Name == "feature")
						{
							text3 = variable2.Value;
						}
						else if (variable2.Name == "release")
						{
							text4 = variable2.Value;
						}
						else if (variable2.Name == "hotfix")
						{
							text5 = variable2.Value;
						}
						else if (variable2.Name == "versionTag")
						{
							versionTag = variable2.Value;
						}
					}
				}
			}
			if (text == null || text2 == null || text3 == null || text4 == null || text5 == null)
			{
				return GitCommandResult<GitFlowSettings>.Success(null);
			}
			return GitCommandResult<GitFlowSettings>.Success(new GitFlowSettings(text, text2, text3, text4, text5, versionTag));
		}

		[Conditional("DEBUG")]
		private static void AssertAreEqual(GitFlowSettings current, GitFlowSettings old)
		{
		}
	}
}
