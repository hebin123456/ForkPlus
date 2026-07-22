// GitFlowCommandProvider.cs：GitFlow 命令提供者。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/GitFlowCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class GitFlowCommandProvider : ICommandProvider
//   - IReadOnlyList<LocalBranch> _allItems
//   - ArgumentType Type { get; }
//   - 构造函数：按 type 过滤 localBranches（Feature/Hotfix/Release）
//   - Refresh(filterString) → GetFilteredBranches
//   - GetFilteredBranches：FuzzyFilter + HeaderCommandProviderItem + ReferenceItem
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / CommandProviderItem / HeaderCommandProviderItem / ReferenceItem
//      均为同命名空间 spike 版（见各文件）
//   3. ForkPlus.Git.LocalBranch / GitFlowSettings 来自 ForkPlus.Core（零修改复用）
//   4. FuzzyFilter / Filter 扩展方法来自 ForkPlus.Core（零修改复用）

using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class GitFlowCommandProvider : ICommandProvider
    {
        private readonly IReadOnlyList<LocalBranch> _allItems;

        private readonly string _headerString;

        public ArgumentType Type { get; }

        public CommandProviderItem[] Items { get; private set; }

        public GitFlowCommandProvider(LocalBranch[] localBranches, GitFlowSettings gitFlowSettings, ArgumentType type)
        {
            Type = type;
            if (gitFlowSettings == null)
            {
                Items = new CommandProviderItem[0];
                _headerString = "";
                return;
            }
            switch (type)
            {
            case ArgumentType.FeatureBranch:
                _allItems = localBranches.Filter((LocalBranch x) => x.IsFeatureBranch(gitFlowSettings));
                _headerString = "Git Flow Features";
                break;
            case ArgumentType.HotfixBranch:
                _allItems = localBranches.Filter((LocalBranch x) => x.IsHotfixBranch(gitFlowSettings));
                _headerString = "Git Flow Hotfixes";
                break;
            case ArgumentType.ReleaseBranch:
                _allItems = localBranches.Filter((LocalBranch x) => x.IsReleaseBranch(gitFlowSettings));
                _headerString = "Git Flow Releases";
                break;
            default:
                throw new Exception("Cannot reach here");
            }
        }

        public void Refresh(string filterString)
        {
            Items = GetFilteredBranches(filterString);
        }

        private CommandProviderItem[] GetFilteredBranches(string filterString)
        {
            IReadOnlyList<LocalBranch> readOnlyList = _allItems.FuzzyFilter(filterString, (LocalBranch x) => x.Name);
            CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
            array[0] = new HeaderCommandProviderItem(_headerString);
            for (int i = 1; i < array.Length; i++)
            {
                array[i] = new ReferenceItem(readOnlyList[i - 1], filterString);
            }
            return array;
        }
    }
}
