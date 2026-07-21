using System.ComponentModel;
using ForkPlus.Accounts;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.21b：Avalonia 版 AccountViewModel（对照 WPF AccountViewModel.cs 35 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/AccountViewModel.cs：
    //   - public class AccountViewModel : INotifyPropertyChanged
    //   - 属性: Account Account / string UserName / ImageSource Icon / string ServiceName
    //   - 构造函数 (Account account)
    //   - Icon: Account.ServiceType.Icon()  ← 返回 WPF ImageSource（依赖 BridgeExtensions.RemoteTypeBridgeExtensions）
    //   - ServiceName: BitbucketServer/GithubEnterprise/GitlabServer → ServerUrl；其余 → FriendlyName()
    //
    // Avalonia 版差异：
    //   1. ImageSource Icon → string IconKey（用 IconKey 字符串键，View 层根据键查找图标）
    //      WPF: Account.ServiceType.Icon()  →  ImageSource
    //      Avalonia: Account.ServiceType.GetIconKey()  →  string（"GitHub" / "GitLab" / "Bitbucket" / ...）
    //      View 层在 axaml 中用 <Image Source="{Binding IconKey, Converter=...}" /> 或 DataTemplate 匹配键。
    //   2. INPC 保留（Avalonia 数据绑定同 WPF）
    //   3. ServiceName 逻辑完全保留（Core 已有 RemoteType.FriendlyName()）
    public class AccountViewModel : INotifyPropertyChanged
    {
        public Account Account { get; }

        public string UserName => Account.Username;

        // Avalonia spike 版：用 IconKey 字符串替代 WPF ImageSource
        // View 层根据 IconKey 选择对应的图标资源（Path / Bitmap / Geometry）
        public string IconKey => Account.ServiceType.GetIconKey();

        public string ServiceName
        {
            get
            {
                if (Account.ServiceType == RemoteType.BitbucketServer
                    || Account.ServiceType == RemoteType.GithubEnterprise
                    || Account.ServiceType == RemoteType.GitlabServer)
                {
                    return Account.ServerUrl;
                }
                return Account.ServiceType.FriendlyName();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AccountViewModel(Account account)
        {
            Account = account;
        }
    }
}
