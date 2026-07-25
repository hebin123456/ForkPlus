using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
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
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Avalonia;

namespace ForkPlus.UI.Commands
{
	// 阶段 4.5：WPF Mouse.GetPosition + VisualTreeHelper.HitTest
	// → Avalonia 通过 InputElement.GetPosition + IVisual.GetVisualAt。
	// WPF ContextMenu → Avalonia.Controls.ContextMenu。
	// WPF MenuItem/Image → Avalonia.Controls.MenuItem/Image。
	public class OpenFileInExternalEditorCommand
	{
		// 阶段 4.5：缓存最近一次指针位置（替代 WPF 静态 Mouse.GetPosition）。
		// 调用方在 PointerPressed/PointerMoved 中更新此字段，菜单弹出时读取。
		// TODO(4.5-n): 调用方需在 ContextMenuOpening 前更新 LastPointerPosition。
		[Null]
		public Point? LastPointerPosition { get; set; }

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
			// 阶段 4.5：WPF Mouse.GetPosition(editor) → 通过 LastPointerPosition 缓存。
			if (!LastPointerPosition.HasValue)
			{
				return null;
			}
			Point position = LastPointerPosition.Value;
			// 阶段 4.5：WPF VisualTreeHelper.HitTest(editor, position)
			// → Avalonia IVisual.GetVisualAt(point)。
			IVisual visual = editor.GetVisualAt(position);
			if (visual == null)
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
