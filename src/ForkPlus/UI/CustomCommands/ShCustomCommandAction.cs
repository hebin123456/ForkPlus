using System;
using System.Diagnostics;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.CustomCommands
{
	public class ShCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "sh";

			public const string Script = "script";

			public const string ShowOutput = "showOutput";

			public const string WaitForExit = "waitForExit";
		}

		private static readonly string FileDefaultScript = "count=$(git log --oneline -- ${file} | wc -l)\n\necho ${file:name} changes count: $count";

		private static readonly string ReferenceDefaultScript = "count=$(git log --oneline ${ref:full} | wc -l)\n\necho ${ref} commits count: $count";

		private static readonly string RevisionDefaultScript = "echo ${sha:abbr} changed files:\n\ngit diff --name-only ${sha}~1 ${sha}";

		private static readonly string RepositoryDefaultScript = "echo ${repo:name} status:\n\ngit status --porcelain";

		private static readonly string SubmoduleDefaultScript = "echo ${submodule} status:\n\ngit submodule update --remote -- ${submodule}";

		public override string TypeKey => Keys.Type;

		public override void WriteProperties(JObject jObject)
		{
			// Phase 0.2c-r2：原 CustomCommandManager.Encode 改为子类虚方法。
			jObject.Add(Keys.Script, new JValue(Script));
			jObject.Add(Keys.ShowOutput, new JValue(ShowOutput));
			jObject.Add(Keys.WaitForExit, new JValue(WaitForExit));
		}

		public string Script { get; }

		public bool ShowOutput { get; }

		public bool WaitForExit { get; }

		public string Path => App.BashPath;

		public ShCustomCommandAction(string script, bool showOutput, bool waitForExit)
		{
			Script = script;
			ShowOutput = showOutput;
			WaitForExit = waitForExit;
		}

		// Phase 0.2c：原本由 CustomCommand.ActionsAreEqual 通过 `is ShCustomCommandAction`
		// 类型分支做比较，逻辑迁入 Core 后改为虚方法分发。
		public override bool CustomCommandEquals(CustomCommandAction other)
		{
			return other is ShCustomCommandAction s
				&& Script == s.Script
				&& ShowOutput == s.ShowOutput
				&& WaitForExit == s.WaitForExit;
		}

		public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
		{
			RepositoryUserControl repositoryUserControl = (RepositoryUserControl)repositoryView;
			Log.Info("Run bash custom action for '" + customCommandName + "'");
			if (!WaitForExit)
			{
				RunProcessActionNoWait(env);
				return;
			}
			string name = env.ReplaceVariablesWithValues(customCommandName);
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult<string> customCommandResult = new RunShCustomCommandActionShellCommand().Execute(this, env, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!customCommandResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, customCommandResult.Error).ShowDialog();
					}
					else
					{
						if (ShowOutput)
						{
							new CustomActionResultWindow(name, customCommandResult.Result).ShowDialog();
						}
						repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
					}
				});
			});
		}

		private void RunProcessActionNoWait(CustomCommandEnvironment environment)
		{
			string path = environment.GitModule.Path;
			string text = environment.ReplaceVariablesWithValues(Script);
			text = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
			try
			{
				using Process process = new Process();
				process.StartInfo = new ProcessStartInfo
				{
					FileName = Path,
					Arguments = "-c " + text.Quotify(),
					UseShellExecute = false,
					WorkingDirectory = path,
					ErrorDialog = false,
					CreateNoWindow = true
				};
				process.Start();
			}
			catch (Exception ex)
			{
				new ErrorWindow(ex.Message).ShowDialog();
			}
		}

		public static string DefaultScript(CustomCommandTarget target)
		{
			return target switch
			{
				CustomCommandTarget.Revision => RevisionDefaultScript, 
				CustomCommandTarget.Repository => RepositoryDefaultScript, 
				CustomCommandTarget.RepositoryFile => FileDefaultScript, 
				CustomCommandTarget.Reference => ReferenceDefaultScript, 
				CustomCommandTarget.Submodule => SubmoduleDefaultScript, 
				_ => "", 
			};
		}
	}
}
