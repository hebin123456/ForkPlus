namespace ForkPlus.UI.CustomCommands
{
	/// <summary>
	/// 自定义命令动作抽象基类。
	///
	/// Phase 0.2c：本类从主工程迁入 Core。原 Execute 方法签名为
	/// <c>Execute(RepositoryUserControl, string, CustomCommandEnvironment)</c>，
	/// 其中 RepositoryUserControl 是 WPF UserControl，不能引入 Core。
	/// 改为 <c>Execute(object, ...)</c>，主工程的子类（ProcessCustomCommandAction 等）
	/// 在实现中向下转型回 RepositoryUserControl。
	///
	/// 同理，原本由 CustomCommand.ActionsAreEqual 通过 <c>is ProcessCustomCommandAction</c>
	/// 等类型判断做的相等比较，改为虚方法 <see cref="CustomCommandEquals"/>，
	/// 由各子类实现具体属性比较逻辑。
	/// </summary>
	public abstract class CustomCommandAction
	{
		public static class Keys
		{
			public const string Type = "type";
		}

		/// <summary>
		/// 执行动作。<paramref name="repositoryView"/> 在主工程中是 RepositoryUserControl，
		/// 此处用 object 以避免 Core 引入 WPF 依赖。子类实现时向下转型即可。
		/// </summary>
		public abstract void Execute(object repositoryView, string customCommandName, CustomCommandEnvironment env);

		/// <summary>
		/// 类型相关的相等比较。子类实现：先检查 <paramref name="other"/> 是否同类型，
		/// 再比较具体属性。用于 <see cref="CustomCommand.CustomCommandEquals"/> 中
		/// 比较 Action 字段（替代原本的 <c>is ProcessCustomCommandAction</c> 类型分支）。
		/// </summary>
		public abstract bool CustomCommandEquals(CustomCommandAction other);
	}
}
