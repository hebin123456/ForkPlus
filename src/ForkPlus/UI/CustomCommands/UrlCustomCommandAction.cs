using System;
using ForkPlus.UI.UserControls;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.CustomCommands
{
	public class UrlCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "url";

			public const string Url = "url";
		}

		public override string TypeKey => Keys.Type;

		public override void WriteProperties(JObject jObject)
		{
			// Phase 0.2c-r2：原 CustomCommandManager.Encode 改为子类虚方法。
			jObject.Add(Keys.Url, new JValue(Url));
		}

		public string Url { get; }

		public UrlCustomCommandAction(string url)
		{
			Url = url;
		}

		// Phase 0.2c：原本由 CustomCommand.ActionsAreEqual 通过 `is UrlCustomCommandAction`
		// 类型分支做比较，逻辑迁入 Core 后改为虚方法分发。
		public override bool CustomCommandEquals(CustomCommandAction other)
		{
			return other is UrlCustomCommandAction u && Url == u.Url;
		}

		public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
		{
			Log.Info("Run url custom action for '" + customCommandName + "'");
			new Uri(env.ReplaceVariablesWithValues(Url, urlEncode: true)).OpenInBrowser();
		}
	}
}
