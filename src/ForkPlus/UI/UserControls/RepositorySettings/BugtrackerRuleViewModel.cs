using System.ComponentModel;

namespace ForkPlus.UI.UserControls.RepositorySettings
{
	public class BugtrackerRuleViewModel : INotifyPropertyChanged
	{
		private string _name;

		private Level _level;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				if (!(_name == value))
				{
					_name = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
				}
			}
		}

		public Level Level
		{
			get
			{
				return _level;
			}
			set
			{
				if (_level != value)
				{
					_level = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Level"));
				}
			}
		}

		public string RegexString { get; set; }

		public string UrlString { get; set; }

		public string SampleMessage { get; set; }

		public static BugtrackerRuleViewModel NewRule => new BugtrackerRuleViewModel
		{
			Name = "Issue Tracker Rule",
			Level = Level.Local,
			SampleMessage = "Fix documentation of the release process (#12337)"
		};

		public static BugtrackerRuleViewModel SampleGitHubRule => new BugtrackerRuleViewModel
		{
			Name = "Sample GitHub Rule",
			Level = Level.Local,
			RegexString = "#(\\d+)",
			UrlString = "https://github.com/username/repository/issues/$1",
			SampleMessage = "Fix documentation of the release process (#12337)"
		};

		public static BugtrackerRuleViewModel SampleJiraRule => new BugtrackerRuleViewModel
		{
			Name = "Sample Jira Rule",
			Level = Level.Local,
			RegexString = "PROJ-(\\d+)",
			UrlString = "https://jira.yourcompany.com/browse/PROJ-$1",
			SampleMessage = "PROJ-28 Fix documentation of the build process"
		};

		public static BugtrackerRuleViewModel SampleJiraMultiprojectRule => new BugtrackerRuleViewModel
		{
			Name = "Sample Jira Multiproject Rule",
			Level = Level.Local,
			RegexString = "(PRJ1|PRJ2)-(\\d+)",
			UrlString = "https://jira.yourcompany.com/browse/$1-$2",
			SampleMessage = "PRJ1-28 Fix documentation of the build process (PRJ2-45)"
		};

		public event PropertyChangedEventHandler PropertyChanged;

		public BugtrackerRuleViewModel()
		{
		}

		public BugtrackerRuleViewModel(BugtrackerLinkDefinition bugtrackerRule)
		{
			Name = bugtrackerRule.Name;
			Level = bugtrackerRule.Level;
			RegexString = bugtrackerRule.RegexString;
			UrlString = bugtrackerRule.UrlString;
			SampleMessage = "Fix documentation of the release process (#12337)";
		}
	}
}
