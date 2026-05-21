using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Git.Commands
{
	public class GetSubmodulesGitCommand
	{
		private static Submodule[] Empty = new Submodule[0];

		public GitCommandResult<Submodule[]> Execute(GitModule gitModule)
		{
			return Execute(gitModule.GitModulesFilePath);
		}

		public GitCommandResult<Submodule[]> Execute(string gitModulesFile)
		{
			if (!File.Exists(gitModulesFile))
			{
				return GitCommandResult<Submodule[]>.Success(Empty);
			}
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModulesFile);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<Submodule[]>.Failure(gitCommandResult.Error);
			}
			GitConfig result = gitCommandResult.Result;
			List<Submodule> list = new List<Submodule>(4);
			GitConfig.Section[] sections = result.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (section.Name != "submodule")
				{
					continue;
				}
				string text = null;
				bool isActive = true;
				GitConfig.Variable[] variables = section.Variables;
				for (int j = 0; j < variables.Length; j++)
				{
					GitConfig.Variable variable = variables[j];
					if (variable.Name == "path")
					{
						text = variable.Value;
					}
					if (variable.Name == "active" && variable.Value == "false")
					{
						isActive = false;
					}
				}
				if (text != null)
				{
					list.Add(new Submodule(text, isActive));
				}
			}
			list.Sort(Submodule.Comparer);
			return GitCommandResult<Submodule[]>.Success(list.ToArray());
		}

		private GitCommandResult<Submodule[]> ExecuteOld(string gitModulesFile)
		{
			if (!File.Exists(gitModulesFile))
			{
				return GitCommandResult<Submodule[]>.Success(Empty);
			}
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModulesFile);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<Submodule[]>.Failure(gitCommandResult.Error);
			}
			GitConfig result = gitCommandResult.Result;
			List<Submodule> list = new List<Submodule>(result.Sections.Length);
			GitConfig.Section[] sections = result.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (section.Name != "submodule")
				{
					continue;
				}
				string text = null;
				bool isActive = true;
				GitConfig.Variable[] variables = section.Variables;
				for (int j = 0; j < variables.Length; j++)
				{
					GitConfig.Variable variable = variables[j];
					if (variable.Name == "path")
					{
						text = variable.Value;
					}
					if (variable.Name == "active" && variable.Value == "false")
					{
						isActive = false;
					}
				}
				if (text != null)
				{
					list.Add(new Submodule(text, isActive));
				}
			}
			list.Sort(Submodule.Comparer);
			return GitCommandResult<Submodule[]>.Success(list.ToArray());
		}
	}
}
