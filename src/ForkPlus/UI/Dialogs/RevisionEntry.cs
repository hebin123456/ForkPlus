using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public class RevisionEntry : IRoundedSelectionListBoxViewModel, INotifyPropertyChanged
	{
		private readonly InteractiveRebaseTodoListItem _todoItem;

		private string _groupMessage;

		private string _customMessage;

		private string _subject;

		private string _description;

		private InteractiveRebaseAction _interactiveRebaseAction;

		private GraphItemType _graphType;

		private ListBoxSelectionType _selectionType;

		public int Row { get; set; }

		public string Sha => _todoItem.Sha.ToString();

		public string AbbreviatedSha { get; }

		public string OriginalMessage => _todoItem.Message;

		public string Message => CustomMessage ?? GroupMessage ?? OriginalMessage;

		public UserIdentity Author => _todoItem.Author;

		public string AuthorName => _todoItem.Author.Name;

		public DateTime AuthorDate => _todoItem.AuthorDate;

		public LocalBranch[] Refs => _todoItem.Refs;

		public ReferencePanelLocalBranchViewModel[] RefViewModels => Refs.Map((LocalBranch x) => new ReferencePanelLocalBranchViewModel(x)).ToArray();

		public string GroupMessage
		{
			get
			{
				return _groupMessage;
			}
			set
			{
				_groupMessage = value;
				UpdateSubjectAndDescription();
			}
		}

		public string CustomMessage
		{
			get
			{
				return _customMessage;
			}
			set
			{
				_customMessage = value;
				UpdateSubjectAndDescription();
			}
		}

		public string Subject
		{
			get
			{
				return _subject;
			}
			private set
			{
				if (!(value == _subject))
				{
					_subject = value;
					NotifyPropertyChanged("Subject");
				}
			}
		}

		public string Description
		{
			get
			{
				return _description;
			}
			private set
			{
				if (!(value == _description))
				{
					_description = value;
					NotifyPropertyChanged("Description");
				}
			}
		}

		public InteractiveRebaseAction Action
		{
			get
			{
				return _interactiveRebaseAction;
			}
			set
			{
				if (value != _interactiveRebaseAction)
				{
					_interactiveRebaseAction = value;
					NotifyPropertyChanged("Action");
				}
			}
		}

		public GraphItemType GraphType
		{
			get
			{
				return _graphType;
			}
			set
			{
				if (value != _graphType)
				{
					_graphType = value;
					NotifyPropertyChanged("GraphType");
				}
			}
		}

		public ListBoxSelectionType SelectionType
		{
			get
			{
				return _selectionType;
			}
			set
			{
				if (_selectionType != value)
				{
					_selectionType = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectionType"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public RevisionEntry(int row, InteractiveRebaseTodoListItem todoItem)
		{
			Row = row;
			_todoItem = todoItem;
			AbbreviatedSha = todoItem.Sha.ToAbbreviatedString();
			Action = todoItem.Action;
			UpdateSubjectAndDescription();
		}

		public string AsTodoListString(bool updateRefs)
		{
			string text = AbbreviatedAction() + " " + _todoItem.Sha.ToString() + " " + Subject + "\n";
			if (updateRefs)
			{
				LocalBranch[] refs = Refs;
				foreach (LocalBranch localBranch in refs)
				{
					text = text + "update-ref " + localBranch.FullReference + "\n";
				}
			}
			return text;
		}

		private void UpdateSubjectAndDescription()
		{
			CommitMessageHelper.SplitCommitBody(Message, out var subject, out var description, "\n");
			Subject = subject;
			Description = description.TrimStart(Consts.Chars.NewLines);
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private string AbbreviatedAction()
		{
			return Action switch
			{
				InteractiveRebaseAction.Fixup => "f", 
				InteractiveRebaseAction.Drop => "d", 
				InteractiveRebaseAction.Squash => "s", 
				InteractiveRebaseAction.Pick => "p", 
				InteractiveRebaseAction.Reword => "r", 
				InteractiveRebaseAction.Edit => "e", 
				InteractiveRebaseAction.UpdateRefs => "u", 
				_ => throw new NotImplementedException(), 
			};
		}
	}
}
