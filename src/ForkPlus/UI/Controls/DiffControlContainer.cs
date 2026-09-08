using System;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.UserControls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DiffControlContainer : Grid, ForkPlus.UI.ILocalizableControl
	{
		public interface IFileDiffControlSubControl
		{
			void ControlWillBeRemovedFromFileDiffControl();
		}

		private global::Avalonia.Controls.Control _subView;

		public FileControlHeaderUserControl Header { get; }

		public global::Avalonia.Controls.Control CurrentSubView => _subView;

		public DiffControlContainer()
		{
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			base.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			Header = new FileControlHeaderUserControl();
			Header.Height = 18.0;
			Header.SetValue(Grid.RowProperty, 0);
			Header.Collapse();
			base.Children.Add(Header);
		}

		public void ApplyLocalization()
		{
			Header.ApplyLocalization();
			if (_subView is ForkPlus.UI.ILocalizableControl localizableControl)
			{
				localizableControl.ApplyLocalization();
			}
		}

		public void ShowSubView<TChild>(Func<TChild> factory, Action<TChild, FileControlHeaderUserControl> initialize = null) where TChild : global::Avalonia.Controls.Control
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
				if (_subView is IFileDiffControlSubControl fileDiffControlSubControl)
				{
					fileDiffControlSubControl.ControlWillBeRemovedFromFileDiffControl();
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

		private bool AttachSubView(global::Avalonia.Controls.Control subView)
		{
			if (subView == null)
			{
				return false;
			}
			subView.SetValue(Grid.RowProperty, 1);
			return VisualTreeAttachmentHelper.TryAddChild(this, subView, GetType().Name + ".SubView");
		}
	}
}
