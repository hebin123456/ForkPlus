// RemoteCommandProvider.cs：远程仓库命令提供者。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/RemoteCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class RemoteCommandProvider : ICommandProvider
//   - Remote[] _allRemotes
//   - 构造函数：repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric)
//   - Refresh(filterString) → GetFilteredRemotes
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / CommandProviderItem / RemoteItem 均为同命名空间 spike 版
//   3. ForkPlus.Git.RepositoryData / Remote 来自 ForkPlus.Core（零修改复用）
//   4. ToSortedArray / FuzzyFilter / Map 扩展方法来自 ForkPlus.Core

using ForkPlus.Git;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class RemoteCommandProvider : ICommandProvider
    {
        private readonly Remote[] _allRemotes;

        public ArgumentType Type => ArgumentType.Remote;

        public CommandProviderItem[] Items { get; private set; }

        public RemoteCommandProvider(RepositoryData repositoryData)
        {
            _allRemotes = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
        }

        public void Refresh(string filterString)
        {
            CommandProviderItem[] filteredRemotes = GetFilteredRemotes(filterString);
            Items = filteredRemotes;
        }

        private RemoteItem[] GetFilteredRemotes(string filterString)
        {
            return _allRemotes.FuzzyFilter(filterString, (Remote x) => x.Name).Map((Remote x) => new RemoteItem(x)
            {
                FuzzySearchString = filterString
            });
        }
    }
}
