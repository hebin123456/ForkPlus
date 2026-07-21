using System.Collections.Generic;
using Avalonia.Controls;
using ForkPlus;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanel.cs（47 行）：
    //   - WPF ReferencePanel : ItemsControl（System.Windows.Controls）
    //   - Refresh(IReadOnlyList<Reference>, Remote[])：
    //     遍历 references，按类型分配 ViewModel：
    //       - LocalBranch → ReferencePanelLocalBranchViewModel
    //       - RemoteBranch → ReferencePanelRemoteBranchViewModel（含 RemoteIcon）
    //       - Tag → ReferencePanelTagViewModel
    //       - BisectMark → ReferencePanelBisectMarkViewModel
    //     设置 ItemsSource
    //   - RemoteIcon 解析：IReadOnlyListExtensions.FirstItem(remotes, name 匹配)?.GetIconImage()
    //     （GetIconImage 是 WPF 专有扩展方法，返回 System.Windows.Media.ImageSource）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ItemsControl + 显示引用列表）：
    //   1. WPF System.Windows.Controls.ItemsControl → Avalonia.Controls.ItemsControl
    //   2. WPF GetIconImage() 扩展（返回 ImageSource）→ spike 传 null
    //      （GetIconImage 为 WPF 专有 BridgeExtensions，spike 未迁移图标解析逻辑；
    //       ReferencePanelRemoteBranchViewModel.RemoteIcon 接收 IImage 类型，spike 传 null）
    //   3. WPF ImageSource RemoteIcon → Avalonia IImage RemoteIcon（在 ViewModel 层处理）
    //   4. spike 保持 Refresh 方法签名 + ViewModel 分配逻辑
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ItemsControl
    //   - Refresh(IReadOnlyList<Reference>, Remote[]) 方法
    //   - 按 Reference 类型分配 ViewModel
    //   - RemoteIcon spike 传 null（图标解析逻辑未迁移）
    public class ReferencePanel : ItemsControl
    {
        // 对照 WPF: public void Refresh(IReadOnlyList<Reference> references, Remote[] remotes)
        // spike: 保持方法签名，RemoteIcon 传 null
        public void Refresh(IReadOnlyList<Reference> references, Remote[] remotes)
        {
            ReferencePanelReferenceViewModel[] array = new ReferencePanelReferenceViewModel[references.Count];
            for (int i = 0; i < references.Count; i++)
            {
                Reference reference = references[i];
                if (!(reference is LocalBranch localBranch))
                {
                    RemoteBranch remoteBranch = reference as RemoteBranch;
                    if (remoteBranch == null)
                    {
                        if (!(reference is Tag tag))
                        {
                            if (reference is BisectMark bisectMark)
                            {
                                array[i] = new ReferencePanelBisectMarkViewModel(bisectMark);
                            }
                        }
                        else
                        {
                            array[i] = new ReferencePanelTagViewModel(tag);
                        }
                    }
                    else
                    {
                        // 对照 WPF: IReadOnlyListExtensions.FirstItem(remotes, x.Name == remoteBranch.Remote)?.GetIconImage()
                        // spike: GetIconImage 为 WPF 专有扩展，spike 传 null
                        // （RemoteBranch.Remote 为 string 类型，匹配 remotes 中的 Remote.Name）
                        array[i] = new ReferencePanelRemoteBranchViewModel(remoteBranch, null);
                    }
                }
                else
                {
                    array[i] = new ReferencePanelLocalBranchViewModel(localBranch);
                }
            }
            ItemsSource = array;
        }
    }
}
