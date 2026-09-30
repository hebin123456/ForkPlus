using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// v3.13.0（WS5/WS2.2）：提交预览区块，PullWindow 拉取预览 / PushWindow 强推覆盖预览 /
	/// ResetBranchWindow 丢失提交预览共用。一行摘要文案（由调用方本地化后经 SetCommits 传入）
	/// + Expander（默认收起，展开后每行一个提交，列表限高滚动）。
	/// “Show commits” 走语言键（key=英文原文），由宿主窗口 Loaded 时 PreferencesLocalization
	/// Apply 递归翻译。v3（2026-09-30）：折叠头局部扁平主题（与摘要行同左缘）；sha 与 subject
	/// 同字体对齐；sha 为超链接，点击经 ShowRevisionInSeparateWindowCommand 打开
	/// RevisionDetailsWindow 变更详情面板（完整 SHA 由 GetCommitsBetweenGitCommand 随行携带）。
	/// </summary>
	public partial class CommitsPreviewSection : UserControl
	{
		public class Item
		{
			public string Sha { get; }

			/// <summary>完整 40 位 SHA，供点击打开 RevisionDetailsWindow；缺省时点击不动作。</summary>
			public string FullSha { get; }

			public string Subject { get; }

			public Item(string sha, string subject, string fullSha = null)
			{
				Sha = sha;
				Subject = subject;
				FullSha = fullSha;
			}
		}

		public CommitsPreviewSection()
		{
			InitializeComponent();
		}

		/// <summary>宿主注入的仓库控件：提供 GitModule 与 RevisionDetailsWindow 上下文，
		/// 供 sha 链接打开变更详情面板；为 null 时点击不动作。</summary>
		public RepositoryUserControl RepositoryUserControl { get; set; }

		/// <summary>设置摘要文案与提交列表（新→旧）。重新赋值会收起 Expander；
		/// 空列表时隐藏 Expander（仅保留摘要行）。</summary>
		public void SetCommits(string summaryText, IReadOnlyList<Item> commits)
		{
			SummaryTextBlock.Text = summaryText;
			CommitsExpander.IsExpanded = false;
			CommitsItemsControl.ItemsSource = commits ?? Array.Empty<Item>();
			CommitsExpander.IsVisible = commits != null && commits.Count > 0;
		}

		/// <summary>sha 超链接点击：经 ShowRevisionInSeparateWindowCommand 打开 RevisionDetailsWindow。
		/// 完整 SHA 经 Tag 传入（短 SHA 无法 Sha.Parse）；FullSha 缺失或宿主未注入仓库控件时不动作。</summary>
		private void ShaText_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			string fullSha = (sender as TextBlock)?.Tag as string;
			Sha? sha = ((fullSha != null) ? Sha.Parse(fullSha) : null);
			if (sha.HasValue && RepositoryUserControl != null)
			{
				new ShowRevisionInSeparateWindowCommand().Execute(RepositoryUserControl, new RevisionDiffTarget.Revision(sha.Value), null);
			}
		}
	}
}
