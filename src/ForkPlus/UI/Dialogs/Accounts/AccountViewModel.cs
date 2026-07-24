// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System.ComponentModel;
using Avalonia.Media;
using ForkPlus.Accounts;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public class AccountViewModel : INotifyPropertyChanged
	{
		public Account Account { get; }

		public string UserName => Account.Username;

		public IImage Icon => Account.ServiceType.Icon();

		public string ServiceName
		{
			get
			{
				if (Account.ServiceType == RemoteType.BitbucketServer || Account.ServiceType == RemoteType.GithubEnterprise || Account.ServiceType == RemoteType.GitlabServer)
				{
					return Account.ServerUrl;
				}
				return Account.ServiceType.FriendlyName();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public AccountViewModel(Account account)
		{
			Account = account;
		}
	}
}
