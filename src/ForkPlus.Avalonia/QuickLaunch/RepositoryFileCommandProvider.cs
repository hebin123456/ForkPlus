// RepositoryFileCommandProvider.cs：仓库文件命令提供者。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/RepositoryFileCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class RepositoryFileCommandProvider : ICommandProvider
//   - RepositoryFileItem[] _allItems
//   - 构造函数：CreateViewModels(GetAllRepositoryFiles(gitModule))
//   - GetAllRepositoryFiles：new GetAllRepositoryFilesGitCommand().Execute(gitModule)
//   - Refresh(filterString) → GetFilteredItems
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / CommandProviderItem / HeaderCommandProviderItem / RepositoryFileItem
//      均为同命名空间 spike 版
//   3. ForkPlus.Git.GitModule 来自 ForkPlus.Core（零修改复用）
//   4. GetAllRepositoryFilesGitCommand 来自 ForkPlus.Git.Commands（ForkPlus.Core，零修改复用）
//   5. NaturalStringComparer 来自 ForkPlus.Core（零修改复用）
//   6. FuzzyFilter 扩展方法来自 ForkPlus.Core

using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class RepositoryFileCommandProvider : ICommandProvider
    {
        private readonly RepositoryFileItem[] _allItems;

        public ArgumentType Type => ArgumentType.RepositoryFile;

        public CommandProviderItem[] Items { get; private set; }

        public CommandProviderItem SelectedItem { get; set; }

        public RepositoryFileCommandProvider(GitModule gitModule)
        {
            _allItems = CreateViewModels(GetAllRepositoryFiles(gitModule));
            Items = GetFilteredItems("");
        }

        public void Refresh(string filterString)
        {
            Items = GetFilteredItems(filterString);
        }

        private CommandProviderItem[] GetFilteredItems(string filterString)
        {
            IReadOnlyList<RepositoryFileItem> readOnlyList = _allItems.FuzzyFilter(filterString, (RepositoryFileItem x) => x.Title, (RepositoryFileItem x) => x.FilePath);
            CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
            array[0] = new HeaderCommandProviderItem("Repository Files");
            for (int i = 1; i < array.Length; i++)
            {
                readOnlyList[i - 1].FuzzySearchString = filterString;
                array[i] = readOnlyList[i - 1];
            }
            return array;
        }

        private RepositoryFileItem[] CreateViewModels(string[] rawItems)
        {
            RepositoryFileItem[] array = rawItems.Map((string file) => new RepositoryFileItem(file));
            Array.Sort(array, (RepositoryFileItem x, RepositoryFileItem y) => NaturalStringComparer.Instance.Compare(x.Title, y.Title));
            return array;
        }

        private string[] GetAllRepositoryFiles(GitModule gitModule)
        {
            GitCommandResult<string[]> gitCommandResult = new GetAllRepositoryFilesGitCommand().Execute(gitModule);
            if (gitCommandResult.Succeeded)
            {
                return gitCommandResult.Result;
            }
            return new string[0];
        }
    }
}
