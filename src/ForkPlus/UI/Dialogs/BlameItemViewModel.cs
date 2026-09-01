using System;
using System.ComponentModel;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public class BlameItemViewModel : INotifyPropertyChanged
	{
		public readonly Revision Revision;

		public UserIdentity Author => Revision.Author;

		public DateTime AuthorDate => Revision.AuthorDate;

		public string AbbreviatedSha => Revision.Sha.ToString().Substring(0, 6);

		public Sha RevisionSha => Revision.Sha;

		public string RevisionSubject => Revision.Message;

		public string FullCredentials => Revision.Author.Name + " <" + Revision.Author.Email + ">";

		public string ShaToolTip => "Navigate to '" + Revision.Sha.ToAbbreviatedString() + "'";

		public string OpenInSeparateWindowButtonToolTip => "Open '" + Revision.Sha.ToAbbreviatedString() + "' in separate window";

		/// <summary>
		/// 该 blame 块的 git-ai 行级归属（AI 生成部分）。null = 无 AI 归属（纯人类代码或 git-ai 未启用）。
		/// 由 BlameWindow.CreateBlameItems 按"当前提交新增行"匹配 git-ai diff 归属区间后设置。
		/// </summary>
		[Null]
		public GitAiLineAttribution AiAttribution { get; private set; }

		/// <summary>该块中带 AI 归属的行数。</summary>
		public int AiAttributedLineCount { get; private set; }

		/// <summary>该块总行数（用于 tooltip 的 "n of m lines" 部分归属描述）。</summary>
		public int TotalLineCount { get; private set; }

		/// <summary>是否有 AI 归属（控制徽标 Visibility）。</summary>
		public bool HasAiAttribution => AiAttribution != null;

		/// <summary>徽标可见性（无 AI 归属时折叠，不占布局空间）。</summary>
		public System.Windows.Visibility AiBadgeVisibility => AiAttribution != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

		/// <summary>AI 徽标文本，固定为 "AI"（tool/model 详情放 tooltip）。</summary>
		public string AiBadgeText => "AI";

		/// <summary>AI 归属 tooltip：生成工具/模型 + 部分归属行数 + 提交该代码的人类作者。</summary>
		[Null]
		public string AiBadgeToolTip
		{
			get
			{
				GitAiLineAttribution attribution = AiAttribution;
				if (attribution == null)
				{
					return null;
				}
				string text = Translate("AI-generated") + ": " + attribution.DisplayName;
				if (AiAttributedLineCount > 0 && TotalLineCount > AiAttributedLineCount)
				{
					text += "\n" + string.Format(Translate("{0} of {1} lines in this block"), AiAttributedLineCount, TotalLineCount);
				}
				if (!string.IsNullOrEmpty(attribution.HumanAuthor))
				{
					text += "\n" + string.Format(Translate("Committed by {0}"), attribution.HumanAuthor);
				}
				return text;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public BlameItemViewModel(Revision revision)
		{
			Revision = revision;
		}

		/// <summary>
		/// 设置 AI 归属信息（仅 BlameWindow.CreateBlameItems 调用）。
		/// </summary>
		/// <param name="attribution">命中的行级归属区间。</param>
		/// <param name="attributedLineCount">块内带归属的行数。</param>
		/// <param name="totalLineCount">块内总行数。</param>
		internal void SetAiAttribution(GitAiLineAttribution attribution, int attributedLineCount, int totalLineCount)
		{
			AiAttribution = attribution;
			AiAttributedLineCount = attributedLineCount;
			TotalLineCount = totalLineCount;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
