namespace ForkPlus.Services
{
	/// <summary>
	/// 设计时模式检测抽象（替换 WPF DesignerProperties）
	/// </summary>
	public interface IDesignModeService
	{
		bool IsInDesignMode { get; }
	}
}
