using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkPlus.Git;
using ForkPlus.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.CustomCommands
{
	internal class CustomCommandManager
	{
		private static class Coders
		{
			public static JArray Encode(IReadOnlyList<CustomCommand> customActions)
			{
				JArray jArray = new JArray();
				JObject jObject = new JObject();
				jObject.Add("version", new JValue(2L));
				jArray.Add(jObject);
				foreach (CustomCommand customAction in customActions)
				{
					jArray.Add(Encode(customAction));
				}
				return jArray;
			}

			private static JObject Encode(CustomCommand customCommand)
			{
				JObject jObject = new JObject();
				jObject.Add("name", new JValue(customCommand.Name));
				jObject.Add("target", Encode(customCommand.Target));
				JToken jToken = Encode(customCommand.OS);
				if (jToken != null)
				{
					jObject.Add("os", jToken);
				}
				if (customCommand.ReferenceTargets != null)
				{
					JArray jArray = Encode(customCommand.ReferenceTargets);
					if (jArray != null)
					{
						jObject.Add("refTargets", jArray);
					}
				}
				JToken jToken2 = Encode(customCommand.Action);
				if (jToken2 != null)
				{
					jObject.Add("action", jToken2);
				}
				JToken jToken3 = Encode(customCommand.UI);
				if (jToken3 != null)
				{
					jObject.Add("ui", jToken3);
				}
				return jObject;
			}

			[Null]
			private static JToken Encode([Null] CustomCommandAction action)
			{
				if (action == null)
				{
					return null;
				}
				JObject jObject = new JObject();
				if (action is ProcessCustomCommandAction processCustomCommandAction)
				{
					jObject.Add("type", new JValue("process"));
					jObject.Add("path", new JValue(processCustomCommandAction.Path));
					jObject.Add("args", new JValue(processCustomCommandAction.Parameters));
					jObject.Add("showOutput", new JValue(processCustomCommandAction.ShowOutput));
					jObject.Add("waitForExit", new JValue(processCustomCommandAction.WaitForExit));
				}
				else if (action is ShCustomCommandAction shCustomCommandAction)
				{
					jObject.Add("type", new JValue("sh"));
					jObject.Add("script", new JValue(shCustomCommandAction.Script));
					jObject.Add("showOutput", new JValue(shCustomCommandAction.ShowOutput));
					jObject.Add("waitForExit", new JValue(shCustomCommandAction.WaitForExit));
				}
				else if (action is UrlCustomCommandAction urlCustomCommandAction)
				{
					jObject.Add("type", new JValue("url"));
					jObject.Add("url", new JValue(urlCustomCommandAction.Url));
				}
				else
				{
					if (!(action is CancelCustomCommandAction))
					{
						throw new CannotReachHereException();
					}
					jObject.Add("type", new JValue("cancel"));
				}
				return jObject;
			}

			[Null]
			private static JToken Encode([Null] CustomCommandUI ui)
			{
				if (ui == null)
				{
					return null;
				}
				return new JObject
				{
					{
						"title",
						new JValue(ui.Title)
					},
					{
						"description",
						new JValue(ui.Description)
					},
					{
						"controls",
						Encode(ui.Controls)
					},
					{
						"buttons",
						Encode(ui.Buttons)
					}
				};
			}

			private static JToken Encode(CustomCommandUI.Control[] controls)
			{
				JArray jArray = new JArray();
				foreach (CustomCommandUI.Control control in controls)
				{
					jArray.Add(Encode(control));
				}
				return jArray;
			}

			private static JToken Encode(CustomCommandUI.Control control)
			{
				JObject jObject = new JObject();
				if (control is CustomCommandUI.Control.GenericTextBox genericTextBox)
				{
					jObject.Add("type", new JValue("textBox"));
					jObject.Add("textBoxType", new JValue("generic"));
					jObject.Add("title", new JValue(genericTextBox.Title));
					jObject.Add("text", new JValue(genericTextBox.Text));
					jObject.Add("placeholder", new JValue(genericTextBox.Placeholder));
				}
				else if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
				{
					jObject.Add("type", new JValue("textBox"));
					jObject.Add("textBoxType", new JValue("filepath"));
					jObject.Add("title", new JValue(pathTextBox.Title));
					JToken jToken = Encode(pathTextBox.PathDialogType);
					if (jToken != null)
					{
						jObject.Add("dialogType", jToken);
					}
					jObject.Add("defaultDirectory", new JValue(pathTextBox.DefaultDirectory));
					jObject.Add("filename", new JValue(pathTextBox.FileName));
				}
				else if (control is CustomCommandUI.Control.Dropdown dropdown)
				{
					jObject.Add("type", new JValue("dropdown"));
					jObject.Add("title", new JValue(dropdown.Title));
					JToken jToken2 = Encode(dropdown.Type);
					if (jToken2 != null)
					{
						jObject.Add("dropdownType", jToken2);
					}
					jObject.Add("filter", new JValue(dropdown.Filter));
				}
				else if (control is CustomCommandUI.Control.CheckBox checkBox)
				{
					jObject.Add("type", new JValue("checkBox"));
					jObject.Add("title", new JValue(checkBox.Title));
					jObject.Add("defaultValue", new JValue(checkBox.DefaultValue));
					jObject.Add("checkedValue", new JValue(checkBox.CheckedValue));
					jObject.Add("uncheckedValue", new JValue(checkBox.UncheckedValue));
				}
				return jObject;
			}

			[Null]
			private static JToken Encode(CustomCommandUI.Control.PathTextBox.DialogType dialogType)
			{
				return dialogType switch
				{
					CustomCommandUI.Control.PathTextBox.DialogType.SaveFile => new JValue("saveFile"), 
					CustomCommandUI.Control.PathTextBox.DialogType.OpenFile => new JValue("openFile"), 
					CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory => new JValue("openDirectory"), 
					_ => null, 
				};
			}

			[Null]
			private static JToken Encode(CustomCommandUI.Control.Dropdown.DropdownType dropdownType)
			{
				if (dropdownType == CustomCommandUI.Control.Dropdown.DropdownType.References)
				{
					return new JValue("references");
				}
				return null;
			}

			private static JToken Encode(CustomCommandUI.Button[] buttons)
			{
				JArray jArray = new JArray();
				foreach (CustomCommandUI.Button button in buttons)
				{
					jArray.Add(Encode(button));
				}
				return jArray;
			}

			private static JToken Encode(CustomCommandUI.Button button)
			{
				return new JObject
				{
					{
						"title",
						new JValue(button.Title)
					},
					{
						"action",
						Encode(button.Action)
					}
				};
			}

			[Null]
			private static JToken Encode(CustomCommandOS os)
			{
				return os switch
				{
					CustomCommandOS.Mac => new JValue("macOS"), 
					CustomCommandOS.Windows => new JValue("windows"), 
					CustomCommandOS.Any => null, 
					_ => null, 
				};
			}

			private static JToken Encode(CustomCommandTarget target)
			{
				return target switch
				{
					CustomCommandTarget.Reference => new JValue("ref"), 
					CustomCommandTarget.Repository => new JValue("repository"), 
					CustomCommandTarget.RepositoryFile => new JValue("file"), 
					CustomCommandTarget.Revision => new JValue("revision"), 
					CustomCommandTarget.Submodule => new JValue("submodule"), 
					_ => throw new InvalidOperationException(), 
				};
			}

			public static JArray Encode(CustomCommandRefTarget[] referenceTargets)
			{
				JArray jArray = new JArray();
				if (referenceTargets == null)
				{
					return jArray;
				}
				for (int i = 0; i < referenceTargets.Length; i++)
				{
					switch (referenceTargets[i])
					{
					case CustomCommandRefTarget.LocalBranch:
						jArray.Add(new JValue("localbranch"));
						break;
					case CustomCommandRefTarget.RemoteBranch:
						jArray.Add(new JValue("remotebranch"));
						break;
					case CustomCommandRefTarget.Tag:
						jArray.Add(new JValue("tag"));
						break;
					}
				}
				return jArray;
			}

			public static CustomCommand[] DecodeCustomCommandArray([Null] JArray jsonArray, bool shared)
			{
				if (jsonArray == null)
				{
					return new CustomCommand[0];
				}
				int version = DecodeFileVersion(jsonArray);
				List<CustomCommand> list = new List<CustomCommand>(jsonArray.Count);
				foreach (JToken item in jsonArray)
				{
					if (item["version"] == null)
					{
						CustomCommand customCommand = DecodeCustomCommand(item, shared, version);
						if (customCommand != null)
						{
							list.Add(customCommand);
						}
					}
				}
				list.Sort((CustomCommand x, CustomCommand y) => x.Name.CompareTo(y.Name));
				return list.ToArray();
			}

			public static int DecodeFileVersion([Null] JArray array)
			{
				if (array.Count > 0)
				{
					int? num = array[0]["version"]?.Value<int>();
					if (num.HasValue)
					{
						return num.GetValueOrDefault();
					}
				}
				return 0;
			}

			[Null]
			private static CustomCommand DecodeCustomCommand(JToken json, bool shared, int version)
			{
				try
				{
					CustomCommandTarget target = DecodeCustomCommandTarget(json["target"]);
					CustomCommandRefTarget[] referenceTargets = DecodeCustomCommandRefTargets((json["referenceTargets"] as JArray) ?? (json["refTargets"] as JArray));
					string name = json["name"].Value<string>();
					CustomCommandAction customCommandAction = DecodeCustomCommandAction(json["action"]);
					CustomCommandUI customCommandUI = DecodeCustomCommandUI(json["ui"]);
					CustomCommandOS os = DecodeCustomCommandOs(json["os"]);
					if (customCommandAction == null && customCommandUI == null)
					{
						Log.Error("Custom command can't be parsed. Either action or ui must be defined.");
						return null;
					}
					return new CustomCommand(target, referenceTargets, name, customCommandAction, customCommandUI, os, shared, version);
				}
				catch
				{
					Log.Error("Custom command can't be parsed and will be skipped");
					return null;
				}
			}

			[Null]
			private static CustomCommandAction DecodeCustomCommandAction([Null] JToken json)
			{
				if (json == null)
				{
					return null;
				}
				try
				{
					switch (json["type"].Value<string>())
					{
					case "process":
					{
						string? path = json["path"].Value<string>();
						string parameters = json["args"].Value<string>();
						bool showOutput2 = json["showOutput"].Value<bool>();
						bool waitForExit2 = json["waitForExit"].Value<bool>();
						return new ProcessCustomCommandAction(path, parameters, showOutput2, waitForExit2);
					}
					case "sh":
					{
						string? script = json["script"].Value<string>();
						bool showOutput = json["showOutput"].Value<bool>();
						bool waitForExit = json["waitForExit"].Value<bool>();
						return new ShCustomCommandAction(script, showOutput, waitForExit);
					}
					case "url":
						return new UrlCustomCommandAction(json["url"].Value<string>());
					case "cancel":
						return new CancelCustomCommandAction();
					default:
						throw new ParseException();
					}
				}
				catch
				{
					Log.Error("Cannot parse custom command action");
					return null;
				}
			}

			[Null]
			private static CustomCommandUI DecodeCustomCommandUI([Null] JToken json)
			{
				if (json == null)
				{
					return null;
				}
				try
				{
					string? title = json["title"].Value<string>();
					string description = json["description"].Value<string>();
					CustomCommandUI.Control[] controls = DecodeCustomCommandUIControls(json["controls"] as JArray);
					CustomCommandUI.Button[] buttons = DecodeCustomCommandUIButtons(json["buttons"] as JArray);
					return new CustomCommandUI(title, description, controls, buttons);
				}
				catch
				{
					Log.Error("Cannot parse custom command UI");
					return null;
				}
			}

			private static CustomCommandUI.Control[] DecodeCustomCommandUIControls([Null] JArray jArray)
			{
				if (jArray == null)
				{
					return new CustomCommandUI.Control[0];
				}
				List<CustomCommandUI.Control> list = new List<CustomCommandUI.Control>(jArray.Count);
				try
				{
					foreach (JToken item in jArray)
					{
						CustomCommandUI.Control control = DecodeCustomCommandUIControl(item);
						if (control != null)
						{
							list.Add(control);
						}
					}
				}
				catch (Exception arg)
				{
					Log.Error($"Cannot parse ui controls: {arg}");
				}
				return list.ToArray();
			}

			[Null]
			private static CustomCommandUI.Control DecodeCustomCommandUIControl([Null] JToken json)
			{
				if (json == null)
				{
					return null;
				}
				try
				{
					switch (json["type"].Value<string>())
					{
					case "textBox":
					{
						string text4 = json["textBoxType"].Value<string>();
						if (text4 == "generic")
						{
							string text5 = json["title"].Value<string>();
							if (text5 == null)
							{
								return null;
							}
							string text6 = json["text"].Value<string>();
							string placeholder = json["placeholder"].Value<string>();
							return new CustomCommandUI.Control.GenericTextBox(text5, text6, placeholder);
						}
						if (text4 == "filepath")
						{
							string text7 = json["title"].Value<string>();
							if (text7 != null)
							{
								CustomCommandUI.Control.PathTextBox.DialogType? dialogType = DecodePathTextBoxDialogType(json["dialogType"]);
								if (dialogType.HasValue)
								{
									CustomCommandUI.Control.PathTextBox.DialogType valueOrDefault2 = dialogType.GetValueOrDefault();
									string defaultDirectory = json["defaultDirectory"].Value<string>();
									string fileName = json["filename"].Value<string>();
									return new CustomCommandUI.Control.PathTextBox(text7, valueOrDefault2, defaultDirectory, fileName);
								}
							}
							return null;
						}
						return null;
					}
					case "dropdown":
					{
						string text2 = json["title"].Value<string>();
						if (text2 != null)
						{
							string text3 = json["filter"].Value<string>();
							if (text3 != null)
							{
								CustomCommandUI.Control.Dropdown.DropdownType? dropdownType = DecodeDropdownType(json["dropdownType"]);
								if (dropdownType.HasValue)
								{
									CustomCommandUI.Control.Dropdown.DropdownType valueOrDefault = dropdownType.GetValueOrDefault();
									return new CustomCommandUI.Control.Dropdown(text2, valueOrDefault, text3);
								}
							}
						}
						return null;
					}
					case "checkBox":
					{
						string text = json["title"].Value<string>();
						if (text == null)
						{
							return null;
						}
						bool defaultValue = json["defaultValue"].Value<bool>();
						string checkedValue = json["checkedValue"].Value<string>();
						string uncheckedValue = json["uncheckedValue"].Value<string>();
						return new CustomCommandUI.Control.CheckBox(text, defaultValue, checkedValue, uncheckedValue);
					}
					default:
						throw new ParseException();
					}
				}
				catch
				{
					Log.Error("Custom command ui control cannot be parsed and will be skipped");
					return null;
				}
			}

			[Null]
			private static CustomCommandUI.Control.PathTextBox.DialogType? DecodePathTextBoxDialogType([Null] JToken token)
			{
				if (token == null)
				{
					return null;
				}
				string text = token.Value<string>();
				switch (text)
				{
				case "saveFile":
					return CustomCommandUI.Control.PathTextBox.DialogType.SaveFile;
				case "openFile":
					return CustomCommandUI.Control.PathTextBox.DialogType.OpenFile;
				case "openDirectory":
					return CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory;
				default:
					Log.Error("Cannot parse PathTextBox.DialogType in " + text);
					return null;
				}
			}

			[Null]
			private static CustomCommandUI.Control.Dropdown.DropdownType? DecodeDropdownType([Null] JToken token)
			{
				if (token == null)
				{
					return null;
				}
				string text = token.Value<string>();
				if (text == "references")
				{
					return CustomCommandUI.Control.Dropdown.DropdownType.References;
				}
				Log.Error("Cannot parse Dropdown.DropdownType in " + text);
				return null;
			}

			private static CustomCommandUI.Button[] DecodeCustomCommandUIButtons([Null] JArray jArray)
			{
				if (jArray == null)
				{
					return new CustomCommandUI.Button[0];
				}
				List<CustomCommandUI.Button> list = new List<CustomCommandUI.Button>(jArray.Count);
				try
				{
					foreach (JToken item in jArray)
					{
						CustomCommandUI.Button button = DecodeCustomCommandUIButton(item);
						if (button != null)
						{
							list.Add(button);
						}
					}
				}
				catch (Exception arg)
				{
					Log.Error($"Cannot parse ui buttons: {arg}");
				}
				return list.ToArray();
			}

			[Null]
			private static CustomCommandUI.Button DecodeCustomCommandUIButton([Null] JToken json)
			{
				if (json == null)
				{
					return null;
				}
				try
				{
					string title = json["title"].Value<string>();
					CustomCommandAction customCommandAction = DecodeCustomCommandAction(json["action"]);
					if (customCommandAction == null)
					{
						return null;
					}
					return new CustomCommandUI.Button(title, customCommandAction);
				}
				catch
				{
					Log.Error("Custom command button cannot be parsed and will be skipped");
					return null;
				}
			}

			private static CustomCommandOS DecodeCustomCommandOs([Null] JToken token)
			{
				if (token == null)
				{
					return CustomCommandOS.Any;
				}
				string text = token.Value<string>();
				switch (text)
				{
				case "macOS":
					return CustomCommandOS.Mac;
				case "windows":
					return CustomCommandOS.Windows;
				case "any":
					return CustomCommandOS.Any;
				default:
					Log.Error("Cannot parse CustomCommandOS in " + text);
					return CustomCommandOS.Any;
				}
			}

			private static CustomCommandTarget DecodeCustomCommandTarget([Null] JToken token)
			{
				string text = token.Value<string>();
				switch (text)
				{
				case "ref":
					return CustomCommandTarget.Reference;
				case "reference":
					return CustomCommandTarget.Reference;
				case "repository":
					return CustomCommandTarget.Repository;
				case "file":
					return CustomCommandTarget.RepositoryFile;
				case "revision":
					return CustomCommandTarget.Revision;
				case "submodule":
					return CustomCommandTarget.Submodule;
				default:
					Log.Error("Cannot parse CustomActionTarget in " + text);
					throw new ParseException();
				}
			}

			[Null]
			private static CustomCommandRefTarget[] DecodeCustomCommandRefTargets([Null] JArray jsonArray)
			{
				if (jsonArray == null)
				{
					return null;
				}
				try
				{
					List<CustomCommandRefTarget> list = new List<CustomCommandRefTarget>(jsonArray.Count);
					using (IEnumerator<JToken> enumerator = jsonArray.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							switch (enumerator.Current.Value<string>())
							{
							case "localBranch":
							case "localbranch":
								list.Add(CustomCommandRefTarget.LocalBranch);
								break;
							case "remoteBranch":
							case "remotebranch":
								list.Add(CustomCommandRefTarget.RemoteBranch);
								break;
							case "tag":
								list.Add(CustomCommandRefTarget.Tag);
								break;
							}
						}
					}
					if (list.Count > 0)
					{
						return list.ToArray();
					}
				}
				catch
				{
					Log.Error("Cannot parse ref targets");
				}
				return null;
			}
		}

		private CustomCommand[] _customCommands;

		private static readonly object _padlock = new object();

		private static CustomCommandManager _current = null;

		public static CustomCommandManager Current
		{
			get
			{
				lock (_padlock)
				{
					if (_current == null)
					{
						_current = new CustomCommandManager();
					}
					return _current;
				}
			}
		}

		public CustomCommandManager()
		{
			_customCommands = Load();
		}

		public CustomCommand[] GetGlobalCustomCommands()
		{
			return _customCommands;
		}

		public CustomCommand[] GetLocalCustomCommands(RepositoryData repositoryData)
		{
			return repositoryData.CustomCommands;
		}

		public void SetGlobalCustomCommands(CustomCommand[] customCommands)
		{
			Array.Sort(customCommands, (CustomCommand x, CustomCommand y) => x.Name.CompareTo(y.Name));
			_customCommands = customCommands;
			Save(_customCommands, GlobalPath(), removeIfEmpty: false);
		}

		public void SetLocalCustomCommands(GitModule gitModule, CustomCommand[] customCommands)
		{
			Array.Sort(customCommands, (CustomCommand x, CustomCommand y) => x.Name.CompareTo(y.Name));
			List<CustomCommand> customCommands2 = customCommands.Filter((CustomCommand x) => !x.Shared);
			List<CustomCommand> list = customCommands.Filter((CustomCommand x) => x.Shared);
			Save(customCommands2, LocalPath(gitModule), removeIfEmpty: true);
			if (!list.AnyItem((CustomCommand x) => !x.IsVersionSupported()))
			{
				Save(list, SharedLocalPath(gitModule), removeIfEmpty: true);
			}
		}

		public CustomCommand[] GetCustomCommands([Null] RepositoryData repositoryData, CustomCommandTarget target)
		{
			if (repositoryData == null)
			{
				return new CustomCommand[0];
			}
			List<CustomCommand> list = new List<CustomCommand>(_customCommands.Length + repositoryData.CustomCommands.Length);
			CustomCommand[] customCommands = _customCommands;
			foreach (CustomCommand customCommand in customCommands)
			{
				if (customCommand.Target == target)
				{
					list.Add(customCommand);
				}
			}
			customCommands = repositoryData.CustomCommands;
			foreach (CustomCommand customCommand2 in customCommands)
			{
				if (customCommand2.Target == target)
				{
					list.Add(customCommand2);
				}
			}
			return list.ToArray();
		}

		public IReadOnlyList<CustomCommand> GetRepositoryCustomCommands(RepositoryData repositoryData, CustomCommandTarget target)
		{
			return repositoryData.CustomCommands.Filter((CustomCommand x) => x.Target == target);
		}

		private CustomCommand[] Load()
		{
			// Phase 0.2c：App.ForkDirectoryPath → ServiceLocator.AppContext.ForkDirectoryPath
			string text = Path.Combine(ServiceLocator.AppContext.ForkDirectoryPath, "custom-commands.json");
			try
			{
				if (File.Exists(text))
				{
					return Coders.DecodeCustomCommandArray(JsonConvert.DeserializeObject(File.ReadAllText(text)) as JArray, shared: false);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load custom commands from '" + text + "'", ex);
			}
			return new CustomCommand[0];
		}

		public static string GlobalPath()
		{
			return Path.Combine(ServiceLocator.AppContext.ForkDirectoryPath, "custom-commands.json");
		}

		public static string LocalPath(GitModule gitModule)
		{
			return Path.Combine(gitModule.CommonGitDir, "fork", "custom-commands.json");
		}

		public static string SharedLocalPath(GitModule gitModule)
		{
			return Path.Combine(gitModule.Path, ".fork", "custom-commands.json");
		}

		public static CustomCommand[] Load(string path, bool shared = false)
		{
			try
			{
				if (File.Exists(path))
				{
					return Coders.DecodeCustomCommandArray(JsonConvert.DeserializeObject(File.ReadAllText(path)) as JArray, shared);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load custom commands from '" + path + "'", ex);
			}
			return new CustomCommand[0];
		}

		private static void Save(IReadOnlyList<CustomCommand> customCommands, string path, bool removeIfEmpty)
		{
			Log.Debug("Saving custom commands");
			try
			{
				if (removeIfEmpty && customCommands.Count == 0)
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(path));
					string content = BeautifyJson(Sort(Coders.Encode(customCommands)).ToString(Formatting.Indented));
					FileHelper.AtomicWrite(path, content);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Cannot save custom commands to '" + path + "'", ex);
			}
		}

		private static string BeautifyJson(string jsonString)
		{
			string input = jsonString.Replace("\r\n", "\n");
			input = Regex.Replace(input, "(?<!\\\\)\": ", "\" : ");
			try
			{
				JsonConvert.DeserializeObject(input);
				return input;
			}
			catch (Exception ex)
			{
				Log.Error("Json beautifier failed. Fall back to the original json", ex);
				return jsonString;
			}
		}

		private static JArray Sort(JArray jArray)
		{
			return new JArray(jArray.Select((JToken x) => Sort(x)));
		}

		private static JToken Sort(JToken jToken)
		{
			if (!(jToken is JArray jArray))
			{
				if (jToken is JObject jObject)
				{
					return Sort(jObject);
				}
				return jToken;
			}
			return Sort(jArray);
		}

		private static JObject Sort(JObject jObject)
		{
			JObject jObject2 = new JObject();
			foreach (JProperty item in jObject.Properties().OrderBy<JProperty, string>((JProperty prop) => prop.Name, StringComparer.Ordinal))
			{
				jObject2.Add(item.Name, Sort(item.Value));
			}
			return jObject2;
		}
	}
}
