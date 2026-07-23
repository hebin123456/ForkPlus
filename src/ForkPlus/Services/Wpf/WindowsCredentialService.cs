namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 <see cref="ICredentialService"/> 实现，委托给现有 <see cref="WindowsCredentialManager"/> 静态类
	/// （封装 advapi32 CredRead/CredWrite/CredDelete）。
	/// 阶段 0 仅注册，<c>Accounts/</c> 层调用点将在阶段 1 迁移到此接口。
	/// </summary>
	public class WindowsCredentialService : ICredentialService
	{
		public Credential ReadCredential(string key)
		{
			return WindowsCredentialManager.ReadCredential(key);
		}

		public void WriteCredential(string key, string userName, string secret)
		{
			WindowsCredentialManager.WriteCredential(key, userName, secret);
		}

		public bool RemoveCredential(string key)
		{
			return WindowsCredentialManager.RemoveCredential(key);
		}

		public void StoreSshPassphrase(string sshKeyPath, string passphrase)
		{
			WindowsCredentialManager.StoreSshPassphrase(sshKeyPath, passphrase);
		}

		public string QuerySshPassphrase(string sshKeyPath)
		{
			return WindowsCredentialManager.QuerySshPassphrase(sshKeyPath);
		}

		public void StoreSshUserPassword(string url, string username, string password)
		{
			WindowsCredentialManager.StoreSshUserPassword(new System.Uri(url), username, password);
		}

		public string QuerySshUserPassword(string url, string username)
		{
			return WindowsCredentialManager.QuerySshUserPassword(new System.Uri(url), username);
		}
	}
}
