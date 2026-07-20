using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.CustomCommands
{
	public class CancelCustomCommandAction : CustomCommandAction
	{
		public new static class Keys
		{
			public const string Type = "cancel";
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
