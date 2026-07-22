using System;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/GitOutputColorizer.cs（347 行）：
    //   - public class GitOutputColorizer : DocumentColorizingTransformer
    //   - 按 git 输出行内容分类（command / important / hint / fork hint / noise / warning / error / default）
    //     设置前景色，部分类别加粗
    //   - light/dark 两套画刷，按 ForkPlusSettings.Default.Theme 切换
    //   - FilesChangedRegex 匹配 " N files? changed" 行
    //   - BoldTypeface 用 FontConstants.MonospaceFontFamily + Bold
    //
    // Avalonia 版差异：
    //   1. 基类 DocumentColorizingTransformer → AvaloniaEdit.Rendering（API 一致）
    //   2. System.Windows.Media SolidColorBrush / Color / Brushes → Avalonia.Media（同名）
    //   3. System.Windows.Media.ColorConverter.ConvertFromString("#RRGGBB") →
    //      Avalonia.Media.Color.Parse("#RRGGBB")
    //   4. brush.Freeze() 删除（Avalonia Brush immutable）
    //   5. WPF Typeface(FontFamily, FontStyle, FontWeight, FontStretch, FontFamily fallback) →
    //      Avalonia Typeface(FontFamily, FontStyle, FontWeight, FontStretch)（无 fallback 参数）
    //   6. WPF FontStyles.Normal / FontWeights.Bold / FontStretches.Normal →
    //      Avalonia FontStyle.Normal / FontWeight.Bold / FontStretch.Normal
    //   7. AvalonEdit CurrentContext.GetText / StringSegment / VisualLineElement → AvaloniaEdit 同名
    //   8. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    public class GitOutputColorizer : DocumentColorizingTransformer
    {
        private static readonly Regex FilesChangedRegex;

        private static readonly IBrush _hintBrushLight;
        private static readonly IBrush _errorBrushLight;
        private static readonly IBrush _warningBrushLight;
        private static readonly IBrush _commandRequestBrushLight;
        private static readonly IBrush _defaultBrushLight;
        private static readonly IBrush _noiseBrushLight;

        private static readonly IBrush _hintBrushDark;
        private static readonly IBrush _errorBrushDark;
        private static readonly IBrush _warningBrushDark;
        private static readonly IBrush _commandRequestBrushDark;
        private static readonly IBrush _defaultBrushDark;
        private static readonly IBrush _noiseBrushDark;

        private static Typeface BoldTypeface => new Typeface(FontConstants.MonospaceFontFamily, FontStyle.Normal, FontWeight.Bold, FontStretch.Normal);

        static GitOutputColorizer()
        {
            FilesChangedRegex = new Regex("^ \\d* files? changed", RegexOptions.Multiline | RegexOptions.Compiled);
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

        private static IBrush Brush(IBrush light, IBrush dark)
        {
            if (ForkPlusSettings.Default.Theme != 0)
            {
                return dark;
            }
            return light;
        }
    }
}
