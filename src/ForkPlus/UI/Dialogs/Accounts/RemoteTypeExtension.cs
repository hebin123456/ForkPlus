using ForkPlus.Accounts;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public static class RemoteTypeExtension
	{
		public static ForkPlusDialogWindow GetLoginWindow(this RemoteType self, Account account = null)
		{
			return self switch
			{
				RemoteType.Bitbucket => new BitbucketLoginWindow(account), 
				RemoteType.BitbucketServer => new BitbucketServerLoginWindow(account), 
				RemoteType.Gitea => new GiteaLoginWindow(account), 
				RemoteType.Github => new GitHubLoginWindow(account), 
				RemoteType.GithubEnterprise => new GitHubEnterpriseLoginWindow(account), 
				RemoteType.Gitlab => new GitLabLoginWindow(server: false, account), 
				RemoteType.GitlabServer => new GitLabLoginWindow(server: true, account), 
				_ => null, 
			};
		}
	}
}
