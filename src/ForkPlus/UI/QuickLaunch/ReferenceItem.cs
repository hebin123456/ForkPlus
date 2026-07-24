// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
// - Application.Current.TryFindResource(key) as ImageSource → Theme.FindImage(key)
using Avalonia;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.QuickLaunch
{
	public class ReferenceItem : CommandProviderItem
	{
		public override IImage Icon
		{
			get
			{
				if (Reference is Tag)
				{
					return Theme.FindImage("TagIcon");
				}
				return Theme.FindImage("BranchIcon");
			}
		}

		public override IImage SelectedIcon
		{
			get
			{
				if (Reference is Tag)
				{
					return Theme.FindImage("TagSelectedIcon");
				}
				return Theme.FindImage("BranchSelectedIcon");
			}
		}

		public Reference Reference { get; }

		public ReferenceItem(Reference reference, string fuzzySearchString)
			: base(reference, reference.Name, "")
		{
			Reference = reference;
			base.FuzzySearchString = fuzzySearchString;
		}
	}
}
