using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus
{
	public class TempFileManager : IDisposable
	{
		private readonly List<string> _tempFiles = new List<string>();
		private bool _disposed;

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
			if (_tempFiles.Contains(absolutePath))
			{
				return;
			}
			_tempFiles.Add(absolutePath);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}
			if (disposing)
			{
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
						Log.Warn("Failed to delete temp file: " + path, ex);
					}
				}
				_tempFiles.Clear();
			}
			_disposed = true;
		}
	}
}
