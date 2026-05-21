using System;
using System.Collections.Generic;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class GetRepositoryOverviewDataGitCommand
	{
		private static string[] authorSeparator = new string[1] { "~#~" };

		public GitCommandResult<RepositoryOverviewData> Execute(GitModule gitModule, JobMonitor monitor)
		{
			Dictionary<string, List<int>> files = new Dictionary<string, List<int>>();
			List<Sha> shas = new List<Sha>();
			List<DateTime> authorDates = new List<DateTime>();
			List<int> identities = new List<int>();
			List<UserIdentity> identityStore = new List<UserIdentity>();
			Dictionary<string, int> identityCache = new Dictionary<string, int>();
			Dictionary<string, string> renames = new Dictionary<string, string>();
			Action<string> stdoutPipeHandler = delegate(string line)
			{
				line = line.TrimEnd();
				if (line.Length > 40 && IsHex(line[0]))
				{
					Range range = new Range(0, 40);
					Sha.TryParse(line.Substring(range), out var result);
					shas.Add(result);
					Range range2 = new Range(range.End, line.IndexOf(Consts.Chars.TabChar));
					DateTimeHelper.TryParseUnixDate(line.Substring(range2), out var result2);
					authorDates.Add(result2);
					if (authorDates.Count % 100 == 0)
					{
						monitor.Update(0.0, string.Format(Translate("{0} commits..."), authorDates.Count));
					}
					Range range3 = new Range(range2.End + 1, line.Length);
					string text = line.Substring(range3);
					if (identityCache.TryGetValue(text, out var value))
					{
						identities.Add(value);
					}
					else
					{
						string[] array = text.Split(authorSeparator, StringSplitOptions.None);
						identityStore.Add(new UserIdentity(array[0], array[1]));
						identityCache[text] = identityStore.Count - 1;
						identities.Add(identityStore.Count - 1);
					}
				}
				else
				{
					string[] array2 = line.Split(Consts.Chars.Tab);
					if (array2.Length > 1)
					{
						string key;
						if (!array2[0].StartsWith("R"))
						{
							key = ((!renames.TryGetValue(array2[1].TrimEnd(), out var value2)) ? array2[1] : value2);
						}
						else
						{
							string text2 = array2[1];
							string text3 = array2[2];
							string value3;
							while (renames.TryGetValue(text3, out value3))
							{
								text3 = value3;
							}
							if (text2 != text3)
							{
								renames[text2] = text3;
							}
							key = text3;
						}
						List<int> list;
						if (files.TryGetValue(key, out var value4))
						{
							list = value4;
						}
						else
						{
							list = new List<int>();
							files[key] = list;
						}
						list.Add(shas.Count - 1);
					}
				}
			};
			StringBuilder stderr = new StringBuilder();
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command("log", "--pretty=format:%H%at\t%aN~#~%aE", "--name-status", "--no-show-signature").ExecuteWithCallbackBt(stdoutPipeHandler, delegate(string line)
			{
				stderr.Append(line);
			}, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<RepositoryOverviewData>.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult<RepositoryOverviewData>.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				return GitCommandResult<RepositoryOverviewData>.Failure(new GitCommandError.CallbackUnknownError(stderr.ToString()));
			}
			return GitCommandResult<RepositoryOverviewData>.Success(new RepositoryOverviewData(files, shas.ToArray(), authorDates.ToArray(), identities.ToArray(), identityStore.ToArray()));
		}

		private static bool IsHex(char character)
		{
			if (!IsDigit(character))
			{
				if (character >= 'a')
				{
					return character <= 'f';
				}
				return false;
			}
			return true;
		}

		private static bool IsDigit(char character)
		{
			if (character >= '0')
			{
				return character <= '9';
			}
			return false;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
