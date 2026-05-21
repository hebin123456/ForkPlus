using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerTreeViewItem : MultiselectionTreeViewItem
	{
		private bool _isInEditMode;

		public bool IsInEditMode
		{
			get
			{
				return _isInEditMode;
			}
			set
			{
				if (_isInEditMode != value)
				{
					_isInEditMode = value;
					RaisePropertyChanged("IsInEditMode");
				}
			}
		}

		public RepositoryManagerTreeViewItem Parent { get; }

		public RepositoryManagerTreeViewItem(RepositoryManagerTreeViewItem parent)
		{
			Parent = parent;
		}
	}
}
