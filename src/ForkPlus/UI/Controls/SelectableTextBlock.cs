using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	public class SelectableTextBlock : TextBlock
	{
		private class TextEditorWrapper
		{
			private static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

			private static readonly PropertyInfo IsReadOnlyProp = TextEditorType.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);

			private static readonly PropertyInfo TextViewProp = TextEditorType.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);

			private static readonly MethodInfo RegisterMethod = TextEditorType.GetMethod("RegisterCommandHandlers", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[4]
			{
				typeof(Type),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, null);

			private static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

			private static readonly PropertyInfo TextContainerTextViewProp = TextContainerType.GetProperty("TextView");

			private static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

			private readonly object _editor;

			public static void RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners)
			{
				RegisterMethod.Invoke(null, new object[4] { controlType, acceptsRichContent, readOnly, registerEventListeners });
			}

			public static TextEditorWrapper CreateFor(TextBlock tb)
			{
				object value = TextContainerProp.GetValue(tb);
				TextEditorWrapper textEditorWrapper = new TextEditorWrapper(value, tb, isUndoEnabled: false);
				IsReadOnlyProp.SetValue(textEditorWrapper._editor, true);
				TextViewProp.SetValue(textEditorWrapper._editor, TextContainerTextViewProp.GetValue(value));
				return textEditorWrapper;
			}

			public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled)
			{
				_editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new object[3] { textContainer, uiScope, isUndoEnabled }, null);
			}
		}

		private readonly TextEditorWrapper _editor;

		static SelectableTextBlock()
		{
			UIElement.FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata(true));
			FrameworkElement.FocusVisualStyleProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata((object)null));
			TextEditorWrapper.RegisterCommandHandlers(typeof(SelectableTextBlock), acceptsRichContent: true, readOnly: true, registerEventListeners: true);
		}

		public SelectableTextBlock()
		{
			_editor = TextEditorWrapper.CreateFor(this);
		}
	}
}
