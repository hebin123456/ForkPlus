// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - Application.Current.TabManager() 保持（ApplicationExtensions 扩展方法已迁移，
//   内部转发 (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow）
using System;
using System.IO;
using Avalonia;
using ForkPlus.UI;

namespace ForkPlus
{
	public class OpenRepositoryCliCommand : CliCommand
	{
		private string _pathArgument;

		public static CliCommand Parse(string[] args)
		{
			if (args.Length == 2)
			{
				return new OpenRepositoryCliCommand(args[1]);
			}
			return null;
		}

		public OpenRepositoryCliCommand(string pathArgument)
		{
			_pathArgument = pathArgument;
		}

		public override void Run(string workingDirectory)
		{
			try
			{
				string path;
				if (Path.IsPathRooted(_pathArgument))
				{
					path = _pathArgument;
				}
				else
				{
					workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
					path = Path.Combine(workingDirectory, _pathArgument);
				}
				path = Path.GetFullPath(path);
				Application.Current.TabManager()?.OpenRepository(path);
			}
			catch (Exception arg)
			{
				Log.Error($"Cannot open '{_pathArgument}'. {arg}");
			}
		}
	}
}
