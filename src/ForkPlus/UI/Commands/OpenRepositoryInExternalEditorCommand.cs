using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInExternalEditorCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open In External Editor";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] GitModule gitModule, [Null] ExternalRepositoryEditor editor)
		{
			if (gitModule != null && editor != null)
			{
				ExecuteInternal(gitModule.Path, editor);
			}
		}

		public void Execute([Null] string path, [Null] ExternalRepositoryEditor editor)
		{
			if (path != null && editor != null)
			{
				ExecuteInternal(path, editor);
			}
		}

		private void ExecuteInternal(string path, ExternalRepositoryEditor editor)
		{
			string text = Environment.ExpandEnvironmentVariables(editor.ApplicationPath);
			if (!File.Exists(text))
			{
				Log.Error("Cannot find " + editor.Name + " at '" + text + "'");
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find {0} at '{1}'", editor.Name, text)).ShowDialog();
				return;
			}
			Process process = new Process();
			process.StartInfo = CreateProcessStartInfo(path, editor, text);
			Log.Info("Running '" + process.StartInfo.FileName + " " + process.StartInfo.Arguments + "'");
			try
			{
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to start external project editor process", ex);
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot run '{0} {1}'.\n{2}", text, path, ex.ToString())).ShowDialog();
			}
		}

		private static ProcessStartInfo CreateProcessStartInfo(string path, ExternalRepositoryEditor editor, string applicationPath)
		{
			if (editor is ExternalRepositoryEditor.OpenCode)
			{
				return new ProcessStartInfo
				{
					FileName = applicationPath,
					Arguments = path.Quotify(),
					WorkingDirectory = path,
					UseShellExecute = true
				};
			}
			return new ProcessStartInfo
			{
				FileName = applicationPath,
				Arguments = path.Quotify()
			};
		}
	}
}
