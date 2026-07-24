// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF Grid → Avalonia.Controls.Grid。
// WPF FrameworkElement → Avalonia.Controls.Control（字段类型 + 泛型约束）。
// WPF DependencyProperty.Register + PropertyMetadata → Avalonia StyledProperty + AvaloniaProperty.Register<TOwner, TType>。
// WPF DependencyPropertyChangedEventArgs → Avalonia.AvaloniaPropertyChangedEventArgs（e.Property/e.NewValue API 兼容）。
using System;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class FileContentControl : Grid
	{
		public interface IFileContentControlSubControl
		{
			void ControlWillBeRemovedFromFileContentControl();
		}

		private Control _subView;

		private readonly CodeEditorScrollPositionCache _positionCache = new CodeEditorScrollPositionCache();

		public static readonly TextContentControlCommands Commands = new TextContentControlCommands();

		// 阶段 4.5：WPF DependencyProperty.Register → Avalonia StyledProperty（AvaloniaProperty.Register<TOwner, TType>）。
		public static readonly StyledProperty<GitCommandResult<Content>> ContentProperty =
			AvaloniaProperty.Register<FileContentControl, GitCommandResult<Content>>(nameof(Content));

		private int MaxContentSize => 1048576;

		public FileControlHeaderUserControl Header { get; }

		public RepositoryUserControl RepositoryUserControl { get; set; }

		public GitCommandResult<Content> Content
		{
			get
			{
				return (GitCommandResult<Content>)GetValue(ContentProperty);
			}
			set
			{
				SetValue(ContentProperty, value);
			}
		}

		public FileContentControl()
		{
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			base.RowDefinitions.Add(new RowDefinition());
			Header = new FileControlHeaderUserControl();
			Header.Height = 18.0;
			Header.SetValue(Grid.RowProperty, 0);
			Header.Collapse();
			base.Children.Add(Header);
		}

		public void ShowSubView<TChild>(Func<TChild> factory, Action<TChild, FileControlHeaderUserControl> initialize = null) where TChild : Control
		{
			if (_subView == null)
			{
				_subView = factory();
				if (!AttachSubView(_subView))
				{
					_subView = null;
					return;
				}
			}
			else if (!(_subView is TChild))
			{
				if (_subView is IFileContentControlSubControl fileContentControlSubControl)
				{
					fileContentControlSubControl.ControlWillBeRemovedFromFileContentControl();
				}
				base.Children.Remove(_subView);
				_subView = factory();
				if (!AttachSubView(_subView))
				{
					_subView = null;
					return;
				}
			}
			initialize?.Invoke(_subView as TChild, Header);
		}

		private bool AttachSubView(Control subView)
		{
			if (subView == null)
			{
				return false;
			}
			subView.SetValue(Grid.RowProperty, 1);
			return VisualTreeAttachmentHelper.TryAddChild(this, subView, GetType().Name + ".SubView");
		}

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs)。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == ContentProperty)
			{
				Content = e.NewValue as GitCommandResult<Content>;
				UpdateView();
			}
		}

		protected virtual void UpdateView(bool loadLargeDiff = false)
		{
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			if (Content == null || !Content.Succeeded)
			{
				ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					h.Collapse();
				});
				return;
			}
			Content result = Content.Result;
			// v3.1.0：优先判断 HexContent（携带字节，走 Hex 视图），再回退到普通 BinaryContent（只有大小）
			HexContent hexContent = result as HexContent;
			if (hexContent != null)
			{
				ShowSubView(() => new HexContentControl(), delegate(HexContentControl c, FileControlHeaderUserControl h)
				{
					c.SetContent(hexContent);
					ShowHeader(h, hexContent.Path, FileControlHeaderMode.Hex);
				});
				return;
			}
			BinaryContent binaryContent = result as BinaryContent;
			if (binaryContent != null)
			{
				ShowSubView(() => new BinaryFileContentControl(), delegate(BinaryFileContentControl c, FileControlHeaderUserControl h)
				{
					c.SetContent(gitModule, binaryContent);
					ShowHeader(h, binaryContent.Path);
				});
				return;
			}
			result = Content.Result;
			TextContent textContent = result as TextContent;
			if (textContent == null)
			{
				return;
			}
			if (!loadLargeDiff && textContent.Text.Length > MaxContentSize)
			{
				Log.Info("Changes for '" + textContent.Path + "' are too large to display");
				ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					c.ResetEvents();
					c.FallbackMessage = PreferencesLocalization.Current("Changes are too large to display");
					c.Button1Title = PreferencesLocalization.Current("Load Diff");
					c.Button1Click += delegate
					{
						UpdateView(loadLargeDiff: true);
					};
					h.Collapse();
				});
				return;
			}
			ShowSubView(delegate
			{
				TextContentControl textContentControl2 = new TextContentControl();
				textContentControl2.PositionCache = _positionCache;
				textContentControl2.ContextMenu = new ContextMenu();
				textContentControl2.ContextMenuClosing += delegate
				{
					textContentControl2.ContextMenu.Items.Clear();
				};
				return textContentControl2;
			}, delegate(TextContentControl c, FileControlHeaderUserControl h)
			{
				c.ContextMenuOpening += delegate(object s, ContextMenuEventArgs e)
				{
					if (e.Source is TextContentControl { ContextMenu: var contextMenu } textContentControl)
					{
						contextMenu.Items.Clear();
						Commands.OpenFileInExternalEditor.AddMenuItems(RepositoryUserControl, textContentControl, contextMenu, textContent.Path);
						contextMenu.Items.Add(new Separator());
						Commands.HunkHistory.AddMenuItems(RepositoryUserControl, c, textContent.Path, contextMenu);
						contextMenu.Items.Add(new Separator());
						Commands.Copy.AddMenuItems(textContentControl, contextMenu);
					}
				};
				c.SetContent(textContent);
				ShowHeader(h, textContent.Path);
			});
		}

		public void ShowHeader(FileControlHeaderUserControl header, string filepath, FileControlHeaderMode mode = FileControlHeaderMode.None)
		{
			header.Show(filepath, null, mode);
		}
	}
}
