using ForkPlus.UI.UserControls;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.CustomCommands
{
	public class CancelCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "cancel";
		}

		public override string TypeKey => Keys.Type;

		public override void WriteProperties(JObject jObject)
		{
			// Phase 0.2c-r2：Cancel 动作无子类属性，仅写 type 字段（由调用方写入）。
		}

		// Phase 0.2c：Cancel 动作无属性需要比较，同类型即视为相等。
		public override bool CustomCommandEquals(CustomCommandAction other)
		{
			return other is CancelCustomCommandAction;
		}

		public override void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env)
		{
		}
	}
}
