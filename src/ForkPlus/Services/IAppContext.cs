using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 应用上下文抽象（替换 <see cref="ForkPlus.App"/> 静态属性的散落访问）。
	/// 阶段 0 扩展：将 <c>App.GitPath</c> / <c>App.BashPath</c> / <c>App.Version</c> 等
	/// 静态属性收敛到此处，使业务层不再直接依赖 <c>App</c> 类。
	/// </summary>
	public interface IAppContext
	{
		string AppDataDirectory { get; }
		string ForkDataDirectoryPath { get; }
		string RepositoriesFilePath { get; }
		Version OSVersion { get; }
		void Shutdown();

		/// <summary>git.exe 可执行路径（阶段 0 新增，替换 <c>App.GitPath</c>）。</summary>
		string GitPath { get; }

		/// <summary>sh.exe 路径（阶段 0 新增，替换 <c>App.ShellPath</c>）。</summary>
		string ShellPath { get; }

		/// <summary>bash.exe 路径（阶段 0 新增，替换 <c>App.BashPath</c>）。</summary>
		string BashPath { get; }

		/// <summary>git-mm 可执行路径（阶段 0 新增，替换 <c>App.GitMmPath</c>）。</summary>
		string GitMmPath { get; }

		/// <summary>当前进程 ID（阶段 0 新增，替换 <c>App.ProcessId</c>）。</summary>
		int ProcessId { get; }

		/// <summary>应用版本字符串（阶段 0 新增，替换 <c>App.Version</c>）。</summary>
		string Version { get; }

		/// <summary>HTTP User-Agent（阶段 0 新增，替换 <c>App.UserAgent</c>）。</summary>
		string UserAgent { get; }
	}
}
