using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionsHeaderUserControl : UserControl
	{

		public RevisionsHeaderUserControl()
		{
			InitializeComponent();
		}

		public void SetSubmoduleRevisions(SubmoduleDiffContent submoduleDiffContent)
		{
			OtherRevisionDetailsContainer.Show();
			SwapRevisionsButton.Hide();
			Revision srcRevision = submoduleDiffContent.SrcRevision;
			if (srcRevision != null)
			{
				Revision dstRevision = submoduleDiffContent.DstRevision;
				if (dstRevision != null)
				{
					UpdateControls(dstRevision, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.AddedBrush);
					UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.RemovedBrush);
				}
				else
				{
					UpdateControls(null, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.AddedBrush, GetCustomLabelString(submoduleDiffContent.DstSha));
					UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.RemovedBrush);
				}
			}
			else
			{
				Revision dstRevision2 = submoduleDiffContent.DstRevision;
				if (dstRevision2 != null)
				{
					UpdateControls(dstRevision2, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.AddedBrush);
					UpdateControls(null, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.RemovedBrush, GetCustomLabelString(submoduleDiffContent.SrcSha));
				}
				else
				{
					UpdateControls(null, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.AddedBrush, GetCustomLabelString(submoduleDiffContent.DstSha));
					UpdateControls(null, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, submoduleDiffContent.Bugtrackers, Theme.Diff.RemovedBrush, GetCustomLabelString(submoduleDiffContent.SrcSha));
				}
			}
		}

		private string GetCustomLabelString(Sha sha)
		{
			if (!(sha == Sha.Zero))
			{
				return sha.ToAbbreviatedString();
			}
			return "null";
		}

		public void SetRevisions(Revision revision, BugtrackerLinkDefinition[] bugtrackers, [Null] RevisionDetails srcRevision = null, bool compareToWorkingDirectory = false)
		{
			if (compareToWorkingDirectory)
			{
				OtherRevisionDetailsContainer.Show();
				UpdateControls(revision, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, bugtrackers, Theme.Diff.RemovedBrush);
				UpdateControls(revision, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, bugtrackers, Theme.Diff.AddedBrush, "Local Changes");
				SwapRevisionsButton.Disable();
			}
			else if (srcRevision != null)
			{
				OtherRevisionDetailsContainer.Show();
				SwapRevisionsButton.Enable();
				UpdateControls(revision, OtherAuthorAvatarImage, OtherAuthorTextBlock, OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder, OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock, OtherCustomTextBlockBorder, OtherCustomTextBlock, bugtrackers, Theme.Diff.AddedBrush);
				UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, bugtrackers, Theme.Diff.RemovedBrush);
			}
			else
			{
				OtherRevisionDetailsContainer.Hide();
				UpdateControls(revision, AuthorAvatarImage, AuthorTextBlock, AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder, SubjectTextBlock, DescriptionSymbolTextBlock, CustomTextBlockBorder, CustomTextBlock, bugtrackers);
			}
		}

		private static void UpdateControls(Revision revision, AvatarImage authorAvatarImage, TextBlock authorTextBlock, TextBlock authorDateTextBlock, TextBlock shaTextBlock, Border shaBackgroundBorder, TextBlock subjectTextBlock, TextBlock descriptionSymbolTextBlock, Border customTextBlockBorder, TextBlock customTextBlock, BugtrackerLinkDefinition[] bugtrackers, [Null] Brush brush = null, [Null] string customTextBlockText = null)
		{
			if (customTextBlockText != null)
			{
				customTextBlockBorder.Show();
				authorTextBlock.Collapse();
				authorDateTextBlock.Collapse();
				shaTextBlock.Collapse();
				shaBackgroundBorder.Collapse();
				subjectTextBlock.Collapse();
				authorAvatarImage.UserIdentity = new UserIdentity("", "");
				customTextBlock.Text = customTextBlockText;
				customTextBlockBorder.Background = brush;
			}
			else
			{
				customTextBlockBorder.Collapse();
				authorTextBlock.Show();
				authorDateTextBlock.Show();
				shaTextBlock.Show();
				shaBackgroundBorder.Show();
				subjectTextBlock.Show();
				authorAvatarImage.UserIdentity = revision.Author;
				authorTextBlock.Text = revision.Author.Name;
				authorDateTextBlock.Text = revision.AuthorDate.ToString(Consts.NormalDateTimeFormat);
				shaTextBlock.Text = revision.Sha.ToAbbreviatedString();
				shaBackgroundBorder.Background = brush;
				revision.MessageParts(out var subject, out var description);
				subjectTextBlock.Text = subject;
				subjectTextBlock.ApplySearchAndButrackerHighlighting(null, bugtrackers);
				subjectTextBlock.ToolTip = revision.Message.TrimEnd();
				descriptionSymbolTextBlock.Visibility = ((!(description != "")) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

	}
}
