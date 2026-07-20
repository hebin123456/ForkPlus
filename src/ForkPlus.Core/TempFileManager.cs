using System;
using System.CodeDom.Compiler;
using System.IO;

namespace ForkPlus
{
	public class TempFileManager : IDisposable
	{
		private readonly TempFileCollection _tempFileCollection = new TempFileCollection();

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

		public void Dispose()
		{
			((IDisposable)_tempFileCollection).Dispose();
		}
	}
}
