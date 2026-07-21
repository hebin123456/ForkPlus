using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanelRemoteBranchViewModel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanelRemoteBranchViewModel.cs（20 行）：
    //   - WPF ReferencePanelRemoteBranchViewModel : ReferencePanelReferenceViewModel
    //   - 字段：_remoteBranch（RemoteBranch）
    //   - override Name => _remoteBranch.Name
    //   - ImageSource RemoteIcon 属性（WPF System.Windows.Media.ImageSource）
    //   - 构造函数接收 RemoteBranch + ImageSource
    //
    // Avalonia 版差异（spike 简化策略，task spec：POCO 类）：
    //   1. WPF ReferencePanelReferenceViewModel 基类 → Avalonia 同名基类
    //   2. WPF RemoteBranch（ForkPlus.Git.Core）→ 同（无 UI 依赖）
    //   3. WPF System.Windows.Media.ImageSource → Avalonia.Media.IImage
    //      （Avalonia Image.Source 绑定 IImage，对照 WPF Image.Source 绑定 ImageSource）
    //   4. spike 保持 POCO 形状（RemoteIcon 类型从 ImageSource 改为 IImage）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferencePanelReferenceViewModel
    //   - override Name => _remoteBranch.Name
    //   - IImage RemoteIcon 属性（Avalonia 版替代 WPF ImageSource）
    //   - 构造函数接收 RemoteBranch + IImage
    public class ReferencePanelRemoteBranchViewModel : ReferencePanelReferenceViewModel
    {
        // 对照 WPF: private RemoteBranch _remoteBranch
        private readonly RemoteBranch _remoteBranch;

        // 对照 WPF: public override string Name => _remoteBranch.Name
        public override string Name => _remoteBranch.Name;

        // 对照 WPF: public ImageSource RemoteIcon { get; }
        // spike: ImageSource → IImage（Avalonia.Media.IImage）
        public IImage RemoteIcon { get; }

        // 对照 WPF: public ReferencePanelRemoteBranchViewModel(RemoteBranch remoteBranch, ImageSource remoteIcon)
        // spike: ImageSource → IImage
        public ReferencePanelRemoteBranchViewModel(RemoteBranch remoteBranch, IImage remoteIcon)
        {
            _remoteBranch = remoteBranch;
            RemoteIcon = remoteIcon;
        }
    }
}
