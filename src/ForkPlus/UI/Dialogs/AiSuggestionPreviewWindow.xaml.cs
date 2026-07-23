using Avalonia.Interactivity;
using System;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class AiSuggestionPreviewWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly string _file;

		private readonly string _oldText;

		private readonly string _newText;

		public AiSuggestionPreviewWindow(RepositoryUserControl repositoryUserControl, string file, string comment, string oldText, string newText)
		{
			_repositoryUserControl = repositoryUserControl;
			_file = file;
			_oldText = oldText;
			_newText = newText;
			InitializeComponent();
			base.DialogTitle = Translate("Suggestion Preview");
			base.DialogDescription = string.IsNullOrWhiteSpace(comment) ? file : file + "\n" + comment;
			base.SubmitButtonTitle = Translate("Apply suggestion");
			base.CancelButtonTitle = Translate("Close");
			base.Loaded += AiSuggestionPreviewWindow_Loaded;
		}

		private void AiSuggestionPreviewWindow_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				PreviewDiffControl.RepositoryUserControl = _repositoryUserControl;
				PreviewDiffControl.Content = GitCommandResult<DiffContent>.Success(CreateDiffContent(_repositoryUserControl, _file, _oldText, _newText));
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to render AI suggestion diff preview", ex);
				PreviewDiffControl.Hide();
				FallbackTextBox.Text = CreateFallbackText(_oldText, _newText);
				FallbackTextBox.Show();
			}
		}

		protected override void OnSubmit()
		{
			CloseWithOk();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private static string CreateFallbackText(string oldText, string newText)
		{
			return "Original:\n"
				+ (oldText ?? "")
				+ "\n\nSuggested:\n"
				+ (newText ?? "");
		}

		private static DiffContent CreateDiffContent(RepositoryUserControl repositoryUserControl, string file, string oldText, string newText)
		{
			ChangedFile changedFile = new ChangedFile(string.IsNullOrWhiteSpace(file) ? "suggestion-preview.txt" : file, StatusType.Modified, StatusType.None);
			string diff = CreateUnifiedDiff(changedFile.Path, oldText, newText);
			int tabWidth = repositoryUserControl?.GitModule?.Settings?.TabWidth ?? 4;
			return new TextDiffContent(changedFile, diff, tabWidth, entireFile: false);
		}

		private static string CreateUnifiedDiff(string file, string oldText, string newText)
		{
			string[] oldLines = SplitDiffLines(oldText, out bool oldEndsWithNewLine);
			string[] newLines = SplitDiffLines(newText, out bool newEndsWithNewLine);
			StringBuilder builder = new StringBuilder();
			string path = EscapeDiffPath(file);
			builder.Append("diff --git forkSrcPrefix/").Append(path).Append(" forkDstPrefix/").Append(path).AppendLine();
			builder.Append("index 0000000000000000000000000000000000000000..0000000000000000000000000000000000000000 100644").AppendLine();
			builder.Append("--- forkSrcPrefix/").Append(path).AppendLine();
			builder.Append("+++ forkDstPrefix/").Append(path).AppendLine();
			builder.Append("@@ -").Append(DiffRangeStart(oldLines.Length)).Append(",").Append(oldLines.Length)
				.Append(" +").Append(DiffRangeStart(newLines.Length)).Append(",").Append(newLines.Length)
				.Append(" @@").AppendLine();
			AppendLines(builder, "-", oldLines, oldEndsWithNewLine);
			AppendLines(builder, "+", newLines, newEndsWithNewLine);
			return builder.ToString();
		}

		private static string[] SplitDiffLines(string text, out bool endsWithNewLine)
		{
			string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
			endsWithNewLine = normalized.EndsWith("\n", StringComparison.Ordinal);
			if (normalized.Length == 0)
			{
				return new string[0];
			}
			string withoutFinalNewLine = endsWithNewLine ? normalized.Substring(0, normalized.Length - 1) : normalized;
			if (withoutFinalNewLine.Length == 0)
			{
				return new[] { "" };
			}
			return withoutFinalNewLine.Split('\n');
		}

		private static void AppendLines(StringBuilder builder, string prefix, string[] lines, bool endsWithNewLine)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				builder.Append(prefix).AppendLine(lines[i]);
				if (i == lines.Length - 1 && !endsWithNewLine)
				{
					builder.AppendLine("\\ No newline at end of file");
				}
			}
		}

		private static int DiffRangeStart(int lineCount)
		{
			return lineCount == 0 ? 0 : 1;
		}

		private static string EscapeDiffPath(string path)
		{
			return (path ?? "suggestion-preview.txt").Replace('\\', '/').Replace("\n", "_").Replace("\r", "_");
		}
	}
}
