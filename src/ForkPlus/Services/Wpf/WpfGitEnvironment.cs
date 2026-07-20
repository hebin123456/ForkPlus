using System;
using ForkPlus.Services;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// IGitEnvironment 的 WPF 实现，包装 App 静态属性。
	/// 业务层迁移到 Core 后通过 ServiceLocator.GitEnvironment 访问这些属性。
	/// </summary>
	public class WpfGitEnvironment : IGitEnvironment
	{
		public string[] OverrideCredentialHelper => App.OverrideCredentialHelper;

		public string[] OverrideCredentialHelperBt => App.OverrideCredentialHelperBt;

		public string GitPath => App.GitPath;

		public string EnvironmentGitInstancePath => App.EnvironmentGitInstancePath;

		public string ForkGitInstancePath => App.ForkGitInstancePath;

		public string AppName => App.AppName;

		/// <summary>
		/// 返回原始命令行参数数组（等价于 Environment.GetCommandLineArgs()）。
		/// App.CliArguments 是 CliArguments 对象（封装 CliCommand），本接口要求原始字符串数组，
		/// 故直接调用 Environment.GetCommandLineArgs()。
		/// </summary>
		public string[] CliArguments => Environment.GetCommandLineArgs();

		public string ShellPath => App.ShellPath;

		public string BashPath => App.BashPath;
	}
}
