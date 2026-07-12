using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using ForkPlus.Git;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class AddGitignoreTemplateWindow : ForkPlusDialogWindow
	{
		private static readonly Dictionary<string, (string[] Extensions, string[] PathComponents)> TemplateMarkers = new Dictionary<string, (string[], string[])>
		{
			{
				"C",
				(new string[1] { ".c" }, new string[0])
			},
			{
				"C++",
				(new string[6] { ".cpp", ".cc", ".cxx", ".hpp", ".hxx", ".hh" }, new string[0])
			},
			{
				"C#",
				(new string[2] { ".cs", ".csproj" }, new string[0])
			},
			{
				"Dart",
				(new string[1] { ".dart" }, new string[0])
			},
			{
				"Go",
				(new string[1] { ".go" }, new string[0])
			},
			{
				"Java",
				(new string[1] { ".java" }, new string[0])
			},
			{
				"Kotlin",
				(new string[2] { ".kt", ".kts" }, new string[0])
			},
			{
				"Node",
				(new string[4] { ".js", ".ts", ".jsx", ".tsx" }, new string[1] { "node_modules" })
			},
			{
				"Objective-C",
				(new string[3] { ".m", ".mm", ".h" }, new string[0])
			},
			{
				"Python",
				(new string[2] { ".py", ".pyc" }, new string[0])
			},
			{
				"Ruby",
				(new string[2] { ".rb", ".gemspec" }, new string[0])
			},
			{
				"Rust",
				(new string[1] { ".rs" }, new string[0])
			},
			{
				"Scala",
				(new string[2] { ".scala", ".sc" }, new string[0])
			},
			{
				"Swift",
				(new string[2] { ".swift", ".xib" }, new string[0])
			},
			{
				"Android",
				(new string[1] { ".gradle" }, new string[0])
			},
			{
				"Unity",
				(new string[3] { ".unity", ".prefab", ".asset" }, new string[0])
			},
			{
				"JetBrains",
				(new string[1] { ".iws" }, new string[1] { ".idea" })
			},
			{
				"VisualStudio",
				(new string[5] { ".sln", ".csproj", ".vbproj", ".vcproj", ".vcxproj" }, new string[1] { ".vs" })
			},
			{
				"VS Code",
				(new string[1] { ".vsix" }, new string[1] { ".vscode" })
			},
			{
				"Xcode",
				(new string[4] { ".xcodeproj", ".xcworkspace", ".xib", ".xcuserstate" }, new string[1] { "xcuserdata" })
			}
		};

		private static readonly HashSet<string> AlwaysEnabledTemplates = new HashSet<string> { "Windows" };

		private static readonly (string Group, HashSet<string> Names)[] TemplateGroups = new(string, HashSet<string>)[3]
		{
			("OS", new HashSet<string> { "Linux", "macOS", "Windows" }),
			("IDE", new HashSet<string> { "Android", "JetBrains", "VisualStudio", "VS Code", "Xcode", "Unity" }),
			("Language", new HashSet<string>
			{
				"C", "C++", "C#", "Dart", "Go", "Java", "Kotlin", "Node", "Objective-C", "Python",
				"Ruby", "Rust", "Scala", "Swift"
			})
		};

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly string[] _untrackedFiles;

		private List<(string Name, string Content)> _templates = new List<(string, string)>();

		private List<string> _selectedTemplateNames = new List<string>();

		private readonly Dictionary<string, CheckBox> _checkboxes = new Dictionary<string, CheckBox>();

		protected override bool IsSubmitAllowed => _selectedTemplateNames.Count > 0;

		public AddGitignoreTemplateWindow(RepositoryUserControl repositoryUserControl, string[] untrackedFiles)
		{
			base.ShowLogo = false;
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_untrackedFiles = untrackedFiles;
			base.DialogTitle = PreferencesLocalization.Current("Add .gitignore Template");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Add");
			base.DescriptionTextBlock.Inlines.Clear();
			base.DescriptionTextBlock.Inlines.Add(new Run("Choose "));
			Hyperlink hyperlink = new Hyperlink(new Run(".gitignore"));
			hyperlink.NavigateUri = new Uri("https://git-scm.com/docs/gitignore");
			hyperlink.Style = (Style)TryFindResource("BlueUnderlineHyperlinkStyle");
			hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
			base.DescriptionTextBlock.Inlines.Add(hyperlink);
			base.DescriptionTextBlock.Inlines.Add(new Run(" template for your project"));
			LoadTemplates();
			BuildTemplateList();
			PreselectTemplates();
			UpdatePreview();
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				string contents = CombinedSelectedContent();
				try
				{
					File.WriteAllText(gitModule.MakePath(".gitignore"), contents);
				}
				catch (Exception ex)
				{
					SetStatus(ForkPlusDialogStatus.Error, ex.Message);
					return;
				}
				CloseWithOk();
			}
		}

		private void LoadTemplates()
		{
			string text;
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.gitignore.txt";
				using Stream stream = executingAssembly.GetManifestResourceStream(name);
				using StreamReader streamReader = new StreamReader(stream);
				text = streamReader.ReadToEnd();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load gitignore templates", ex);
				return;
			}
			int startIndex = 0;
			while (true)
			{
				int num = text.IndexOf("### ", startIndex, StringComparison.Ordinal);
				if (num >= 0)
				{
					int num2 = num + 4;
					int num3 = text.IndexOf('\n', num2);
					if (num3 < 0)
					{
						num3 = text.Length;
					}
					string item = text.Substring(num2, num3 - num2).Trim();
					int num4 = text.IndexOf("\n### ", num3, StringComparison.Ordinal);
					int num5 = ((num4 >= 0) ? num4 : text.Length);
					string item2 = text.Substring(num, num5 - num);
					_templates.Add((item, item2));
					startIndex = num5;
					continue;
				}
				break;
			}
		}

		private void BuildTemplateList()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			(string, HashSet<string>)[] templateGroups = TemplateGroups;
			for (int i = 0; i < templateGroups.Length; i++)
			{
				(string, HashSet<string>) tuple = templateGroups[i];
				foreach (string item2 in tuple.Item2)
				{
					(dictionary[item2], _) = tuple;
				}
			}
			(string, List<int>)[] array = TemplateGroups.Map(((string Group, HashSet<string> Names) g) => (Group: g.Group, Indices: new List<int>()));
			for (int j = 0; j < _templates.Count; j++)
			{
				if (dictionary.TryGetValue(_templates[j].Name, out var groupName))
				{
					int num = Array.FindIndex(array, ((string Group, List<int> Indices) g) => g.Group == groupName);
					if (num >= 0)
					{
						array[num].Item2.Add(j);
					}
				}
			}
			(string, List<int>)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, List<int>) tuple3 = array2[i];
				TextBlock element = new TextBlock
				{
					Text = tuple3.Item1,
					FontWeight = FontWeights.Bold,
					FontSize = 11.0,
					Margin = new Thickness(4.0, 6.0, 0.0, 2.0)
				};
				TemplateListPanel.Children.Add(element);
				foreach (int item3 in tuple3.Item2)
				{
					string item = _templates[item3].Name;
					CheckBox checkBox = new CheckBox
					{
						Content = item,
						FontSize = 13.0,
						Margin = new Thickness(4.0, 2.0, 0.0, 2.0),
						Tag = item3
					};
					checkBox.Checked += Checkbox_Toggled;
					checkBox.Unchecked += Checkbox_Toggled;
					_checkboxes[item] = checkBox;
					TemplateListPanel.Children.Add(checkBox);
				}
			}
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void Checkbox_Toggled(object sender, RoutedEventArgs e)
		{
			if (!(sender is CheckBox checkBox))
			{
				return;
			}
			int index = (int)checkBox.Tag;
			string item = _templates[index].Name;
			if (checkBox.IsChecked.GetValueOrDefault())
			{
				if (!_selectedTemplateNames.Contains(item))
				{
					_selectedTemplateNames.Add(item);
				}
			}
			else
			{
				_selectedTemplateNames.Remove(item);
			}
			UpdatePreview();
			UpdateSubmitButton();
		}

		private void PreselectTemplates()
		{
			HashSet<string> fileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> pathComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] untrackedFiles = _untrackedFiles;
			foreach (string text in untrackedFiles)
			{
				int num = text.LastIndexOf('.');
				if (num >= 0)
				{
					fileExtensions.Add(text.Substring(num));
				}
				string[] array = text.Split('/');
				for (int j = 0; j < array.Length - 1; j++)
				{
					pathComponents.Add(array[j]);
				}
			}
			HashSet<string> hashSet = new HashSet<string>(_templates.Map(((string Name, string Content) t) => t.Name));
			foreach (string alwaysEnabledTemplate in AlwaysEnabledTemplates)
			{
				if (hashSet.Contains(alwaysEnabledTemplate) && !_selectedTemplateNames.Contains(alwaysEnabledTemplate))
				{
					_selectedTemplateNames.Add(alwaysEnabledTemplate);
				}
			}
			foreach (KeyValuePair<string, (string[], string[])> templateMarker in TemplateMarkers)
			{
				string key = templateMarker.Key;
				(string[], string[]) value = templateMarker.Value;
				if (hashSet.Contains(key) && !_selectedTemplateNames.Contains(key) && (value.Item1.AnyItem((string ext) => fileExtensions.Contains(ext)) || value.Item2.AnyItem((string pc) => pathComponents.Contains(pc))))
				{
					_selectedTemplateNames.Add(key);
				}
			}
			foreach (string selectedTemplateName in _selectedTemplateNames)
			{
				if (_checkboxes.TryGetValue(selectedTemplateName, out var value2))
				{
					value2.IsChecked = true;
				}
			}
		}

		private string CombinedSelectedContent()
		{
			HashSet<string> hashSet = new HashSet<string>(_selectedTemplateNames);
			List<string> list = new List<string>();
			foreach (var template in _templates)
			{
				if (hashSet.Contains(template.Name))
				{
					list.Add(template.Content);
				}
			}
			return string.Join("\n\n", list);
		}

		private void UpdatePreview()
		{
			PreviewCodeEditor.Text = CombinedSelectedContent();
		}

	}
}
