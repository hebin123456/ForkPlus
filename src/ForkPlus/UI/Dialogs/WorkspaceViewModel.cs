using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs
{
	public class WorkspaceViewModel : INotifyPropertyChanged
	{
		private const string DefaultName = "New Workspace";

		[Null]
		private readonly Workspace _workspace;

		private string _name;

		private bool _isInEditMode;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					_name = _workspace?.Name ?? "New Workspace";
				}
				else
				{
					_name = value;
				}
				NotifyPropertyChanged("Name");
				NotifyPropertyChanged("DisplayName");
			}
		}

		public string DisplayName
		{
			get
			{
				return _name;
			}
			set
			{
				Name = value;
			}
		}

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
					NotifyPropertyChanged("IsInEditMode");
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public WorkspaceViewModel(string name = "New Workspace")
		{
			Name = name;
		}

		public WorkspaceViewModel(Workspace workspace)
		{
			_workspace = workspace;
			Name = workspace.Name;
		}

		public Workspace CreateWorkspace()
		{
			string[] array = _workspace?.Repositories ?? new string[0];
			string activeRepository = _workspace?.ActiveRepository ?? array.FirstItem();
			return new Workspace(Name, array, activeRepository);
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

	}
}
