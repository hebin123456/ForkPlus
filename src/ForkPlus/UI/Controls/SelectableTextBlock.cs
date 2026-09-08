using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 可选中文本的 TextBlock。
	/// Migration note：原 WPF 实现通过反射调用 PresentationFramework 内部 TextEditor
	/// （System.Windows.Documents.TextEditor.RegisterCommandHandlers + TextBlock.TextContainer）
	/// 让 TextBlock 支持选择/复制。Avalonia 无这些内部类型，Type.GetType 返回 null，
	/// 静态构造函数直接 NRE 崩溃（打开仓库页签时 EnsureLayoutInitialized → CommitUserControl
	/// → FilePathTextBlock → 此类 cctor）。Avalonia 自带
	/// Avalonia.Controls.SelectableTextBlock（TextBlock 派生、支持 Inlines 与选择/复制），
	/// 直接继承，删除全部反射包装。
	/// </summary>
	public class SelectableTextBlock : global::Avalonia.Controls.SelectableTextBlock
	{
		public SelectableTextBlock()
		{
			// Migration note：WPF FocusableProperty.OverrideMetadata(default true) → 构造函数 Focusable = true；
			// FocusVisualStyle 在 Avalonia 无对应（焦点视觉由主题伪类承担），移除。
			Focusable = true;
		}
	}
}
