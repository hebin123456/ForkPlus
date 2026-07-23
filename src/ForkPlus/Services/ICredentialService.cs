using ForkPlus;

namespace ForkPlus.Services
{
	/// <summary>
	/// 凭据存储抽象（替换 <see cref="WindowsCredentialManager"/> 静态类的直接调用）。
	/// WPF/Windows 实现封装 Windows CredManager（advapi32 CredRead/CredWrite）；
	/// Linux/macOS 实现需用 libsecret / keychain 等替代方案。
	/// 该抽象使 <c>Accounts/</c> 层（PrivateAccessTokenAuthentication / BitbucketOAuthAuthentication）
	/// 可在阶段 1 验证为纯业务代码，不直接依赖 Windows 凭据 API。
	/// </summary>
	public interface ICredentialService
	{
		/// <summary>读取凭据，不存在返回 null。</summary>
		Credential ReadCredential(string key);

		/// <summary>写入凭据（覆盖同名）。</summary>
		void WriteCredential(string key, string userName, string secret);

		/// <summary>删除凭据，返回是否曾存在。</summary>
		bool RemoveCredential(string key);

		/// <summary>存储 SSH 私钥口令。</summary>
		void StoreSshPassphrase(string sshKeyPath, string passphrase);

		/// <summary>查询 SSH 私钥口令，不存在返回 null。</summary>
		string QuerySshPassphrase(string sshKeyPath);

		/// <summary>存储 SSH 用户名/密码（HTTP 场景）。</summary>
		void StoreSshUserPassword(string url, string username, string password);

		/// <summary>查询 SSH 用户名/密码，不存在返回 null。</summary>
		string QuerySshUserPassword(string url, string username);
	}
}
