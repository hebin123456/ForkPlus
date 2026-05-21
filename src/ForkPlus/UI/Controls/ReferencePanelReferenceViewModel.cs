using System.ComponentModel;

namespace ForkPlus.UI.Controls
{
	public abstract class ReferencePanelReferenceViewModel : INotifyPropertyChanged
	{
		public abstract string Name { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		protected void RaisePropertyChanged(string propertyName)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
