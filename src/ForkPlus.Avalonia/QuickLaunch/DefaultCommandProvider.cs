// DefaultCommandProvider.cs：默认命令提供者（最复杂，spike 大幅简化）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/DefaultCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch, 351 行）
//   - class DefaultCommandProvider : ICommandProvider
//   - 嵌套类 CommandSection（Title + Items）
//   - CommandDescriptor[] _allCommands（GetAllCommands：BisectCommand/DecreaseLayoutScaleCommand/...
//     ShowGitFlowXxxWindowCommand/ShowGitLfsXxxCommand/FastForwardCommand/ApplyPatchCommand/
//     OpenRepositoryCommand/OpenRepositoryInShellToolCommand/OpenRepositoryInFileExplorerCommand/
//     SwitchWorkspaceCommand/GetCreatePullRequestCommands 等 40+ 个 WPF 命令类的 PublicCommands）
//   - CommandDescriptor[] _allCustomCommands（GetAllCustomCommands：CustomCommandManager.Current
//     .GetGlobalCustomCommands() + GetLocalCustomCommands(repositoryData)）
//   - RepositoryData _repositoryData
//   - Refresh(filterString) → FilteredItems
//   - FilteredItems：RecentRepositories + FilteredCommands + FilteredCustomCommands + FilteredRepositories
//     按 Title.Match(filterString) 降序排序各 CommandSection
//   - GetRecentRepositories：RepositoryManager.Instance.Repositories.ToSortedArray + Subsequence(0,8) + Map
//   - GetFilteredRepositories：RepositoryManager.Instance.Repositories.FuzzyFilter + Map
//   - GetFilteredCommands：_allCommands.FuzzyFilter by Name + Translate(Name) + Map → PaletteCommandItem
//   - GetFilteredCustomCommands：_allCustomCommands.FuzzyFilter by Name + Map → PaletteCommandItem
//   - GetCreatePullRequestCommands(remotes)：遍历 remotes 构造 Create Pull Request 命令
//     依赖 RepositoryUrlBuilder / CommitGraphCache / RepositoryUserControl.Commands（WPF-only）
//
// Avalonia 版差异（spike 简化策略）：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / CommandDescriptor / Argument / CommandProviderItem / HeaderCommandProviderItem /
//      PaletteCommandItem / RepositoryInfoItem 均为同命名空间 spike 版
//   3. ForkPlus.Git.RepositoryData / Remote 来自 ForkPlus.Core（零修改复用）
//   4. spike 简化 GetAllCommands：返回空数组
//      原因：BisectCommand / DecreaseLayoutScaleCommand / IncreaseLayoutScaleCommand / NewTabCommand /
//      ShowBenchmarkWindowCommand / ShowRepositorySettingsWindowCommand / ShowRepositoryStatisticsWindowCommand /
//      ShowRepositoryOverviewWindowCommand / ShowRemoveTagWindowCommand / ShowRemoveLocalBranchWindowCommand /
//      ShowRenameLocalBranchWindowCommand / ShowCreateBranchWindowCommand / ShowCreateTagWindowCommand /
//      ShowCreateWorktreeWindowCommand / ShowCheckoutBranchWindowCommand / ShowMergeBranchWindowCommand /
//      ShowRebaseBranchWindowCommand / ShowFileHistoryWindowCommand / ShowBlameWindowCommand /
//      ShowFetchWindowCommand / ShowPullWindowCommand / ShowPushWindowCommand / FastForwardCommand /
//      ApplyPatchCommand / ShowSaveStashWindowCommand / OpenRepositoryCommand /
//      OpenRepositoryInShellToolCommand / OpenRepositoryInFileExplorerCommand /
//      ShowGitFlowStartFeatureWindowCommand / ShowGitFlowStartHotfixWindowCommand /
//      ShowGitFlowStartReleaseWindowCommand / ShowGitFlowFinishFeatureWindowCommand /
//      ShowGitFlowFinishHotfixWindowCommand / ShowGitFlowFinishReleaseWindowCommand /
//      ShowGitFlowInitWindowCommand / ShowGitLfsFetchWindowCommand / ShowGitLfsPullWindowCommand /
//      GitLfsPruneCommand / ShowGitLfsStatusWindowCommand / GitLfsLockCommand / GitLfsUnlockCommand /
//      SwitchWorkspaceCommand 全部在 ForkPlus.UI.Commands 命名空间（WPF 工程，Avalonia 不可访问）
//   5. spike 简化 GetAllCustomCommands：返回空数组
//      原因：CustomCommandManager / CustomCommand / CustomCommandTarget / CustomCommandRefTarget /
//      CustomCommandEnvironment 全部在 ForkPlus.UI.CustomCommands 命名空间（WPF 工程）
//   6. spike 简化 GetCreatePullRequestCommands：返回空数组
//      原因：RepositoryUrlBuilder / CommitGraphCache / RepositoryUserControl.Commands 全部 WPF-only
//   7. spike 保留 FilteredItems 多段结构（RecentRepositories + Commands + CustomCommands + Repositories）
//      以便未来 Phase 接入真实命令后立即生效
//   8. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate（task spec 关键 API）
//   9. NaturalStringComparer / FuzzyFilter / Map / Subsequence / ToSortedArray / Match 扩展方法
//      均来自 ForkPlus.Core（零修改复用）
//  10. RepositoryManager.Instance.Repositories 返回空数组（spike 版，见 SpikeTypes.cs），
//      故 RecentRepositories / FilteredRepositories 段在 spike 阶段为空
//
// spike 简化（task spec 关键 API）：
//   - 保留 CommandSection 嵌套类
//   - 保留 FilteredItems 多段结构（即使各段在 spike 阶段为空）
//   - GetAllCommands / GetAllCustomCommands / GetCreatePullRequestCommands 返回空数组
//   - RecentRepositories / FilteredRepositories 用 spike RepositoryManager（空数组）

using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class DefaultCommandProvider : ICommandProvider
    {
        // 对照 WPF: private class CommandSection { string Title; CommandProviderItem[] Items; }
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

        // 对照 WPF: [Null] private readonly RepositoryData _repositoryData;
        private readonly RepositoryData _repositoryData;

        public ArgumentType Type => ArgumentType.Default;

        public CommandProviderItem[] Items { get; private set; }

        public CommandProviderItem SelectedItem { get; set; }

        public DefaultCommandProvider(RepositoryData repositoryData)
        {
            _repositoryData = repositoryData;
            _allCommands = GetAllCommands();
            _allCustomCommands = GetAllCustomCommands();
        }

        // 对照 WPF: private CommandDescriptor[] GetAllCustomCommands()
        //   list.AddRange(GetCustomCommands(CustomCommandManager.Current.GetGlobalCustomCommands()));
        //   list.AddRange(GetCustomCommands(CustomCommandManager.Current.GetLocalCustomCommands(_repositoryData)));
        // spike 简化：CustomCommandManager 为 WPF 工程专有，返回空数组
        private CommandDescriptor[] GetAllCustomCommands()
        {
            return new CommandDescriptor[0];
        }

        // 对照 WPF: private CommandDescriptor[] GetCustomCommands(CustomCommand[] customCommands)
        //   遍历 customCommands，按 Target（Repository/RepositoryFile/Reference）构造 CommandDescriptor
        // spike 简化：跳过（CustomCommand / CustomCommandTarget / CustomCommandRefTarget WPF-only）

        // 对照 WPF: private ArgumentType? GetCommandArgumentType(CustomCommandRefTarget[] referenceTargets)
        // spike 简化：跳过（CustomCommandRefTarget WPF-only）

        public void Refresh(string filterString)
        {
            Items = FilteredItems(filterString);
        }

        // 对照 WPF: private CommandDescriptor[] GetAllCommands()
        //   list.AddRange(BisectCommand.PublicCommands); ... list.AddRange(SwitchWorkspaceCommand.PublicCommands);
        //   list.Sort by NaturalStringComparer.Instance.Compare(lhs.Name, rhs.Name)
        // spike 简化：所有 *Command 类均为 WPF 工程专有（ForkPlus.UI.Commands 命名空间），返回空数组
        private CommandDescriptor[] GetAllCommands()
        {
            return new CommandDescriptor[0];
        }

        // 对照 WPF: private static CommandDescriptor[] GetCreatePullRequestCommands(Remote[] remotes)
        //   遍历 remotes，构造 "Create Pull Request on '{remote.Name}'..." 命令
        //   依赖 RepositoryUrlBuilder / CommitGraphCache / RepositoryUserControl.Commands（WPF-only）
        // spike 简化：返回空数组（依赖类型 WPF-only）
        private static CommandDescriptor[] GetCreatePullRequestCommands(Remote[] remotes)
        {
            return new CommandDescriptor[0];
        }

        // 对照 WPF: private CommandProviderItem[] FilteredItems(string filterString)
        //   RecentRepositories + FilteredCommands + FilteredCustomCommands + FilteredRepositories
        //   按 Title.Match(filterString) 降序排序各 CommandSection
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
            // 对照 WPF: list2.Sort((x, y) => -1 * x.Items[0].Title.Match(filterString).CompareTo(y.Items[0].Title.Match(filterString)));
            // 注意：仅在有 2 个及以上 section 时排序才有意义，单 section 跳过
            if (list2.Count >= 2)
            {
                list2.Sort((CommandSection x, CommandSection y) => -1 * x.Items[0].Title.Match(filterString).CompareTo(y.Items[0].Title.Match(filterString)));
            }
            foreach (CommandSection item in list2)
            {
                list.Add(new HeaderCommandProviderItem(item.Title));
                list.AddRange(item.Items);
            }
            return list.ToArray();
        }

        // 对照 WPF: private CommandSection GetRecentRepositories()
        //   RepositoryManager.Instance.Repositories.ToSortedArray(...).Subsequence(0, 8).Map(...)
        // spike: RepositoryManager.Instance.Repositories 返回空数组（spike 版），故本段为空
        private CommandSection GetRecentRepositories()
        {
            RepositoryInfoItem[] array = RepositoryManager.Instance.Repositories
                .ToSortedArray((RepositoryManager.Repository lhs, RepositoryManager.Repository rhs) => rhs.Opened.GetValueOrDefault().CompareTo(lhs.Opened.GetValueOrDefault()))
                .Subsequence(0, 8)
                .Map((RepositoryManager.Repository x) => new RepositoryInfoItem(x));
            CommandProviderItem[] items = array;
            return new CommandSection("Recent Repositories", items);
        }

        // 对照 WPF: private CommandSection GetFilteredRepositories(string filterString)
        //   RepositoryManager.Instance.Repositories.FuzzyFilter(...).Map(...)
        // spike: RepositoryManager.Instance.Repositories 返回空数组（spike 版），故本段为空
        private CommandSection GetFilteredRepositories(string filterString)
        {
            RepositoryInfoItem[] array = RepositoryManager.Instance.Repositories
                .FuzzyFilter(filterString, (RepositoryManager.Repository x) => x.GetDisplayName(), (RepositoryManager.Repository x) => x.Path)
                .Map((RepositoryManager.Repository x) => new RepositoryInfoItem(x)
                {
                    FuzzySearchString = filterString
                });
            CommandProviderItem[] items = array;
            return new CommandSection("Repositories", items);
        }

        // 对照 WPF: private CommandSection GetFilteredCommands(string filterString)
        //   _allCommands.FuzzyFilter(filterString, x => x.Name, x => PreferencesLocalization.Translate(x.Name, language))
        //     .Map(x => new PaletteCommandItem(x) { FuzzySearchString = filterString })
        // spike: _allCommands 为空数组（GetAllCommands 简化），故本段为空
        private CommandSection GetFilteredCommands(string filterString)
        {
            string language = GetUserLanguage();
            PaletteCommandItem[] array = _allCommands
                .FuzzyFilter(filterString, (CommandDescriptor x) => x.Name, (CommandDescriptor x) => TranslateCommandName(x.Name, language))
                .Map((CommandDescriptor x) => new PaletteCommandItem(x)
                {
                    FuzzySearchString = filterString
                });
            CommandProviderItem[] items = array;
            return new CommandSection("Commands", items);
        }

        // 对照 WPF: private CommandSection GetFilteredCustomCommands(string filterString)
        //   _allCustomCommands.FuzzyFilter(filterString, x => x.Name).Map(x => new PaletteCommandItem(x) {...})
        // spike: _allCustomCommands 为空数组（GetAllCustomCommands 简化），故本段为空
        private CommandSection GetFilteredCustomCommands(string filterString)
        {
            PaletteCommandItem[] array = _allCustomCommands
                .FuzzyFilter(filterString, (CommandDescriptor x) => x.Name)
                .Map((CommandDescriptor x) => new PaletteCommandItem(x)
                {
                    FuzzySearchString = filterString
                });
            CommandProviderItem[] items = array;
            return new CommandSection("Custom Commands", items);
        }

        // 对照 WPF: ForkPlusSettings.Default.UiLanguage
        // spike: 优先用 ServiceLocator.UserSettings.UiLanguage，回退 ForkPlusSettings.Default.UiLanguage
        private static string GetUserLanguage()
        {
            var userSettings = ServiceLocator.UserSettings;
            if (userSettings != null)
            {
                return userSettings.UiLanguage;
            }
            return ForkPlusSettings.Default.UiLanguage;
        }

        // 对照 WPF: PreferencesLocalization.Translate(x.Name, language)
        // spike: ServiceLocator.Localization.Translate(name, language)（task spec 关键 API）
        private static string TranslateCommandName(string name, string language)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null && !string.IsNullOrEmpty(language))
            {
                return localization.Translate(name, language);
            }
            return name;
        }
    }
}
