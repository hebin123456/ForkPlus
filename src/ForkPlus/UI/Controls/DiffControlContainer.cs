using System;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class DiffControlContainer : Grid, ForkPlus.UI.ILocalizableControl
	{
		public interface IFileDiffControlSubControl
		{
			void ControlWillBeRemovedFromFileDiffControl();
		}

		private FrameworkElement _subView;

		public FileControlHeaderUserControl Header { get; }

		public DiffControlContainer()
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

		public void ApplyLocalization()
		{
			Header.ApplyLocalization();
			if (_subView is ForkPlus.UI.ILocalizableControl localizableControl)
			{
				localizableControl.ApplyLocalization();
			}
		}

		public void ShowSubView<TChild>(Func<TChild> factory, Action<TChild, FileControlHeaderUserControl> initialize = null) where TChild : FrameworkElement
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

		private bool AttachSubView(FrameworkElement subView)
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
