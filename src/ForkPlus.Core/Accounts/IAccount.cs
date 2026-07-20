using ForkPlus.Git;

namespace ForkPlus.Accounts
{
	/// <summary>
	/// Account 抽象接口，用于解耦 Core/Git/ 对主工程 Account 类的直接依赖。
	///
	/// Phase 0.2c：Git/ 目录迁移到 Core 时，Remote.cs / GetRemotesGitCommand.cs
	/// 引用 Account 类型。Account 类本身依赖 GitService / 各种 *Service 实现
	/// （Phase 0.5 迁移），不能立即迁入 Core。本接口只暴露 Git/ 用到的两个属性：
	/// ServerUrl（RepositoryUrlBuilder 用）+ ServiceType（Remote 构造时用）。
	///
	/// 主工程 Account 类实现本接口；UI 层代码若需要 Service / Username 等成员，
	/// 可直接强转 ((Account)remote.Account).Service（主工程内部使用）。
	/// Phase 0.5 迁移 Account 到 Core 后本接口可考虑删除。
	/// </summary>
	public interface IAccount
	{
		/// <summary>服务端 URL（等价于 Account.ServerUrl）。</summary>
		string ServerUrl { get; }

		/// <summary>远程服务类型（等价于 Account.ServiceType）。</summary>
		RemoteType ServiceType { get; }

		/// <summary>用户名（等价于 Account.Username）。</summary>
		string Username { get; }
	}
}
