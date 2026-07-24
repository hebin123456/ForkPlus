// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Inline/Run）
// - Inline[]/Run 用法保持原样（Avalonia InlineCollection 兼容；参考 EditCustomActionWindow）
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Documents;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandViewModel : INotifyPropertyChanged
	{
		private static readonly string UrlCommandDefaultUrl = "https://hebin.me";

		private CustomCommandTarget _target;

		private CustomCommandActionViewModel _actionViewModel;

		private CustomCommandUIViewModel _uiViewModel;

		private bool _shared;

		private CustomCommandOS _os;

		private ActionType _actionType;

		private bool _localBranchReferenceTarget;

		private bool _remoteBranchReferenceTarget;

		private bool _tagReferenceTarget;

		private string _name;

		private Inline[] _variablesInlines;

		public CustomCommandTarget Target
		{
			get
			{
				return _target;
			}
			set
			{
				_target = value;
				VariablesInlines = _target.CreateVariablesList();
				NotifyPropertyChanged("Target");
			}
		}

		public CustomCommand CustomCommand
		{
			get
			{
				CustomCommandAction action = null;
				CustomCommandUI ui = null;
				if (ActionType == ActionType.Action)
				{
					action = ActionViewModel.Action;
				}
				else if (ActionType == ActionType.UI)
				{
					List<CustomCommandUI.Button> list = new List<CustomCommandUI.Button>(2);
					if (UIViewModel.Button1Enabled)
					{
						list.Add(new CustomCommandUI.Button(UIViewModel.Button1Title, UIViewModel.Button1ActionViewModel.Action));
					}
					if (UIViewModel.Button2Enabled)
					{
						list.Add(new CustomCommandUI.Button(UIViewModel.Button2Title, UIViewModel.Button2ActionViewModel.Action));
					}
					ui = new CustomCommandUI(UIViewModel.DialogTitle, UIViewModel.DialogDescription, UIViewModel.Controls, list.ToArray());
				}
				List<CustomCommandRefTarget> list2 = null;
				if (Target == CustomCommandTarget.Reference)
				{
					list2 = new List<CustomCommandRefTarget>();
					if (LocalBranchReferenceTarget)
					{
						list2.Add(CustomCommandRefTarget.LocalBranch);
					}
					if (RemoteBranchReferenceTarget)
					{
						list2.Add(CustomCommandRefTarget.RemoteBranch);
					}
					if (TagReferenceTarget)
					{
						list2.Add(CustomCommandRefTarget.Tag);
					}
				}
				int version = ((Version > 2) ? Version : 2);
				return new CustomCommand(Target, list2?.ToArray(), Name, action, ui, OS, Shared, version);
			}
		}

		public CustomCommandActionViewModel ActionViewModel
		{
			get
			{
				return _actionViewModel;
			}
			set
			{
				_actionViewModel = value;
				NotifyPropertyChanged("ActionViewModel");
			}
		}

		public CustomCommandUIViewModel UIViewModel
		{
			get
			{
				return _uiViewModel;
			}
			set
			{
				_uiViewModel = value;
				NotifyPropertyChanged("UIViewModel");
			}
		}

		public bool Shared
		{
			get
			{
				return _shared;
			}
			set
			{
				_shared = value;
				NotifyPropertyChanged("Shared");
			}
		}

		public CustomCommandOS OS
		{
			get
			{
				return _os;
			}
			set
			{
				_os = value;
				NotifyPropertyChanged("OS");
			}
		}

		public int Version { get; }

		public ActionType ActionType
		{
			get
			{
				return _actionType;
			}
			set
			{
				_actionType = value;
				NotifyPropertyChanged("ActionType");
				NotifyPropertyChanged("Details");
			}
		}

		public bool LocalBranchReferenceTarget
		{
			get
			{
				return _localBranchReferenceTarget;
			}
			set
			{
				_localBranchReferenceTarget = value;
				NotifyPropertyChanged("LocalBranchReferenceTarget");
			}
		}

		public bool RemoteBranchReferenceTarget
		{
			get
			{
				return _remoteBranchReferenceTarget;
			}
			set
			{
				_remoteBranchReferenceTarget = value;
				NotifyPropertyChanged("RemoteBranchReferenceTarget");
			}
		}

		public bool TagReferenceTarget
		{
			get
			{
				return _tagReferenceTarget;
			}
			set
			{
				_tagReferenceTarget = value;
				NotifyPropertyChanged("TagReferenceTarget");
			}
		}

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
				NotifyPropertyChanged("Name");
			}
		}

		public Inline[] VariablesInlines
		{
			get
			{
				return _variablesInlines;
			}
			set
			{
				_variablesInlines = value;
				NotifyPropertyChanged("VariablesInlines");
			}
		}

		public string VariablesString => string.Join("", _variablesInlines.Map((Inline x) => (x as Run).Text));

		public string Details => ActionType switch
		{
			ActionType.UI => "ui: " + UIViewModel.DialogTitle, 
			ActionType.Action => ActionViewModel.Details, 
			_ => throw new CannotReachHereException(), 
		};

		public event PropertyChangedEventHandler PropertyChanged;

		public CustomCommandViewModel(CustomCommand command)
			: this(command.Target, command.Name, command.Action, command.ReferenceTargets, command.UI, command.OS, command.Shared, command.Version)
		{
		}

		public CustomCommandViewModel(CustomCommandTarget target, string name, [Null] CustomCommandAction action, [Null] CustomCommandRefTarget[] referenceTargets = null, [Null] CustomCommandUI ui = null, CustomCommandOS osType = CustomCommandOS.Any, bool shared = false, int version = 2)
		{
			Target = target;
			Name = name;
			Shared = shared;
			OS = osType;
			Version = version;
			ActionType = ((action == null) ? ActionType.UI : ActionType.Action);
			ActionViewModel = new CustomCommandActionViewModel(action ?? new UrlCustomCommandAction(UrlCommandDefaultUrl));
			UIViewModel = new CustomCommandUIViewModel(ui ?? CreateDefaultCustomCommandUI(), this);
			if (Target == CustomCommandTarget.Reference)
			{
				LocalBranchReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.LocalBranch) ?? false;
				RemoteBranchReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.RemoteBranch) ?? false;
				TagReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.Tag) ?? false;
			}
			else
			{
				LocalBranchReferenceTarget = true;
				RemoteBranchReferenceTarget = true;
			}
		}

		public void RefreshDetails()
		{
			NotifyPropertyChanged("Details");
		}

		private static CustomCommandUI CreateDefaultCustomCommandUI()
		{
			string description = "Are you sure you want to execute custom command?";
			CustomCommandUI.Button button = new CustomCommandUI.Button("OK", new UrlCustomCommandAction(UrlCommandDefaultUrl));
			CustomCommandUI.Button button2 = new CustomCommandUI.Button("Cancel", new CancelCustomCommandAction());
			return new CustomCommandUI("Execute custom command", description, new CustomCommandUI.Control[0], new CustomCommandUI.Button[2] { button, button2 });
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
