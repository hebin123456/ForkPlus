using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus
{
	public class TempFileManager : IDisposable
	{
		// 阶段 5：原 System.CodeDom.Compiler.TempFileCollection 在 net10.0 下被类型转发到
		// System.CodeDom 程序集（CS1069）。为避免新增 PackageReference，改用 List<string>
		// 跟踪临时文件路径，Dispose 时手动删除（等价于原 TempFileCollection(keepFile:false) 语义）。
		private readonly List<string> _tempFiles = new List<string>();

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
			// 阶段 5：去重遍历保持原 TempFileCollection 语义。
			foreach (string item in _tempFiles)
			{
				if (item == absolutePath)
				{
					return;
				}
			}
			try
			{
				_tempFiles.Add(absolutePath);
			}
			catch (ArgumentException ex)
			{
				Log.Warn("Failed to add temp file path", ex);
			}
		}

		public void Dispose()
		{
			// 阶段 5：等价于原 TempFileCollection.Dispose() —— 删除所有跟踪的临时文件。
			foreach (string path in _tempFiles)
			{
				try
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to delete temp file", ex);
				}
			}
			_tempFiles.Clear();
		}
	}
}
