using System;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// 与主工程同款的可空性标注（单文件注解，不引入整包 Nullable）。
	/// v5.0.0 拆分后 public：契约（Abstractions）→ 共享组件（Ui）→ 各插件
	///（Image/Hex/第三方）跨工程使用；命名空间取 ForkPlus.Plugins（而非主工程
	/// 同名的 ForkPlus），测试工程同用两者不会 CS0433 二义。插件内各文件均在
	/// ForkPlus.Plugins.* 子命名空间下，经命名空间向上查找照常解析。
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Delegate)]
	public sealed class NullAttribute : Attribute
	{
	}
}
