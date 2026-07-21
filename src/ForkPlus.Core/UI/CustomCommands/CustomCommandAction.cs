using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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
	///
	/// Phase 0.2c-r2：Encode/Decode 同样改为虚方法 + 委托工厂。
	/// - <see cref="TypeKey"/> + <see cref="WriteProperties"/> 替代 Encode 中的类型分支
	/// - <see cref="RegisterDecoder"/> + <see cref="Decode(JObject)"/> 替代 Decode 中的
	///   <c>switch (type)</c>，主工程启动时注册各子类的 decoder 委托
	/// 这样 4 个 *CustomCommandAction 子类（强 WPF 依赖）可留在主工程，Core 端
	/// CustomCommandManager 不再需要直接引用它们。
	/// </summary>
	public abstract class CustomCommandAction
	{
		public static class Keys
		{
			public const string Type = "type";
		}

		/// <summary>
		/// 序列化时的 type 字段值（如 "process" / "sh" / "url" / "cancel"）。
		/// 子类返回常量字符串，替代原 Encode 中 <c>is ProcessCustomCommandAction</c> 类型分支。
		/// </summary>
		public abstract string TypeKey { get; }

		/// <summary>
		/// 把子类特有属性写入 <paramref name="jObject"/>。type 字段由调用方写入，
		/// 本方法只写子类属性。替代原 Encode 中 <c>jObject.Add("path", ...)</c> 等代码。
		/// </summary>
		public abstract void WriteProperties(JObject jObject);

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

		// ===== Phase 0.2c-r2：Decode 委托工厂 =====

		private static readonly Dictionary<string, System.Func<JObject, CustomCommandAction>> _decoders =
			new Dictionary<string, System.Func<JObject, CustomCommandAction>>();

		/// <summary>
		/// 主工程在 App.OnStartup 中注册各子类的 decoder。
		/// Core 端 <see cref="Decode(JObject)"/> 通过 type 字段查表分发。
		/// </summary>
		public static void RegisterDecoder(string typeKey, System.Func<JObject, CustomCommandAction> decoder)
		{
			_decoders[typeKey] = decoder;
		}

		/// <summary>
		/// 按 type 字段查表分发反序列化。未注册的 type 抛 ParseException。
		/// </summary>
		public static CustomCommandAction Decode(JObject json)
		{
			if (json == null)
			{
				return null;
			}
			string typeKey = json[Keys.Type]?.Value<string>();
			if (typeKey == null || !_decoders.TryGetValue(typeKey, out var decoder))
			{
				throw new ParseException();
			}
			return decoder(json);
		}
	}
}
