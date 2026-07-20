namespace ForkPlus.UI.CustomCommands
{
	public class CustomCommandUI
	{
		public static class Keys
		{
			public const string Title = "title";

			public const string Description = "description";

			public const string Controls = "controls";

			public const string Buttons = "buttons";
		}

		public class Control
		{
			public enum ControlType
			{
				TextBox,
				Dropdown,
				CheckBox
			}

			public enum TextBoxType
			{
				Generic,
				FilePath
			}

			public static class Keys
			{
				public const string Type = "type";
			}

			public class GenericTextBox : Control
			{
				public new static class Keys
				{
					public const string Title = "title";

					public const string TextBoxType = "textBoxType";

					public const string Text = "text";

					public const string Placeholder = "placeholder";
				}

				public string Title { get; }

				[Null]
				public string Text { get; }

				[Null]
				public string Placeholder { get; }

				public GenericTextBox(string title, [Null] string text = null, [Null] string placeholder = null)
				{
					Title = title;
					Text = text;
					Placeholder = placeholder;
				}
			}

			public class PathTextBox : Control
			{
				public enum DialogType
				{
					SaveFile,
					OpenFile,
					OpenDirectory
				}

				public new static class Keys
				{
					public const string Title = "title";

					public const string TextBoxType = "textBoxType";

					public const string DialogType = "dialogType";

					public const string DefaultDirectory = "defaultDirectory";

					public const string FileName = "filename";
				}

				public string Title { get; }

				public DialogType PathDialogType { get; }

				[Null]
				public string DefaultDirectory { get; }

				[Null]
				public string FileName { get; }

				public PathTextBox(string title, DialogType dialogType, [Null] string defaultDirectory = null, [Null] string fileName = null)
				{
					Title = title;
					PathDialogType = dialogType;
					DefaultDirectory = defaultDirectory;
					FileName = fileName;
				}
			}

			public class Dropdown : Control
			{
				public enum DropdownType
				{
					References
				}

				public new static class Keys
				{
					public const string Title = "title";

					public const string DropdownType = "dropdownType";

					public const string Filter = "filter";
				}

				public string Title { get; }

				public DropdownType Type { get; }

				public string Filter { get; }

				public Dropdown(string title, DropdownType type, string filter)
				{
					Title = title;
					Type = type;
					Filter = filter;
				}
			}

			public class CheckBox : Control
			{
				public new static class Keys
				{
					public const string Title = "title";

					public const string DefaultValue = "defaultValue";

					public const string CheckedValue = "checkedValue";

					public const string UncheckedValue = "uncheckedValue";
				}

				public string Title { get; }

				public bool DefaultValue { get; }

				[Null]
				public string CheckedValue { get; }

				[Null]
				public string UncheckedValue { get; }

				public CheckBox(string title, bool defaultValue = false, [Null] string checkedValue = null, [Null] string uncheckedValue = null)
				{
					Title = title;
					CheckedValue = checkedValue;
					UncheckedValue = uncheckedValue;
					DefaultValue = defaultValue;
				}
			}
		}

		public class Button
		{
			public static class Keys
			{
				public const string Title = "title";

				public const string Action = "action";
			}

			public string Title { get; }

			public CustomCommandAction Action { get; }

			public Button(string title, CustomCommandAction action)
			{
				Title = title;
				Action = action;
			}
		}

		public string Title { get; }

		public string Description { get; }

		public Control[] Controls { get; }

		public Button[] Buttons { get; }

		public CustomCommandUI(string title, string description, Control[] controls, Button[] buttons)
		{
			Title = title;
			Description = description;
			Controls = controls;
			Buttons = buttons;
		}
	}
}
