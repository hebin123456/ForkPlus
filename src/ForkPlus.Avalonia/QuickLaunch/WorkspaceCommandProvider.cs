// WorkspaceCommandProvider.cs：工作区命令提供者。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/WorkspaceCommandProvider.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class WorkspaceCommandProvider : ICommandProvider
//   - WorkspaceItem[] _allItems
//   - 构造函数：CreateViewModels(workspaces) + GetFilteredItems("")
//   - Refresh(filterString) → GetFilteredItems
//   - GetFilteredItems：FuzzyFilter by Title + HeaderCommandProviderItem("Workspaces")
//   - CreateViewModels：Map(workspaces) + NaturalStringComparer sort by Title
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ArgumentType / CommandProviderItem / HeaderCommandProviderItem / WorkspaceItem
//      均为同命名空间 spike 版
//   3. ForkPlus.UI.Workspace 来自 ForkPlus.Core（零修改复用，无 WPF 依赖）
//   4. FuzzyFilter / Map 扩展方法来自 ForkPlus.Core
//   5. NaturalStringComparer 来自 ForkPlus.Core（零修改复用）

using System;
using System.Collections.Generic;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class WorkspaceCommandProvider : ICommandProvider
    {
        private readonly WorkspaceItem[] _allItems;

        public ArgumentType Type => ArgumentType.Workspace;

        public CommandProviderItem[] Items { get; private set; }

        public WorkspaceCommandProvider(Workspace[] workspaces)
        {
            _allItems = CreateViewModels(workspaces);
            Items = GetFilteredItems("");
        }

        public void Refresh(string filter)
        {
            Items = GetFilteredItems(filter);
        }

        private CommandProviderItem[] GetFilteredItems(string filterString)
        {
            IReadOnlyList<WorkspaceItem> readOnlyList = _allItems.FuzzyFilter(filterString, (WorkspaceItem x) => x.Title);
            CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
            array[0] = new HeaderCommandProviderItem("Workspaces");
            for (int i = 1; i < array.Length; i++)
            {
                readOnlyList[i - 1].FuzzySearchString = filterString;
                array[i] = readOnlyList[i - 1];
            }
            return array;
        }

        private WorkspaceItem[] CreateViewModels(Workspace[] workspaces)
        {
            WorkspaceItem[] array = workspaces.Map((Workspace x) => new WorkspaceItem(x));
            Array.Sort(array, (WorkspaceItem x, WorkspaceItem y) => NaturalStringComparer.Instance.Compare(x.Title, y.Title));
            return array;
        }
    }
}
