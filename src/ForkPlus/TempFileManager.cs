using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus
{
	public class TempFileManager : IDisposable
	{
		private readonly TempFileCollection _tempFileCollection = new TempFileCollection();

		private readonly object _directoryLock = new object();

		private readonly List<string> _tempDirectoryPaths = new List<string>();

		public static string MakeFilePath(string path)
		{
			return Path.Combine(Path.GetTempPath(), "ForkPlus", path);
		}

		public string GetTempFilePath(string path)
		{
			string text = MakeFilePath(path);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text));
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to create temp file path", ex);
			}
			AddFilePath(text);
			return text;
		}

		public void AddFilePath(string absolutePath)
		{
			foreach (object item in _tempFileCollection)
			{
				if (item as string == absolutePath)
				{
					return;
				}
			}
			try
			{
				_tempFileCollection.AddFile(absolutePath, keepFile: false);
			}
			catch (ArgumentException ex)
			{
				Log.Warn("Failed to add temp file path", ex);
			}
		}

		/// <summary>
		/// 在 %TEMP%\ForkPlus 下创建一个用于导出完整文件树等场景的临时目录，
		/// 并注册到托管列表（Dispose 时递归删除，语义对齐 AddFilePath 的临时文件）。
		/// </summary>
		public string GetTempDirectoryPath(string name)
		{
			string dir = MakeFilePath(name);
			try
			{
				Directory.CreateDirectory(dir);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to create temp directory path", ex);
			}
			AddDirectoryPath(dir);
			return dir;
		}

		public void AddDirectoryPath(string absolutePath)
		{
			lock (_directoryLock)
			{
				if (!_tempDirectoryPaths.Contains(absolutePath))
				{
					_tempDirectoryPaths.Add(absolutePath);
				}
			}
		}

		public void Dispose()
		{
			((IDisposable)_tempFileCollection).Dispose();
			string[] directories;
			lock (_directoryLock)
			{
				directories = _tempDirectoryPaths.ToArray();
				_tempDirectoryPaths.Clear();
			}
			string[] array = directories;
			for (int i = 0; i < array.Length; i++)
			{
				try
				{
					if (Directory.Exists(array[i]))
					{
						Directory.Delete(array[i], recursive: true);
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to delete temp directory '" + array[i] + "'", ex);
				}
			}
		}
	}
}
