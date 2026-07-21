using ForkPlus.Avalonia.Dialogs.Accounts;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    /// <summary>
    /// Avalonia 端账户登录窗口工厂：根据 <see cref="RemoteType"/> 创建对应的登录窗口实例。
    ///
    /// 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/RemoteTypeExtension.cs：
    ///   public static ForkPlusDialogWindow GetLoginWindow(this RemoteType self, Account account = null)
    ///   {
    ///       return self switch
    ///       {
    ///           RemoteType.Bitbucket => new BitbucketLoginWindow(account),
    ///           RemoteType.BitbucketServer => new BitbucketServerLoginWindow(account),
    ///           RemoteType.Gitea => new GiteaLoginWindow(account),
    ///           RemoteType.Github => new GitHubLoginWindow(account),
    ///           RemoteType.GithubEnterprise => new GitHubEnterpriseLoginWindow(account),
    ///           RemoteType.Gitlab => new GitLabLoginWindow(server: false, account),
    ///           RemoteType.GitlabServer => new GitLabLoginWindow(server: true, account),
    ///           _ => null,
    ///       };
    ///   }
    ///
    /// Avalonia 版差异：
    ///   1. 返回类型从 WPF ForkPlusDialogWindow 改为 Avalonia ForkPlusDialogWindow（spike 版基类）
    ///   2. 新增 onAccountChanged 回调参数，透传给具体登录窗口（解耦 MainWindow 依赖）
    ///   3. OpenAi 不在此工厂中（WPF 也未在此工厂中，OpenAiLoginWindow 是独立路径）
    /// </summary>
    public static class RemoteTypeExtension
    {
        public static global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow? GetLoginWindow(
            this RemoteType self,
            ForkPlus.Accounts.Account? account = null,
            System.Action? onAccountChanged = null)
        {
            return self switch
            {
                RemoteType.Bitbucket => new BitbucketLoginWindow(account, onAccountChanged),
                RemoteType.BitbucketServer => new BitbucketServerLoginWindow(account, onAccountChanged),
                RemoteType.Gitea => new GiteaLoginWindow(account, onAccountChanged),
                RemoteType.Github => new GitHubLoginWindow(account, onAccountChanged),
                RemoteType.GithubEnterprise => new GitHubEnterpriseLoginWindow(account, onAccountChanged),
                RemoteType.Gitlab => new GitLabLoginWindow(server: false, account, onAccountChanged),
                RemoteType.GitlabServer => new GitLabLoginWindow(server: true, account, onAccountChanged),
                _ => null,
            };
        }
    }
}
