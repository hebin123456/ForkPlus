using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 应用上下文抽象（替换 Application.Current 的访问）
	/// </summary>
	public interface IAppContext
	{
		string AppDataDirectory { get; }
		string ForkDataDirectoryPath { get; }
		string RepositoriesFilePath { get; }
		Version OSVersion { get; }
		/// <summary>单实例锁文件目录（等价于 App.InstanceDirectory）。</summary>
		string InstanceDirectory { get; }
		/// <summary>应用进程 ID（等价于 App.ProcessId）。</summary>
		int ProcessId { get; }
		/// <summary>应用进程 ID 字符串形式（等价于 App.ProcessIdString）。</summary>
		string ProcessIdString { get; }
		/// <summary>HTTP User-Agent 头（等价于 App.UserAgent）。</summary>
		string UserAgent { get; }
		/// <summary>Fork 内置 credential helper 可执行文件路径（等价于 App.ForkCredentialHelperPath）。</summary>
		string ForkCredentialHelperPath { get; }
		void Shutdown();
	}
}
