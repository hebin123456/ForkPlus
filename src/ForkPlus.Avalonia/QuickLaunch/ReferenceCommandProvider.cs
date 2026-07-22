// ReferenceCommandProvider.cs：引用命令提供者（分支/Tag）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/ReferenceCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class ReferenceCommandProvider : ICommandProvider
//   - 构造函数：按 argument.Type 从 repositoryData.References 取 LocalBranches/RemoteBranches/Tags 等
//   - Refresh(filterString) → GetFilteredReferences + GetFilteredPinnedReferences
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / Argument / CommandProviderItem / HeaderCommandProviderItem / ReferenceItem
//      均为同命名空间 spike 版
//   3. ForkPlus.Git.RepositoryData / Reference / Branch / LocalBranch / RemoteBranch / Tag / Remote
//      来自 ForkPlus.Core（零修改复用）
//   4. FuzzyFilter / Filter / ContainsItem / Map 扩展方法来自 ForkPlus.Core

using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class ReferenceCommandProvider : ICommandProvider
    {
        private readonly IReadOnlyList<Reference> _allReferences;

        private readonly IReadOnlyList<Reference> _pinnedReferences;

        private readonly string _headerString;

        public ArgumentType Type { get; }

        public CommandProviderItem[] Items { get; private set; }

        public ReferenceCommandProvider(RepositoryData repositoryData, Argument argument)
        {
            Type = argument.Type;
            string[] pinnedReferences = repositoryData.References.PinnedReferences;
            switch (argument.Type)
            {
            case ArgumentType.Reference:
                _allReferences = repositoryData.References.Items;
                _pinnedReferences = _allReferences.Filter((Reference x) => pinnedReferences.ContainsItem(x.FullReference));
                _headerString = "Branches and Tags";
                break;
            case ArgumentType.Tag:
                _allReferences = repositoryData.References.Tags;
                _pinnedReferences = _allReferences.Filter((Reference x) => pinnedReferences.ContainsItem(x.FullReference));
                _headerString = "Tags";
                break;
            case ArgumentType.Branch:
            {
                object tag = argument.Tag;
                Remote remote = tag as Remote;
                if (remote != null)
                {
                    List<Branch> list = new List<Branch>();
                    list.AddRange(repositoryData.References.LocalBranches.Filter((LocalBranch x) => x.UpstreamFullName?.StartsWith(remote.Name) ?? true));
                    list.AddRange(repositoryData.References.RemoteBranches.Filter((RemoteBranch x) => x.Remote == remote.Name));
                    _allReferences = list.ToArray();
                }
                else
                {
                    _allReferences = repositoryData.References.Items.CompactMap((Reference x) => x as Branch);
                }
                _pinnedReferences = _allReferences.Filter((Reference x) => pinnedReferences.ContainsItem(x.FullReference));
                _headerString = "Branches";
                break;
            }
            case ArgumentType.LocalBranch:
                _allReferences = repositoryData.References.LocalBranches;
                _pinnedReferences = _allReferences.Filter((Reference x) => pinnedReferences.ContainsItem(x.FullReference));
                _headerString = "Local Branches";
                break;
            case ArgumentType.RemoteBranch:
                _allReferences = repositoryData.References.RemoteBranches;
                _pinnedReferences = _allReferences.Filter((Reference x) => pinnedReferences.ContainsItem(x.FullReference));
                _headerString = "Remote Branches";
                break;
            default:
                throw new Exception("Cannot reach here");
            }
        }

        public void Refresh(string filterString)
        {
            CommandProviderItem[] filteredReferences = GetFilteredReferences(filterString);
            CommandProviderItem[] filteredPinnedReferences = GetFilteredPinnedReferences(filterString);
            List<CommandProviderItem> list = new List<CommandProviderItem>(filteredReferences.Length + filteredPinnedReferences.Length + 1);
            if (filteredPinnedReferences.Length != 0)
            {
                list.Add(new HeaderCommandProviderItem("Pinned"));
                list.AddRange(filteredPinnedReferences);
            }
            list.AddRange(filteredReferences);
            Items = list.ToArray();
        }

        private CommandProviderItem[] GetFilteredReferences(string filterString)
        {
            IReadOnlyList<Reference> readOnlyList = _allReferences.FuzzyFilter(filterString, (Reference x) => x.Name);
            CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
            array[0] = new HeaderCommandProviderItem(_headerString);
            for (int i = 1; i < array.Length; i++)
            {
                array[i] = new ReferenceItem(readOnlyList[i - 1], filterString);
            }
            return array;
        }

        private CommandProviderItem[] GetFilteredPinnedReferences(string filterString)
        {
            return _pinnedReferences.FuzzyFilter(filterString, (Reference x) => x.Name).Map((Reference x) => new ReferenceItem(x, filterString));
        }
    }
}
