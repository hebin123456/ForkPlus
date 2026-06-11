using System;
using WindowState = System.Windows.WindowState;
using ForkPlus.UI;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Settings
{
	public class CustomDecoders
	{
		internal static JObject Encode(MergeTool target)
		{
			JObject jObject = new JObject();
			if (target is MergeTool.AraxisMerge araxisMerge)
			{
				jObject.Add("Type", new JValue(araxisMerge.Type));
				jObject.Add("ApplicationPath", new JValue(araxisMerge.ApplicationPath));
			}
			else if (target is MergeTool.BeyondCompare beyondCompare)
			{
				jObject.Add("Type", new JValue(beyondCompare.Type));
				jObject.Add("ApplicationPath", new JValue(beyondCompare.ApplicationPath));
			}
			else if (target is MergeTool.KDiff3 kDiff)
			{
				jObject.Add("Type", new JValue(kDiff.Type));
				jObject.Add("ApplicationPath", new JValue(kDiff.ApplicationPath));
			}
			else if (target is MergeTool.P4Merge p4Merge)
			{
				jObject.Add("Type", new JValue(p4Merge.Type));
				jObject.Add("ApplicationPath", new JValue(p4Merge.ApplicationPath));
			}
			else if (target is MergeTool.VisualStudio visualStudio)
			{
				jObject.Add("Type", new JValue(visualStudio.Type));
				jObject.Add("ApplicationPath", new JValue(visualStudio.ApplicationPath));
			}
			else if (target is MergeTool.VSCode vSCode)
			{
				jObject.Add("Type", new JValue(vSCode.Type));
				jObject.Add("ApplicationPath", new JValue(vSCode.ApplicationPath));
			}
			else if (target is MergeTool.Unity3d unity3d)
			{
				jObject.Add("Type", new JValue(unity3d.Type));
				jObject.Add("ApplicationPath", new JValue(unity3d.ApplicationPath));
			}
			else if (target is MergeTool.WinMerge winMerge)
			{
				jObject.Add("Type", new JValue(winMerge.Type));
				jObject.Add("ApplicationPath", new JValue(winMerge.ApplicationPath));
			}
			else if (target is MergeTool.Custom custom)
			{
				jObject.Add("Type", new JValue(custom.Type));
				jObject.Add("ApplicationPath", new JValue(custom.ApplicationPath));
				jObject.Add("Arguments", new JValue(custom.Arguments));
			}
			return jObject;
		}

		internal static JObject EncodeShellTool(ShellTool target)
		{
			JObject jObject = new JObject();
			if (target is ShellTool.Default)
			{
				return null;
			}
			if (target is ShellTool.WindowsTerminal windowsTerminal)
			{
				jObject.Add("Type", new JValue("WindowsTerminal"));
				jObject.Add("ApplicationPath", new JValue(windowsTerminal.ApplicationPath));
			}
			else if (target is ShellTool.CommandPrompt commandPrompt)
			{
				jObject.Add("Type", new JValue("CommandPrompt"));
				jObject.Add("ApplicationPath", new JValue(commandPrompt.ApplicationPath));
			}
			else if (target is ShellTool.PowerShell powerShell)
			{
				jObject.Add("Type", new JValue("PowerShell"));
				jObject.Add("ApplicationPath", new JValue(powerShell.ApplicationPath));
			}
			else if (target is ShellTool.Custom custom)
			{
				jObject.Add("Type", new JValue("Custom"));
				jObject.Add("ApplicationPath", new JValue(custom.ApplicationPath));
				jObject.Add("Arguments", new JValue(custom.Arguments));
			}
			return jObject;
		}

		internal static string DecodeReferenceSpaceCharacterReplacement(JToken json)
		{
			string text = "-";
			string text2 = "_";
			string text3 = json?.Value<string>();
			if (text3 == text)
			{
				return text;
			}
			if (text3 == text2)
			{
				return text2;
			}
			return text;
		}

		[Null]
		internal static MergeTool Decode([Null] JObject json)
		{
			if (json == null)
			{
				return null;
			}
			string propertyName = "Type";
			try
			{
				switch (json[propertyName]?.Value<string>())
				{
				case "AraxisMerge":
				{
					string text9 = json["ApplicationPath"]?.Value<string>();
					if (text9 != null)
					{
						return new MergeTool.AraxisMerge(text9);
					}
					break;
				}
				case "BeyondCompare":
				{
					string text10 = json["ApplicationPath"]?.Value<string>();
					if (text10 != null)
					{
						return new MergeTool.BeyondCompare(text10);
					}
					break;
				}
				case "KDiff3":
				{
					string text8 = json["ApplicationPath"]?.Value<string>();
					if (text8 != null)
					{
						return new MergeTool.KDiff3(text8);
					}
					break;
				}
				case "P4Merge":
				{
					string text6 = json["ApplicationPath"]?.Value<string>();
					if (text6 != null)
					{
						return new MergeTool.P4Merge(text6);
					}
					break;
				}
				case "VisualStudio":
				{
					string text3 = json["ApplicationPath"]?.Value<string>();
					if (text3 != null)
					{
						return new MergeTool.VisualStudio(text3);
					}
					break;
				}
				case "VSCode":
				{
					string text4 = json["ApplicationPath"]?.Value<string>();
					if (text4 != null)
					{
						return new MergeTool.VSCode(text4);
					}
					break;
				}
				case "UnityYAMLMerge":
				{
					string text7 = json["ApplicationPath"]?.Value<string>();
					if (text7 != null)
					{
						return new MergeTool.Unity3d(text7);
					}
					break;
				}
				case "WinMerge":
				{
					string text5 = json["ApplicationPath"]?.Value<string>();
					if (text5 != null)
					{
						return new MergeTool.WinMerge(text5);
					}
					break;
				}
				case "Custom":
				{
					string text = json["ApplicationPath"]?.Value<string>();
					if (text != null)
					{
						string text2 = json["Arguments"]?.Value<string>();
						if (text2 != null)
						{
							return new MergeTool.Custom(text, text2);
						}
					}
					break;
				}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to decode merge tool", ex);
			}
			return null;
		}

		[Null]
		internal static ShellTool DecodeShellTool([Null] JObject json)
		{
			if (json == null)
			{
				return null;
			}
			string propertyName = "Type";
			try
			{
				string text = json[propertyName]?.Value<string>();
				if (text == ShellTool.WindowsTerminalType)
				{
					string text2 = json["ApplicationPath"]?.Value<string>();
					if (text2 != null)
					{
						return new ShellTool.WindowsTerminal(text2);
					}
				}
				else if (text == ShellTool.CommandPromptType)
				{
					string text3 = json["ApplicationPath"]?.Value<string>();
					if (text3 != null)
					{
						return new ShellTool.CommandPrompt(text3);
					}
				}
				else if (text == ShellTool.PowerShellType)
				{
					string text4 = json["ApplicationPath"]?.Value<string>();
					if (text4 != null)
					{
						return new ShellTool.PowerShell(text4);
					}
				}
				else
				{
					if (!(text == ShellTool.CustomType))
					{
						return new ShellTool.Default();
					}
					string text5 = json["ApplicationPath"]?.Value<string>();
					if (text5 != null)
					{
						string arguments = json["Arguments"]?.Value<string>();
						return new ShellTool.Custom(text5, arguments);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to decode shell tool", ex);
			}
			return null;
		}

		internal static WindowLocationState DecodeWindowLocationState([Null] JObject json)
		{
			if (json == null)
			{
				return null;
			}
			try
			{
				double left = json["Left"].Value<double>();
				double top = json["Top"].Value<double>();
				double width = json["Width"].Value<double>();
				double height = json["Height"].Value<double>();
				WindowState windowState = (WindowState)json["WindowState"].Value<int>();
				return new WindowLocationState(left, top, width, height, windowState);
			}
			catch
			{
				return null;
			}
		}

		internal static JObject Encode(WindowLocationState target)
		{
			return new JObject
			{
				{
					"Left",
					new JValue(target.Left)
				},
				{
					"Top",
					new JValue(target.Top)
				},
				{
					"Width",
					new JValue(target.Width)
				},
				{
					"Height",
					new JValue(target.Height)
				},
				{
					"WindowState",
					new JValue((long)target.WindowState)
				}
			};
		}
	}
}
