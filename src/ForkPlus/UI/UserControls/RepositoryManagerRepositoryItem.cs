using System.Windows.Media;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerRepositoryItem : RepositoryManagerTreeViewItem
	{
		private const string GitMmPrefix = "git mm: ";

		private readonly string _name;

		private readonly bool _isGitMmWorkspace;

		private ImageSource _repositoryIcon;

		public string Name
		{
			get
			{
				return FormatName(_name);
			}
			set
			{
				string newName = NormalizeName(value);
				if (!(_name == newName))
				{
					base.Title = FormatName(newName);
					base.IsInEditMode = false;
					RepositoryManager.Instance.RenameRepository(Repository.Path, newName);
					RepositoryManager.Instance.Save();
					NotificationCenter.Current.RaiseRepositoryNameChanged(this, Repository.Path);
				}
			}
		}

		public string Path => Repository.Path;

		public RepositoryManager.Repository Repository { get; }

		[Null]
		public SolidColorBrush RepositoryColor => RepositoryColorsUserControl.GetBrush(Repository.Color);

		public ImageSource RepositoryIcon
		{
			get
			{
				return _repositoryIcon;
			}
			set
			{
				if (_repositoryIcon != value)
				{
					_repositoryIcon = value;
					RaisePropertyChanged("RepositoryIcon");
				}
			}
		}

		public RepositoryManagerRepositoryItem(RepositoryManager.Repository repository, RepositoryManagerTreeViewItem parent)
			: base(parent)
		{
			_isGitMmWorkspace = GitMmUserControl.IsGitMmWorkspace(repository.Path);
			_name = repository.Name();
			Repository = repository;
			base.Title = Name;
			RepositoryIcon = Theme.RepositoryIcon;
		}

		private string FormatName(string name)
		{
			return _isGitMmWorkspace ? GitMmPrefix + name : name;
		}

		private string NormalizeName(string name)
		{
			if (_isGitMmWorkspace && name != null && name.StartsWith(GitMmPrefix))
			{
				return name.Substring(GitMmPrefix.Length);
			}
			return name;
		}
	}
}
