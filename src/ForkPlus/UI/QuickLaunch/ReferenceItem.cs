using Avalonia;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.QuickLaunch
{
	public class ReferenceItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon
		{
			get
			{
				if (Reference is Tag)
				{
					return Application.Current.TryFindResource("TagIcon") as global::Avalonia.Media.IImage;
				}
				return Application.Current.TryFindResource("BranchIcon") as global::Avalonia.Media.IImage;
			}
		}

		public override global::Avalonia.Media.IImage SelectedIcon
		{
			get
			{
				if (Reference is Tag)
				{
					return Application.Current.TryFindResource("TagSelectedIcon") as global::Avalonia.Media.IImage;
				}
				return Application.Current.TryFindResource("BranchSelectedIcon") as global::Avalonia.Media.IImage;
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
