// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF ItemsControl → Avalonia.Controls.ItemsControl。WPF System.Windows.Media.ImageSource → Avalonia.Media.IImage。
// WPF ItemsControl.ItemsSource → Avalonia ItemsControl.ItemsSource（API 兼容）。
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
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
						// TODO(4.5): Remote.Icon（定义于 Remote.partial.cs，尚未迁移）仍返回 WPF System.Windows.Media.ImageSource。
						// 待 Remote.partial.cs 迁移为 Avalonia.Media.IImage 后，此赋值类型与 ReferencePanelRemoteBranchViewModel(IImage) 一致。
						IImage remoteIcon = IReadOnlyListExtensions.FirstItem(remotes, (Remote x) => x.Name == remoteBranch.Remote)?.Icon;
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
