using System.ComponentModel;
using System.Diagnostics;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	[DebuggerDisplay("{Reference.FullReference}")]
	public abstract class ReferenceViewModel : INotifyPropertyChanged
	{
		private static readonly PropertyChangedEventArgs SearchStringChangedEventArgs = new PropertyChangedEventArgs("SearchString");

		[Null]
		private string _searchString;

		public abstract Reference Reference { get; }

		public int ActiveGraphColumn { get; }

		[Null]
		public string SearchString
		{
			get
			{
				return _searchString;
			}
			set
			{
				if (!(_searchString == value))
				{
					_searchString = value;
					this.PropertyChanged?.Invoke(this, SearchStringChangedEventArgs);
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public ReferenceViewModel(int graphColumn)
		{
			ActiveGraphColumn = graphColumn;
		}

		protected void RaisePropertyChanged(PropertyChangedEventArgs args)
		{
			this.PropertyChanged?.Invoke(this, args);
		}
	}
}
