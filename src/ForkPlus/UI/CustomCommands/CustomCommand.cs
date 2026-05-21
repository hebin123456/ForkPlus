namespace ForkPlus.UI.CustomCommands
{
	public class CustomCommand
	{
		public static class Keys
		{
			public const string Name = "name";

			public const string Target = "target";

			public const string ReferenceTargets = "refTargets";

			public const string OldReferenceTargets = "referenceTargets";

			public const string Action = "action";

			public const string UI = "ui";

			public const string OS = "os";
		}

		public const int SupportedSchemaVersion = 2;

		public CustomCommandTarget Target { get; }

		[Null]
		public CustomCommandRefTarget[] ReferenceTargets { get; }

		public string Name { get; }

		[Null]
		public CustomCommandAction Action { get; }

		[Null]
		public CustomCommandUI UI { get; }

		public CustomCommandOS OS { get; }

		public bool Shared { get; }

		public int Version { get; }

		public CustomCommand(CustomCommandTarget target, [Null] CustomCommandRefTarget[] referenceTargets, string name, [Null] CustomCommandAction action, [Null] CustomCommandUI ui, CustomCommandOS os, bool shared, int version)
		{
			Target = target;
			ReferenceTargets = referenceTargets;
			Name = name;
			Action = action;
			UI = ui;
			OS = os;
			Shared = shared;
			Version = version;
		}

		public bool CustomCommandEquals(CustomCommand customCommand)
		{
			if (Target == customCommand.Target && ReferenceTargetsAreEqual(ReferenceTargets, customCommand.ReferenceTargets) && Name == customCommand.Name && ActionsAreEqual(Action, customCommand.Action) && UIsAreEqual(UI, customCommand.UI) && OS == customCommand.OS && Shared == customCommand.Shared)
			{
				return Version == customCommand.Version;
			}
			return false;
		}

		private bool UIsAreEqual([Null] CustomCommandUI lhs, [Null] CustomCommandUI rhs)
		{
			if (lhs != null)
			{
				if (rhs != null)
				{
					if (lhs.Title == rhs.Title && lhs.Description == rhs.Description && ControlArraysAreEqual(lhs.Controls, rhs.Controls))
					{
						return ButtonArraysAreEqual(lhs.Buttons, rhs.Buttons);
					}
					return false;
				}
				return false;
			}
			return rhs == null;
		}

		private static bool ControlArraysAreEqual(CustomCommandUI.Control[] lhs, CustomCommandUI.Control[] rhs)
		{
			if (lhs.Length != rhs.Length)
			{
				return false;
			}
			for (int i = 0; i < lhs.Length; i++)
			{
				if (!ControlsAreEqual(lhs[i], rhs[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool ControlsAreEqual(CustomCommandUI.Control lhs, CustomCommandUI.Control rhs)
		{
			if (lhs is CustomCommandUI.Control.GenericTextBox genericTextBox && rhs is CustomCommandUI.Control.GenericTextBox genericTextBox2)
			{
				if (genericTextBox.Title == genericTextBox2.Title && genericTextBox.Text == genericTextBox2.Text)
				{
					return genericTextBox.Placeholder == genericTextBox2.Placeholder;
				}
				return false;
			}
			if (lhs is CustomCommandUI.Control.PathTextBox pathTextBox && rhs is CustomCommandUI.Control.PathTextBox pathTextBox2)
			{
				if (pathTextBox.Title == pathTextBox2.Title && pathTextBox.FileName == pathTextBox2.FileName && pathTextBox.DefaultDirectory == pathTextBox2.DefaultDirectory)
				{
					return pathTextBox.PathDialogType == pathTextBox2.PathDialogType;
				}
				return false;
			}
			if (lhs is CustomCommandUI.Control.Dropdown dropdown && rhs is CustomCommandUI.Control.Dropdown dropdown2)
			{
				if (dropdown.Title == dropdown2.Title && dropdown.Type == dropdown2.Type)
				{
					return dropdown.Filter == dropdown2.Filter;
				}
				return false;
			}
			if (lhs is CustomCommandUI.Control.CheckBox checkBox && rhs is CustomCommandUI.Control.CheckBox checkBox2)
			{
				if (checkBox.Title == checkBox2.Title && checkBox.DefaultValue == checkBox2.DefaultValue && checkBox.CheckedValue == checkBox2.CheckedValue)
				{
					return checkBox.UncheckedValue == checkBox2.UncheckedValue;
				}
				return false;
			}
			return false;
		}

		private static bool ButtonArraysAreEqual(CustomCommandUI.Button[] lhs, CustomCommandUI.Button[] rhs)
		{
			if (lhs.Length != rhs.Length)
			{
				return false;
			}
			for (int i = 0; i < lhs.Length; i++)
			{
				if (!ButtonsAreEqual(lhs[i], rhs[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool ButtonsAreEqual(CustomCommandUI.Button lhs, CustomCommandUI.Button rhs)
		{
			if (lhs.Title == rhs.Title)
			{
				return ActionsAreEqual(lhs.Action, rhs.Action);
			}
			return false;
		}

		private static bool ActionsAreEqual([Null] CustomCommandAction lhs, [Null] CustomCommandAction rhs)
		{
			if (lhs != null)
			{
				if (rhs != null)
				{
					if (lhs is ProcessCustomCommandAction processCustomCommandAction && rhs is ProcessCustomCommandAction processCustomCommandAction2)
					{
						if (processCustomCommandAction.Path == processCustomCommandAction2.Path && processCustomCommandAction.Parameters == processCustomCommandAction2.Parameters && processCustomCommandAction.ShowOutput == processCustomCommandAction2.ShowOutput)
						{
							return processCustomCommandAction.WaitForExit == processCustomCommandAction2.WaitForExit;
						}
						return false;
					}
					if (lhs is ShCustomCommandAction shCustomCommandAction && rhs is ShCustomCommandAction shCustomCommandAction2)
					{
						if (shCustomCommandAction.Script == shCustomCommandAction2.Script && shCustomCommandAction.ShowOutput == shCustomCommandAction2.ShowOutput)
						{
							return shCustomCommandAction.WaitForExit == shCustomCommandAction2.WaitForExit;
						}
						return false;
					}
					if (lhs is UrlCustomCommandAction urlCustomCommandAction && rhs is UrlCustomCommandAction urlCustomCommandAction2)
					{
						return urlCustomCommandAction.Url == urlCustomCommandAction2.Url;
					}
					return false;
				}
				return false;
			}
			return rhs == null;
		}

		private static bool ReferenceTargetsAreEqual(CustomCommandRefTarget[] lhs, CustomCommandRefTarget[] rhs)
		{
			if (lhs != null)
			{
				if (rhs != null)
				{
					if (lhs.Length != rhs.Length)
					{
						return false;
					}
					for (int i = 0; i < lhs.Length; i++)
					{
						if (lhs[i] != rhs[i])
						{
							return false;
						}
					}
					return true;
				}
				return false;
			}
			return rhs == null;
		}
	}
}
