// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System.ComponentModel;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public class ExternalToolViewModel : INotifyPropertyChanged
	{
		private string _name;

		private bool _isPrimary;

		private bool _isVisible;

		private bool _isPredefined;

		private string _arguments;

		private string _path;

		private bool _pathOverridden;

		private bool _argumentsOverridden;

		private bool _resetInProgress;

		private bool _initialized;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
			}
		}

		public bool IsPrimary
		{
			get
			{
				return _isPrimary;
			}
			set
			{
				_isPrimary = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPrimary"));
			}
		}

		public bool IsVisible
		{
			get
			{
				return _isVisible;
			}
			set
			{
				_isVisible = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsVisible"));
			}
		}

		public bool IsPredefined
		{
			get
			{
				return _isPredefined;
			}
			set
			{
				_isPredefined = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPredefined"));
			}
		}

		public string Arguments
		{
			get
			{
				return _arguments;
			}
			set
			{
				if (_initialized && _arguments != value && IsPredefined && !_resetInProgress)
				{
					_argumentsOverridden = true;
				}
				_arguments = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Arguments"));
			}
		}

		public string Path
		{
			get
			{
				return _path;
			}
			set
			{
				if (_initialized && _path != value && IsPredefined && !_resetInProgress)
				{
					_pathOverridden = true;
				}
				_path = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Path"));
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Icon"));
			}
		}

		public ToolType Type { get; }

		public bool IsAvailable => !string.IsNullOrEmpty(Path);

		public string PrimaryLabel => PreferencesLocalization.Translate("primary", ForkPlusSettings.Default.UiLanguage);

		[Null]
		public IImage Icon => IconTools.GetImageSourceForFile(Path);

		public ExternalTool ExternalTool => new ExternalTool(Type, Name, Path, _pathOverridden, Arguments.Split(Consts.Chars.Space), _argumentsOverridden, IsPredefined, IsPrimary, IsVisible);

		public event PropertyChangedEventHandler PropertyChanged;

		public void ApplyLocalization()
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryLabel)));
		}

		public ExternalToolViewModel(ExternalTool tool)
			: this(tool.Type, tool.Name, tool.Path, tool.PathOverridden, tool.Arguments, tool.ArgumentsOverridden, tool.IsPredefined, tool.IsPrimary, tool.IsVisible)
		{
		}

		public ExternalToolViewModel(string name)
			: this(ToolType.Custom, name, string.Empty, pathOverridden: false, new string[0], argumentsOverridden: false, isPredefined: false, isPrimary: false, isVisible: true)
		{
		}

		public ExternalToolViewModel(ToolType type, string name, string path, bool pathOverridden, string[] arguments, bool argumentsOverridden, bool isPredefined, bool isPrimary, bool isVisible)
		{
			Type = type;
			Name = name;
			Path = path;
			_pathOverridden = pathOverridden;
			Arguments = string.Join(" ", arguments);
			_argumentsOverridden = argumentsOverridden;
			IsPredefined = isPredefined;
			IsPrimary = isPrimary;
			IsVisible = isVisible;
			_initialized = true;
		}

		public void ResetToDefault(ToolDefinition[] toolDefinitions)
		{
			if (IsPredefined)
			{
				ToolDefinition? toolDefinition = toolDefinitions.FirstItemStruct((ToolDefinition x) => x.Type == Type);
				if (toolDefinition.HasValue)
				{
					ToolDefinition valueOrDefault = toolDefinition.GetValueOrDefault();
					_resetInProgress = true;
					string predefinedToolPath = ExternalToolManager.GetPredefinedToolPath(valueOrDefault);
					Path = ((predefinedToolPath != null) ? predefinedToolPath : string.Empty);
					_pathOverridden = false;
					Arguments = string.Join(" ", valueOrDefault.Arguments);
					IsVisible = true;
					_argumentsOverridden = false;
					_resetInProgress = false;
				}
			}
		}
	}
}
