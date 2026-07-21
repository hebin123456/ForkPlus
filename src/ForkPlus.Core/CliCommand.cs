using System;

namespace ForkPlus
{
	/// <summary>
	/// CLI 命令抽象。Phase 0.2c：CreateCliCommand 改用注入的委托工厂，
	/// 让 Core 不再直接引用主工程的 OpenRepositoryCliCommand（强 WPF 依赖）。
	///
	/// 主工程在 App.OnStartup 中调用 CliCommand.SetParser(OpenRepositoryCliCommand.Parse)
	/// 注册具体解析器；Core 端通过 CreateCliCommand 调用即可。
	/// </summary>
	public abstract class CliCommand
	{
		/// <summary>
		/// CLI 命令解析委托。主工程启动时注入 OpenRepositoryCliCommand.Parse 等具体实现。
		/// </summary>
		public static Func<string[], CliCommand> Parser { get; private set; }

		/// <summary>
		/// 由主工程在 App.OnStartup 中注册 CLI 命令解析器。
		/// </summary>
		public static void SetParser(Func<string[], CliCommand> parser)
		{
			Parser = parser;
		}

		public static CliCommand CreateCliCommand(string[] args)
		{
			return Parser?.Invoke(args);
		}

		public virtual void Run(string workingDirectory)
		{
		}
	}
}
