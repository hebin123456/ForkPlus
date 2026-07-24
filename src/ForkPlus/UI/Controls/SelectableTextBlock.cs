using Avalonia.Controls;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：WPF SelectableTextBlock 使用反射访问内部 TextEditor 实现文本选择。
	/// Avalonia 原生 SelectableTextBlock 已内置文本选择支持，反射 hack（TextEditorWrapper）完全移除。
	/// </summary>
	public class SelectableTextBlock : global::Avalonia.Controls.SelectableTextBlock
	{
	}
}
