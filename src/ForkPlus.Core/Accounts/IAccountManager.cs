namespace ForkPlus.Accounts
{
	/// <summary>
	/// AccountManager 抽象接口，用于解耦 Core/Git/ 对主工程 AccountManager.Current 静态单例的依赖。
	///
	/// Phase 0.2c：GetRemotesGitCommand.FindAccount 调用 AccountManager.Current.FindAccount。
	/// AccountManager 依赖 Account 类（含 GitService 等重型依赖，Phase 0.5 迁移），
	/// 不能立即迁入 Core。本接口只暴露 FindAccount 方法。
	///
	/// 主工程 AccountManager 实现本接口（或通过包装器），
	/// ServiceLocator.Initialize 时注入实例。
	/// </summary>
	public interface IAccountManager
	{
		/// <summary>按 host + username 查找已配置的 Account（等价于 AccountManager.Current.FindAccount）。</summary>
		IAccount FindAccount(string host, string username);
	}
}
