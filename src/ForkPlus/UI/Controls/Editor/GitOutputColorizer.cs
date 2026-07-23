using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace ForkPlus.UI.Controls.Editor
{
	public class GitOutputColorizer : DocumentColorizingTransformer
	{
		private static readonly Regex FilesChangedRegex;

		private static readonly SolidColorBrush _hintBrushLight;

		private static readonly SolidColorBrush _errorBrushLight;

		private static readonly SolidColorBrush _warningBrushLight;

		private static readonly SolidColorBrush _commandRequestBrushLight;

		private static readonly SolidColorBrush _defaultBrushLight;

		private static readonly SolidColorBrush _noiseBrushLight;

		private static readonly SolidColorBrush _hintBrushDark;

		private static readonly SolidColorBrush _errorBrushDark;

		private static readonly SolidColorBrush _warningBrushDark;

		private static readonly SolidColorBrush _commandRequestBrushDark;

		private static readonly SolidColorBrush _defaultBrushDark;

		private static readonly SolidColorBrush _noiseBrushDark;

		private static Typeface BoldTypeface => new Typeface(FontConstants.MonospaceFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

		static GitOutputColorizer()
		{
			FilesChangedRegex = new Regex("^ \\d* files? changed", RegexOptions.Multiline | RegexOptions.Compiled);
			// 阶段 4 里程碑 4.7-a：ColorConverter.ConvertFromString → Avalonia Color.Parse；
			// 移除 brush.Freeze()（Avalonia 画刷默认不可变）。
			_hintBrushLight = new SolidColorBrush(Color.Parse("#54A353"));
			_errorBrushLight = new SolidColorBrush(Color.Parse("#000000"));
			_warningBrushLight = new SolidColorBrush(Color.Parse("#FF9000"));
			_commandRequestBrushLight = new SolidColorBrush(Color.Parse("#000000"));
			_defaultBrushLight = new SolidColorBrush(Color.Parse("#595959"));
			_noiseBrushLight = new SolidColorBrush(Color.Parse("#9F9797"));
			_hintBrushDark = new SolidColorBrush(Color.Parse("#98C278"));
			_errorBrushDark = new SolidColorBrush(Color.Parse("#FFFFFF"));
			_warningBrushDark = new SolidColorBrush(Color.Parse("#FFCB00"));
			_commandRequestBrushDark = new SolidColorBrush(Color.Parse("#FFFFFF"));
			_defaultBrushDark = new SolidColorBrush(Color.Parse("#DADADA"));
			_noiseBrushDark = new SolidColorBrush(Color.Parse("#ABABAB"));
		}

		protected override void ColorizeLine(DocumentLine documentLine)
		{
			string text2;
			try
			{
				StringSegment text = base.CurrentContext.GetText(documentLine.Offset, documentLine.Length);
				if (text.Count == 0)
				{
					return;
				}
				text2 = text.Text;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get line substring in current context", ex);
				return;
			}
			if (IsCommandRequest(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightCommandRequestLine);
			}
			else if (IsImportant(text2))
			{
				if (EmphasiseWithBold(text2))
				{
					ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightImportantLineBold);
				}
				else
				{
					ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightImportantLine);
				}
			}
			else if (IsHint(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightHintRequestLine);
			}
			else if (IsForkHint(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightForkHintLine);
			}
			else if (IsNoise(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightNoiseRequestLine);
			}
			else if (IsWarning(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightWarningRequestLine);
			}
			else if (IsError(text2))
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightErrorRequestLine);
			}
			else
			{
				ChangeLinePart(documentLine.Offset, documentLine.EndOffset, HighlightDefaultLine);
			}
		}

		private static bool IsCommandRequest(string line)
		{
			if (line.StartsWith("$ "))
			{
				return true;
			}
			return false;
		}

		private static bool IsImportant(string line)
		{
			if (FilesChangedRegex.IsMatch(line))
			{
				return true;
			}
			if (line.StartsWith("Switched to "))
			{
				return true;
			}
			if (line.StartsWith("CONFLICT "))
			{
				return true;
			}
			if (line.StartsWith(" ! [rejected]"))
			{
				return true;
			}
			if (line.StartsWith(" * [new branch]"))
			{
				return true;
			}
			if (line.StartsWith(" - [deleted]"))
			{
				return true;
			}
			if (line.StartsWith(" + "))
			{
				return true;
			}
			if (line.StartsWith("Everything up-to-date"))
			{
				return true;
			}
			if (line.EndsWith("is the first bad commit"))
			{
				return true;
			}
			return false;
		}

		private static bool EmphasiseWithBold(string line)
		{
			if (FilesChangedRegex.IsMatch(line))
			{
				return true;
			}
			if (line.StartsWith("Switched to "))
			{
				return true;
			}
			if (line.EndsWith("is the first bad commit"))
			{
				return true;
			}
			return false;
		}

		private static bool IsHint(string line)
		{
			if (line.StartsWith("hint: "))
			{
				return true;
			}
			return false;
		}

		private static bool IsForkHint(string line)
		{
			if (line.StartsWith("fork:"))
			{
				return true;
			}
			return false;
		}

		private static bool IsNoise(string line)
		{
			if (line.StartsWith(" = [up to date]"))
			{
				return true;
			}
			if (line.StartsWith("Enumerating objects:"))
			{
				return true;
			}
			if (line.StartsWith("Delta compression using"))
			{
				return true;
			}
			if (line.StartsWith("Total "))
			{
				return true;
			}
			if (line.StartsWith("POST git-upload-pack"))
			{
				return true;
			}
			if (line.StartsWith("POST git-receive-pack"))
			{
				return true;
			}
			if (line.StartsWith("Auto-merging "))
			{
				return true;
			}
			if (line.StartsWith("  (use \"git "))
			{
				return true;
			}
			return false;
		}

		private static bool IsWarning(string line)
		{
			if (line.StartsWith("warning: "))
			{
				return true;
			}
			return false;
		}

		private static bool IsError(string line)
		{
			if (line.StartsWith("git:"))
			{
				return true;
			}
			if (line.StartsWith("error: "))
			{
				return true;
			}
			if (line.StartsWith("fatal: "))
			{
				return true;
			}
			if (line.StartsWith("ssh: "))
			{
				return true;
			}
			if (line.StartsWith("Automatic merge failed;"))
			{
				return true;
			}
			return false;
		}

		private static void HighlightCommandRequestLine(VisualLineElement element)
		{
			MakeBold(element);
			element.TextRunProperties.SetForegroundBrush(Brush(_commandRequestBrushLight, _commandRequestBrushDark));
		}

		private static void HighlightImportantLine(VisualLineElement element)
		{
			element.TextRunProperties.SetForegroundBrush(Brush(_commandRequestBrushLight, _commandRequestBrushDark));
		}

		private static void HighlightImportantLineBold(VisualLineElement element)
		{
			MakeBold(element);
			element.TextRunProperties.SetForegroundBrush(Brush(_commandRequestBrushLight, _commandRequestBrushDark));
		}

		private static void HighlightHintRequestLine(VisualLineElement element)
		{
			element.TextRunProperties.SetForegroundBrush(Brush(_hintBrushLight, _hintBrushDark));
		}

		private static void HighlightForkHintLine(VisualLineElement element)
		{
			MakeBold(element);
			element.TextRunProperties.SetForegroundBrush(Brush(_hintBrushLight, _hintBrushDark));
		}

		private static void HighlightNoiseRequestLine(VisualLineElement element)
		{
			element.TextRunProperties.SetForegroundBrush(Brush(_noiseBrushLight, _noiseBrushDark));
		}

		private static void HighlightWarningRequestLine(VisualLineElement element)
		{
			element.TextRunProperties.SetForegroundBrush(Brush(_warningBrushLight, _warningBrushDark));
		}

		private static void HighlightErrorRequestLine(VisualLineElement element)
		{
			MakeBold(element);
			element.TextRunProperties.SetForegroundBrush(Brush(_errorBrushLight, _errorBrushDark));
		}

		private static void HighlightDefaultLine(VisualLineElement element)
		{
			element.TextRunProperties.SetForegroundBrush(Brush(_defaultBrushLight, _defaultBrushDark));
		}

		private static void MakeBold(VisualLineElement element)
		{
			element.TextRunProperties.SetTypeface(BoldTypeface);
		}

		private static SolidColorBrush Brush(SolidColorBrush light, SolidColorBrush dark)
		{
			if (ForkPlusSettings.Default.Theme != 0)
			{
				return dark;
			}
			return light;
		}
	}
}
