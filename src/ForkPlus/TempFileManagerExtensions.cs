using System;
using System.IO;
using ForkPlus.UI.Dialogs;

namespace ForkPlus
{
	public static class TempFileManagerExtensions
	{
		[Null]
		public static string CreateTemporaryFile(this TempFileManager tempFileManager, string filePath, MemoryStream fileContent, string suffix)
		{
			string path = Path.GetFileNameWithoutExtension(filePath) + "~" + suffix + Path.GetExtension(filePath);
			string tempFilePath = tempFileManager.GetTempFilePath(path);
			try
			{
				using FileStream stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
				fileContent?.WriteTo(stream);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write to '" + tempFilePath + "'", ex);
				new ErrorWindow(ex.ToString()).ShowDialog();
				return null;
			}
			return tempFilePath;
		}
	}
}
