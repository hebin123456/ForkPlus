using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.QuickLaunch
{
	public class DefaultCommandProvider : ICommandProvider
	{
		private class CommandSection
		{
			public string Title { get; }

			public CommandProviderItem[] Items { get; }

			public CommandSection(string title, CommandProviderItem[] items)
			{
				Title = title;
				Items = items;
			}
		}

		private readonly CommandDescriptor[] _allCommands;

		private readonly CommandDescriptor[] _allCustomCommands;

		[Null]
		private readonly RepositoryData _repositoryData;

		public ArgumentType Type => ArgumentType.Default;

		public CommandProviderItem[] Items { get; private set; }

		public CommandProviderItem SelectedItem { get; set; }

		public DefaultCommandProvider([Null] RepositoryData repositoryData)
		{
			_repositoryData = repositoryData;
			_allCommands = GetAllCommands();
			_allCustomCommands = GetAllCustomCommands();
		}

		private CommandDescriptor[] GetAllCustomCommands()
		{
			List<CommandDescriptor> list = new List<CommandDescriptor>();
			if (_repositoryData == null)
			{
				return list.ToArray();
			}
			list.AddRange(GetCustomCommands(CustomCommandManager.Current.GetGlobalCustomCommands()));
			list.AddRange(GetCustomCommands(CustomCommandManager.Current.GetLocalCustomCommands(_repositoryData)));
			list.Sort((CommandDescriptor lhs, CommandDescriptor rhs) => NaturalStringComparer.Instance.Compare(lhs.Name, rhs.Name));
			return list.ToArray();
		}

		private CommandDescriptor[] GetCustomCommands(CustomCommand[] customCommands)
		{
			List<CommandDescriptor> list = new List<CommandDescriptor>();
			foreach (CustomCommand customCommand in customCommands)
			{
				if (customCommand.Target == CustomCommandTarget.Repository)
				{
					CommandDescriptor item = new CommandDescriptor(customCommand.Name, new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
					{
						GitModule gitModule3 = repositoryUserControl.GitModule;
						if (gitModule3 != null)
						{
							CustomCommandEnvironment env3 = new CustomCommandEnvironment(gitModule3);
							RepositoryUserControl.Commands.RunCustomCommand.Execute(repositoryUserControl, customCommand, env3);
						}
					});
					list.Add(item);
				}
				else if (customCommand.Target == CustomCommandTarget.RepositoryFile)
				{
					CommandDescriptor item2 = new CommandDescriptor(customCommand.Name, new Argument[1]
					{
						new Argument(ArgumentType.RepositoryFile)
					}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
					{
						GitModule gitModule2 = repositoryUserControl.GitModule;
						if (gitModule2 != null && arguments[0] is string filepath)
						{
							CustomCommandEnvironment env2 = new CustomCommandEnvironment(gitModule2, filepath, null);
							RepositoryUserControl.Commands.RunCustomCommand.Execute(repositoryUserControl, customCommand, env2);
						}
					});
					list.Add(item2);
				}
				else
				{
					if (customCommand.Target != CustomCommandTarget.Reference)
					{
						continue;
					}
					ArgumentType? commandArgumentType = GetCommandArgumentType(customCommand.ReferenceTargets);
					if (!commandArgumentType.HasValue)
					{
						continue;
					}
					ArgumentType valueOrDefault = commandArgumentType.GetValueOrDefault();
					list.Add(new CommandDescriptor(customCommand.Name, new Argument[1]
					{
						new Argument(valueOrDefault)
					}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
					{
						GitModule gitModule = repositoryUserControl.GitModule;
						if (gitModule != null && arguments[0] is Reference reference)
						{
							CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, reference);
							RepositoryUserControl.Commands.RunCustomCommand.Execute(repositoryUserControl, customCommand, env);
						}
					}));
				}
			}
			return list.ToArray();
		}

		private ArgumentType? GetCommandArgumentType(CustomCommandRefTarget[] referenceTargets)
		{
			if (referenceTargets.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.LocalBranch))
			{
				if (referenceTargets.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.RemoteBranch))
				{
					if (referenceTargets.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.Tag))
					{
						return ArgumentType.Reference;
					}
					return ArgumentType.Branch;
				}
				return ArgumentType.LocalBranch;
			}
			if (referenceTargets.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.RemoteBranch))
			{
				return ArgumentType.RemoteBranch;
			}
			if (referenceTargets.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.Tag))
			{
				return ArgumentType.Tag;
			}
			return null;
		}

		public void Refresh(string filterString)
		{
			Items = FilteredItems(filterString);
		}

		private CommandDescriptor[] GetAllCommands()
		{
			List<CommandDescriptor> list = new List<CommandDescriptor>(128);
			if (_repositoryData != null)
			{
				list.AddRange(BisectCommand.PublicCommands);
				list.AddRange(DecreaseLayoutScaleCommand.PublicCommands);
				list.AddRange(IncreaseLayoutScaleCommand.PublicCommands);
				list.AddRange(NewTabCommand.PublicCommands);
				list.AddRange(ShowBenchmarkWindowCommand.PublicCommands);
				list.AddRange(ShowRepositorySettingsWindowCommand.PublicCommands);
				list.AddRange(ShowRepositoryStatisticsWindowCommand.PublicCommands);
				list.AddRange(ShowRepositoryOverviewWindowCommand.PublicCommands);
				list.AddRange(ShowRemoveTagWindowCommand.PublicCommands);
				list.AddRange(ShowRemoveLocalBranchWindowCommand.PublicCommands);
				list.AddRange(ShowRenameLocalBranchWindowCommand.PublicCommands);
				list.AddRange(ShowCreateBranchWindowCommand.PublicCommands);
				list.AddRange(ShowCreateTagWindowCommand.PublicCommands);
				list.AddRange(ShowCreateWorktreeWindowCommand.PublicCommands);
				list.AddRange(ShowCheckoutBranchWindowCommand.PublicCommands);
				list.AddRange(ShowMergeBranchWindowCommand.PublicCommands);
				list.AddRange(ShowRebaseBranchWindowCommand.PublicCommands);
				list.AddRange(ShowFileHistoryWindowCommand.PublicCommands);
				list.AddRange(ShowBlameWindowCommand.PublicCommands);
				list.AddRange(ShowFetchWindowCommand.PublicCommands);
				list.AddRange(ShowPullWindowCommand.PublicCommands);
				list.AddRange(ShowPushWindowCommand.PublicCommands);
				list.AddRange(FastForwardCommand.PublicCommands);
				list.AddRange(ApplyPatchCommand.PublicCommands);
				list.AddRange(ShowSaveStashWindowCommand.PublicCommands);
				list.AddRange(OpenRepositoryCommand.PublicCommands);
				list.AddRange(OpenRepositoryInShellToolCommand.PublicCommands);
				list.AddRange(OpenRepositoryInFileExplorerCommand.PublicCommands);
				list.AddRange(GetCreatePullRequestCommands(_repositoryData.Remotes.Items));
				if (_repositoryData.GitFlowSettings != null)
				{
					list.AddRange(ShowGitFlowStartFeatureWindowCommand.PublicCommands);
					list.AddRange(ShowGitFlowStartHotfixWindowCommand.PublicCommands);
					list.AddRange(ShowGitFlowStartReleaseWindowCommand.PublicCommands);
					list.AddRange(ShowGitFlowFinishFeatureWindowCommand.PublicCommands);
					list.AddRange(ShowGitFlowFinishHotfixWindowCommand.PublicCommands);
					list.AddRange(ShowGitFlowFinishReleaseWindowCommand.PublicCommands);
				}
				else
				{
					list.AddRange(ShowGitFlowInitWindowCommand.PublicCommands);
				}
				if (_repositoryData.GitLfsInitialized && _repositoryData.Remotes.HasLfsCompatibleRemotes())
				{
					list.AddRange(ShowGitLfsFetchWindowCommand.PublicCommands);
					list.AddRange(ShowGitLfsPullWindowCommand.PublicCommands);
					list.AddRange(GitLfsPruneCommand.PublicCommands);
					list.AddRange(ShowGitLfsStatusWindowCommand.PublicCommands);
					list.AddRange(GitLfsLockCommand.PublicCommands);
					list.AddRange(GitLfsUnlockCommand.PublicCommands);
				}
			}
			list.AddRange(SwitchWorkspaceCommand.PublicCommands);
			list.Sort((CommandDescriptor lhs, CommandDescriptor rhs) => NaturalStringComparer.Instance.Compare(lhs.Name, rhs.Name));
			return list.ToArray();
		}

		private static CommandDescriptor[] GetCreatePullRequestCommands(Remote[] remotes)
		{
			List<CommandDescriptor> list = new List<CommandDescriptor>();
			foreach (Remote remote in remotes)
			{
				list.Add(new CommandDescriptor("Create Pull Request on '" + remote.Name + "'...", new Argument[1]
				{
					new Argument(ArgumentType.Branch, null, remote)
				}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
				{
					if (arguments[0] is Branch branch)
					{
						GitModule gitModule = repositoryUserControl.GitModule;
						if (gitModule != null)
						{
							RepositoryData repositoryData = repositoryUserControl.RepositoryData;
							if (repositoryData != null)
							{
								CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
								if (commitGraphCache != null)
								{
									if (branch is RemoteBranch remoteBranch)
									{
										string text = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(remoteBranch.ShortName);
										if (text != null)
										{
											RepositoryUserControl.Commands.CreatePullRequest.Execute(text);
										}
									}
									else
									{
										LocalBranch localBranch = branch as LocalBranch;
										if (localBranch != null)
										{
											RemoteBranch remoteBranch2 = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == localBranch.UpstreamFullReference && x.Remote == remote.Name);
											string branch2 = remoteBranch2?.ShortName ?? localBranch.Name;
											string text2 = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(branch2);
											if (text2 != null)
											{
												if (remoteBranch2 == null || localBranch.IsInfrontUpstream(remoteBranch2, gitModule, commitGraphCache))
												{
													RepositoryUserControl.Commands.CreatePullRequest.Execute(repositoryUserControl, localBranch, remoteBranch2, remote.Name, text2);
												}
												else
												{
													RepositoryUserControl.Commands.CreatePullRequest.Execute(text2);
												}
											}
										}
									}
								}
							}
						}
					}
				}));
			}
			list.Sort((CommandDescriptor lhs, CommandDescriptor rhs) => NaturalStringComparer.Instance.Compare(lhs.Name, rhs.Name));
			return list.ToArray();
		}

		private CommandProviderItem[] FilteredItems(string filterString)
		{
			List<CommandProviderItem> list = new List<CommandProviderItem>(128);
			List<CommandSection> list2 = new List<CommandSection>(4);
			if (string.IsNullOrEmpty(filterString))
			{
				CommandSection recentRepositories = GetRecentRepositories();
				if (recentRepositories.Items.Length != 0)
				{
					list2.Add(recentRepositories);
				}
			}
			CommandSection filteredCommands = GetFilteredCommands(filterString);
			CommandSection filteredCustomCommands = GetFilteredCustomCommands(filterString);
			if (filteredCommands.Items.Length != 0)
			{
				list2.Add(filteredCommands);
			}
			if (filteredCustomCommands.Items.Length != 0)
			{
				list2.Add(filteredCustomCommands);
			}
			if (!string.IsNullOrEmpty(filterString))
			{
				CommandSection filteredRepositories = GetFilteredRepositories(filterString);
				if (filteredRepositories.Items.Length != 0)
				{
					list2.Add(filteredRepositories);
				}
			}
			list2.Sort((CommandSection x, CommandSection y) => -1 * x.Items[0].Title.Match(filterString).CompareTo(y.Items[0].Title.Match(filterString)));
			foreach (CommandSection item in list2)
			{
				list.Add(new HeaderCommandProviderItem(item.Title));
				list.AddRange(item.Items);
			}
			return list.ToArray();
		}

		private CommandSection GetRecentRepositories()
		{
			RepositoryInfoItem[] array = RepositoryManager.Instance.Repositories.ToSortedArray((RepositoryManager.Repository lhs, RepositoryManager.Repository rhs) => rhs.Opened.GetValueOrDefault().CompareTo(lhs.Opened.GetValueOrDefault())).Subsequence(0, 8).Map((RepositoryManager.Repository x) => new RepositoryInfoItem(x));
			CommandProviderItem[] items = array;
			return new CommandSection("Recent Repositories", items);
		}

		private CommandSection GetFilteredRepositories(string filterString)
		{
			RepositoryInfoItem[] array = RepositoryManager.Instance.Repositories.FuzzyFilter(filterString, (RepositoryManager.Repository x) => x.Name(), (RepositoryManager.Repository x) => x.Path).Map((RepositoryManager.Repository x) => new RepositoryInfoItem(x)
			{
				FuzzySearchString = filterString
			});
			CommandProviderItem[] items = array;
			return new CommandSection("Repositories", items);
		}

		private CommandSection GetFilteredCommands(string filterString)
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			PaletteCommandItem[] array = _allCommands.FuzzyFilter(filterString, (CommandDescriptor x) => x.Name, (CommandDescriptor x) => PreferencesLocalization.Translate(x.Name, language)).Map((CommandDescriptor x) => new PaletteCommandItem(x)
			{
				FuzzySearchString = filterString
			});
			CommandProviderItem[] items = array;
			return new CommandSection("Commands", items);
		}

		private CommandSection GetFilteredCustomCommands(string filterString)
		{
			PaletteCommandItem[] array = _allCustomCommands.FuzzyFilter(filterString, (CommandDescriptor x) => x.Name).Map((CommandDescriptor x) => new PaletteCommandItem(x)
			{
				FuzzySearchString = filterString
			});
			CommandProviderItem[] items = array;
			return new CommandSection("Custom Commands", items);
		}
	}
}
