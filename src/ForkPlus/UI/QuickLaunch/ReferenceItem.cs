using System.Windows;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.QuickLaunch
{
	public class ReferenceItem : CommandProviderItem
	{
		public override ImageSource Icon
		{
			get
			{
				if (Reference is Tag)
				{
					return Application.Current.TryFindResource("TagIcon") as ImageSource;
				}
				return Application.Current.TryFindResource("BranchIcon") as ImageSource;
			}
		}

		public override ImageSource SelectedIcon
		{
			get
			{
				if (Reference is Tag)
				{
					return Application.Current.TryFindResource("TagSelectedIcon") as ImageSource;
				}
				return Application.Current.TryFindResource("BranchSelectedIcon") as ImageSource;
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
