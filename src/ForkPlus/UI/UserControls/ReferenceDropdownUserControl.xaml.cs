using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls
{
	public partial class ReferenceDropdownUserControl : UserControl
	{
		public class DropdownItem : INotifyPropertyChanged
		{
			public ForkPlus.Git.Reference Reference { get; private set; }

			public ImageSource Icon { get; private set; }

			public string Title { get; private set; }

			public event PropertyChangedEventHandler PropertyChanged;

			public DropdownItem(ForkPlus.Git.Reference reference, ImageSource icon)
			{
				Reference = reference;
				Icon = icon;
				Title = reference.Name;
			}
		}

		private string _filter;

		private ForkPlus.Git.Reference[] _references;

		private Remote[] _remotes;

		[Null]
		public ForkPlus.Git.Reference SelectedReference => (ReferenceComboBox.SelectedItem as DropdownItem)?.Reference;

		public ReferenceDropdownUserControl(RepositoryData repositoryData, CustomCommandUI.Control.Dropdown dropdown)
		{
			_references = repositoryData.References.Items;
			_remotes = repositoryData.Remotes.Items;
			_filter = dropdown.Filter;
			InitializeComponent();
			ReferenceComboBox.ItemsSource = CreateItemsSource();
			ReferenceComboBox.SelectedIndex = 0;
		}

		private DropdownItem[] CreateItemsSource()
		{
			string[] array = _filter.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries);
			int num = array.Length;
			List<DropdownItem> list = new List<DropdownItem>(array.Length);
			for (int i = 0; i < _references.Length; i++)
			{
				ForkPlus.Git.Reference reference = _references[i];
				if (num != 0 && !array.ContainsItem((string x) => reference.FullReference.StartsWith(x)))
				{
					continue;
				}
				ImageSource icon = null;
				string fullReference = reference.FullReference;
				if (fullReference.StartsWith("refs/heads/"))
				{
					icon = Theme.BranchIcon;
				}
				else if (fullReference.StartsWith("refs/tags/"))
				{
					icon = Theme.TagIcon;
				}
				else if (fullReference.StartsWith("refs/remotes/"))
				{
					string text = fullReference.Substring("refs/remotes/".Length);
					int num2 = text.IndexOf('/');
					if (num2 != -1 && num2 + 1 < text.Length)
					{
						string remoteName = text.Substring(0, num2);
						icon = GetRemoteIcon(remoteName);
					}
				}
				list.Add(new DropdownItem(reference, icon));
			}
			return list.ToArray();
		}

		private ImageSource GetRemoteIcon(string remoteName)
		{
			return IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == remoteName)?.Icon ?? Theme.RemoteIcon;
		}

	}
}
