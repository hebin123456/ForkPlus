using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// Git 运行环境抽象（替换 App.xaml.cs 中的 static 属性）。
	///
	/// 当前主工程的 App 类有大量 static 属性被业务层直接引用：
	/// - App.OverrideCredentialHelper / App.OverrideCredentialHelperBt（30+ 处）
	/// - App.GitPath / App.EnvironmentGitInstancePath / App.ForkGitInstancePath
	/// - App.AppName / App.CliArguments / App.OSVersion
	/// - App.ForkDirectoryPath / App.ForkDataDirectoryPath / App.RepositoriesFilePath
	///
	/// 本接口抽象 Git 运行时相关的属性，让业务层通过 ServiceLocator 访问。
	/// 注意：AppDataDirectory / ForkDataDirectoryPath / RepositoriesFilePath / OSVersion / Shutdown
	/// 已在 <see cref="IAppContext"/> 中定义，本接口只覆盖 Git 特有的部分。
	///
	/// 实施策略（Phase 0.3+）：
	/// 1. 主工程实现 WpfGitEnvironment，从 App 静态属性取值。
	/// 2. ServiceLocator.Initialize 时注入实例。
	/// 3. 业务层把 App.OverrideCredentialHelper 改为 ServiceLocator.GitEnvironment.OverrideCredentialHelper。
	/// 4. 给 App 静态属性加 [Obsolete] 标注，编译告警驱动替换。
	/// </summary>
	public interface IGitEnvironment
	{
		/// <summary>
		/// 用户在 Preferences 中配置的 credential helper 覆盖命令（等价于 App.OverrideCredentialHelper）。
		/// 返回 null 或空数组表示使用默认 credential helper。
		/// </summary>
		string[] OverrideCredentialHelper { get; }

		/// <summary>
		/// Biturbo 路径下的 credential helper 覆盖命令（等价于 App.OverrideCredentialHelperBt）。
		/// </summary>
		string[] OverrideCredentialHelperBt { get; }

		/// <summary>
		/// git.exe 完整路径（等价于 App.GitPath）。
		/// </summary>
		string GitPath { get; }

		/// <summary>
		/// 环境 PATH 中的 git 实例路径（等价于 App.EnvironmentGitInstancePath）。
		/// </summary>
		string EnvironmentGitInstancePath { get; }

		/// <summary>
		/// Fork 内置的 git 实例路径（等价于 App.ForkGitInstancePath）。
		/// </summary>
		string ForkGitInstancePath { get; }

		/// <summary>
		/// 应用名称（等价于 App.AppName）。
		/// </summary>
		string AppName { get; }

		/// <summary>
		/// 命令行参数（原始字符串数组，等价于 Environment.GetCommandLineArgs()）。
		/// 主工程实现时如需包装为 CliArguments 对象，可自行在 UI 层做。
		/// </summary>
		string[] CliArguments { get; }
	}
}
