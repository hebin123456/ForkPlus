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
		void Shutdown();
	}
}
