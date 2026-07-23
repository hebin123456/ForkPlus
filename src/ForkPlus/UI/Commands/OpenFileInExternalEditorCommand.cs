using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace ForkPlus.UI.Commands
{
	public class OpenFileInExternalEditorCommand
	{
		public void AddMenuItems(RepositoryUserControl repositoryUserControl, DiffCodeEditor diffCodeEditor, ContextMenu menu, string path)
		{
			VisualPatch visualPatch = diffCodeEditor.VisualPatch;
			if (visualPatch == null)
			{
				return;
			}
			int? charIndexUnderMousePointer = GetCharIndexUnderMousePointer(diffCodeEditor);
			if (charIndexUnderMousePointer.HasValue)
			{
				int valueOrDefault = charIndexUnderMousePointer.GetValueOrDefault();
				charIndexUnderMousePointer = visualPatch.GetVisualLineNumber(valueOrDefault);
				if (charIndexUnderMousePointer.HasValue)
				{
					int valueOrDefault2 = charIndexUnderMousePointer.GetValueOrDefault();
					AddMenuItems(repositoryUserControl, menu, path, valueOrDefault2);
				}
			}
		}

		public void AddMenuItems(RepositoryUserControl repositoryUserControl, TextContentControl textContentControl, ContextMenu menu, string path)
		{
			int? charIndexUnderMousePointer = GetCharIndexUnderMousePointer(textContentControl);
			if (charIndexUnderMousePointer.HasValue)
			{
				int valueOrDefault = charIndexUnderMousePointer.GetValueOrDefault();
				charIndexUnderMousePointer = GetVisualLineNumber(textContentControl, valueOrDefault);
				if (charIndexUnderMousePointer.HasValue)
				{
					int valueOrDefault2 = charIndexUnderMousePointer.GetValueOrDefault();
					AddMenuItems(repositoryUserControl, menu, path, valueOrDefault2);
				}
			}
		}

		private void AddMenuItems(RepositoryUserControl repositoryUserControl, ContextMenu menu, string path, int originalLineNumber)
		{
			ExternalTool[] array = ExternalToolManager.RevealAvailableFileEditorTools();
			if (array.Length == 1)
			{
				ExternalTool editor2 = array[0];
				menu.AddMenuItem("Reveal Line in " + array[0].Name, delegate
				{
					Execute(repositoryUserControl, editor2, path, originalLineNumber);
				});
			}
			else
			{
				if (array.Length <= 1)
				{
					return;
				}
				MenuItem menuItem = new MenuItem
				{
					Header = Translate("Reveal Line in").Replace("_", "__")
				};
				ExternalTool[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					ExternalTool editor = array2[i];
					MenuItem menuItem2 = new MenuItem();
					menuItem2.Header = editor.Name ?? "";
					menuItem2.Icon = new Image
					{
						Source = IconTools.GetImageSourceForFile(editor.Path)
					};
					menuItem2.Click += delegate
					{
						Execute(repositoryUserControl, editor, path, originalLineNumber);
					};
					menuItem.Items.Add(menuItem2);
				}
				menu.Items.Add(menuItem);
			}
		}

		private void Execute(RepositoryUserControl repositoryUserControl, ExternalTool editor, string path, int line)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string editorPath = Environment.ExpandEnvironmentVariables(editor.Path);
			if (!File.Exists(editorPath))
			{
				Log.Error("Cannot find external tool at '" + editorPath + "'");
				new ErrorWindow(ServiceLocator.Localization.FormatCurrent("Cannot find external tool at '{0}'", editorPath)).ShowDialog();
				return;
			}
			string filePath = PathHelper.Normalize(gitModule.MakePath(path));
			string argumentsString = string.Join(" ", editor.Arguments.Map((string x) => x.Replace("$FILEPATH", filePath).Replace("$LINE", $"{line}")));
			repositoryUserControl.JobQueue.Add(Translate("External tool"), delegate(JobMonitor monitor)
			{
				Process process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = editorPath,
						Arguments = argumentsString
					}
				};
				Log.Info("Running '" + editorPath + " " + argumentsString + "'");
				monitor.AppendOutputLine("$ " + editorPath + " " + argumentsString);
				try
				{
					process.Start();
				}
				catch (Exception ex)
				{
					Log.Error("Failed to start external tool '" + editorPath + " " + argumentsString + "'", ex);
					new ErrorWindow($"Cannot run '{editorPath}'.\n{ex}").ShowDialog();
				}
			});
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		[Null]
		private int? GetCharIndexUnderMousePointer(CodeEditor editor)
		{
			Point position = Mouse.GetPosition(editor);
			if (VisualTreeHelper.HitTest(editor, position) == null)
			{
				return null;
			}
			TextViewPosition? positionFromPoint = editor.GetPositionFromPoint(position);
			if (!positionFromPoint.HasValue)
			{
				return null;
			}
			TextLocation location = positionFromPoint.Value.Location;
			return editor.Document.GetOffset(location);
		}

		[Null]
		private int? GetVisualLineNumber(CodeEditor editor, int charIndex)
		{
			if (charIndex < editor.Document.TextLength)
			{
				return editor.Document.GetLineByOffset(charIndex).LineNumber;
			}
			return null;
		}
	}
}
