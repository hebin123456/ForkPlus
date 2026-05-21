using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Accounts;

namespace ForkPlus.Git.Commands
{
	public class GetRemotesGitCommand
	{
		public GitCommandResult<RepositoryRemotes> Execute(GitConfig gitConfig)
		{
			List<Remote> list = new List<Remote>(2);
			GitConfig.Section[] sections = gitConfig.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (!(section.Name == "remote") || !(section.Subsection != ""))
				{
					continue;
				}
				string subsection = section.Subsection;
				string url = null;
				bool disableImplicitFetch = false;
				GitConfig.Variable[] variables = section.Variables;
				for (int j = 0; j < variables.Length; j++)
				{
					GitConfig.Variable variable = variables[j];
					if (variable.Name == "url")
					{
						url = variable.Value;
					}
					else if (variable.Name == "disableimplicitfetch")
					{
						disableImplicitFetch = variable.Value == "true";
					}
				}
				Remote remote = CreateRemote(subsection, url, disableImplicitFetch);
				if (remote != null)
				{
					list.Add(remote);
				}
			}
			list.Sort(Remote.Comparer);
			return GitCommandResult<RepositoryRemotes>.Success(new RepositoryRemotes(list.ToArray()));
		}

		public GitCommandResult<RepositoryRemotes> Execute(GitModule gitModule)
		{
			string text = gitModule.ConfigFilePath();
			if (!File.Exists(text))
			{
				return GitCommandResult<RepositoryRemotes>.Failure(new GitCommandError.NotFound("Cannot find git config '" + text + "'"));
			}
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(text);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryRemotes>.Failure(gitCommandResult.Error);
			}
			GitConfig result = gitCommandResult.Result;
			return Execute(result);
		}

		[Null]
		private static Remote CreateRemote(string name, string url, bool disableImplicitFetch)
		{
			if (string.IsNullOrEmpty(name) || url == null)
			{
				return null;
			}
			Account account = FindAccount(url);
			return new Remote(name, url, disableImplicitFetch, account);
		}

		[Null]
		private static Account FindAccount(string url)
		{
			GitUrl gitUrl = new GitUrl(url);
			Uri uri = gitUrl.Uri;
			if ((object)uri == null)
			{
				return null;
			}
			string host = uri.Host;
			if (host == null)
			{
				return null;
			}
			return AccountManager.Current.FindAccount(host, gitUrl.Username);
		}
	}
}
