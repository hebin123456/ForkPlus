using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanel : ItemsControl
	{
		public void Refresh(IReadOnlyList<Reference> references, Remote[] remotes)
		{
			ReferencePanelReferenceViewModel[] array = new ReferencePanelReferenceViewModel[references.Count];
			for (int i = 0; i < references.Count; i++)
			{
				Reference reference = references[i];
				if (!(reference is LocalBranch localBranch))
				{
					RemoteBranch remoteBranch = reference as RemoteBranch;
					if (remoteBranch == null)
					{
						if (!(reference is Tag tag))
						{
							if (reference is BisectMark bisectMark)
							{
								array[i] = new ReferencePanelBisectMarkViewModel(bisectMark);
							}
						}
						else
						{
							array[i] = new ReferencePanelTagViewModel(tag);
						}
					}
					else
					{
						ImageSource remoteIcon = IReadOnlyListExtensions.FirstItem(remotes, (Remote x) => x.Name == remoteBranch.Remote)?.GetIconImage();
						array[i] = new ReferencePanelRemoteBranchViewModel(remoteBranch, remoteIcon);
					}
				}
				else
				{
					array[i] = new ReferencePanelLocalBranchViewModel(localBranch);
				}
			}
			base.ItemsSource = array;
		}
	}
}
