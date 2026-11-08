using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls.Editor.Diff.LineBlame
{
	/// <summary>行号 margin 命中结果：悬停的是旧/新哪一侧、对应两侧原始行号与行在视口中的位置。</summary>
	internal sealed class LineBlameHitInfo
	{
		public bool OldSide;

		public int OldLine;

		public int NewLine;

		public double Top;

		public double Height;
	}

	/// <summary>
	/// 行号 margin 悬停控制器：400ms 悬停后打开 blame 浮窗，鼠标离开行号区/浮窗、
	/// 滚动、Esc 或上下文切换时关闭。浮窗挂在 RepositoryUserControl 根 Panel 中
	///（与 GraphCellView 合并节点浮窗同款），保证 DynamicResource 主题资源可解析。
	/// </summary>
	internal sealed class LineBlameHoverController : IDisposable
	{
		private enum BlameSide
		{
			Old,
			New
		}

		private sealed class BlameRequest
		{
			[Null]
			public string Spec;

			public bool OldSide;

			public int Line;
		}

		private const int HoverDelayMs = 400;

		private const int CloseDelayMs = 500;

		private readonly DiffLineNumberMargin _margin;

		private readonly LineBlameContext _context;

		private readonly DispatcherTimer _hoverTimer;

		private readonly DispatcherTimer _closeTimer;

		[Null]
		private Popup _popup;

		[Null]
		private LineBlamePopupUserControl _content;

		[Null]
		private Panel _popupHost;

		[Null]
		private TopLevel _topLevel;

		[Null]
		private EventHandler<KeyEventArgs> _escapeHandler;

		[Null]
		private EventHandler<PointerEventArgs> _topLevelPointerMovedHandler;

		[Null]
		private EventHandler<PointerEventArgs> _topLevelPointerExitedHandler;

		[Null]
		private EventHandler<PointerPressedEventArgs> _topLevelPointerPressedHandler;

		[Null]
		private EventHandler<PointerWheelEventArgs> _topLevelPointerWheelHandler;

		[Null]
		private EventHandler _scrollHandler;

		[Null]
		private LineBlameHitInfo _hoverHit;

		/// <summary>当前浮窗对应的行（用于判断悬停是否已经换到另一行，避免重复重开）。</summary>
		[Null]
		private LineBlameHitInfo _openHit;

		[Null]
		private LineBlamePopupData _lastData;

		private bool _pointerPressedOnPopup;

		/// <summary>鼠标是否在浮窗内容上（Windows 独立 HWND 下 IsPointerOver 可能滞后，用事件标志兜底）。</summary>
		private bool _pointerOverContent;

		/// <summary>
		/// 鼠标进入过浮窗后置 true：此后不再因鼠标移动/离开自动关窗，必须点击浮窗外、
		/// 滚动、Esc 或切换行号才关闭。避免展开历史时布局变化引起的假 PointerExited
		/// 让面板“稍微一动就消失”。
		/// </summary>
		private bool _popupPinned;

		private bool _disposed;

		private int _generation;

		public LineBlameHoverController(DiffLineNumberMargin margin, LineBlameContext context)
		{
			_margin = margin;
			_context = context;
			_hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoverDelayMs) };
			_hoverTimer.Tick += HoverTimer_Tick;
			_closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CloseDelayMs) };
			_closeTimer.Tick += CloseTimer_Tick;
			TextView textView = margin.TextViewForBlame;
			if (textView != null)
			{
				_scrollHandler = delegate
				{
					ClosePopup();
				};
				textView.ScrollOffsetChanged += _scrollHandler;
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_hoverTimer.Stop();
			_closeTimer.Stop();
			ClosePopup();
			TextView textView = _margin.TextViewForBlame;
			if (textView != null && _scrollHandler != null)
			{
				textView.ScrollOffsetChanged -= _scrollHandler;
				_scrollHandler = null;
			}
		}

		/// <summary>margin 临时离开视觉树（切换标签页等）：只收掉浮窗，保留控制器以便重新挂载后继续悬停。</summary>
		public void OnMarginDetached()
		{
			_hoverTimer.Stop();
			_closeTimer.Stop();
			_hoverHit = null;
			ClosePopup();
		}

		public void OnPointerMoved(PointerEventArgs e)
		{
			if (_disposed)
			{
				return;
			}
			LineBlameHitInfo hit = _margin.HitTestLineBlame(e.GetPosition(_margin));
			if (hit == null)
			{
				_hoverTimer.Stop();
				_hoverHit = null;
				// 不在这里启动关闭定时器：浮窗打开时 Avalonia 可能对静止鼠标重算命中，
				// 产生假 PointerExited；立刻关窗会让用户来不及把鼠标移到浮窗上。
				// 真正离开由顶层 PointerMoved / 浮窗内容事件驱动。
				return;
			}
			if (_hoverHit != null && SameHit(_hoverHit, hit))
			{
				return;
			}
			_hoverTimer.Stop();
			_hoverHit = hit;
			if (_popup != null && _popup.IsOpen)
			{
				// 浮窗已被用户接管（进入/点击/展开过）时，鼠标掠过行号不能立刻关窗：
				// 展开历史会让卡片尺寸变化、位置重算，指针很容易蹭回行号区——
				// 旧逻辑在这里直接 ClosePopup()，用户看到的就是“面板刷新后鼠标一动就没”。
				// 改为交给 400ms 悬停定时器：真的停在新行上才切换，路过不影响。
				if (_popupPinned || IsPointerOverPopupContent())
				{
					_hoverTimer.Start();
					return;
				}
				ClosePopup();
			}
			_hoverTimer.Start();
		}

		private static bool SameHit(LineBlameHitInfo a, LineBlameHitInfo b)
		{
			return a.OldSide == b.OldSide && a.OldLine == b.OldLine && a.NewLine == b.NewLine;
		}

		public void OnPointerExited()
		{
			if (_disposed)
			{
				return;
			}
			_hoverTimer.Stop();
			_hoverHit = null;
			// 同上：离开行号区时不立即关窗——给用户 500ms 从行号移到浮窗上阅读/点击。
			// 若鼠标真的移向别处，顶层 PointerMoved 或浮窗内容 PointerExited 会启动关闭定时器。
		}

		private void HoverTimer_Tick(object sender, EventArgs e)
		{
			_hoverTimer.Stop();
			if (_disposed || _hoverHit == null || !_margin.IsPointerOver)
			{
				return;
			}
			LineBlameHitInfo hit = _hoverHit;
			if (_popup != null && _popup.IsOpen)
			{
				// 同一行：保持现有浮窗（展开历史等操作不会重开）。
				if (_openHit != null && SameHit(_openHit, hit))
				{
					return;
				}
				// 真的换到另一行了才切换。
				ClosePopup();
			}
			int generation = ++_generation;
			try
			{
				OpenPopup(hit);
				Load(generation, hit);
			}
			catch (Exception ex)
			{
				Log.Error("LineBlame popup open failed: " + ex);
				ClosePopup();
			}
		}

		private void CloseTimer_Tick(object sender, EventArgs e)
		{
			_closeTimer.Stop();
			if (_disposed)
			{
				return;
			}
			bool pointerStillPressed = _pointerPressedOnPopup;
			if (_popupPinned || _margin.IsPointerOver || IsPointerOverPopupContent() || pointerStillPressed)
			{
				return;
			}
			ClosePopup();
		}

		/// <summary>指针是否在浮窗内容上（Windows 独立 HWND 下 IsPointerOver 可能滞后，事件标志兜底）。</summary>
		private bool IsPointerOverPopupContent()
		{
			return _pointerOverContent || (_content != null && _content.IsPointerOver);
		}

		private void StartCloseTimer()
		{
			_closeTimer.Stop();
			_closeTimer.Start();
		}

		private void OpenPopup(LineBlameHitInfo hit)
		{
			LineBlamePopupUserControl content = new LineBlamePopupUserControl();
			_content = content;
			_openHit = hit;
			content.ShaClicked += delegate(Sha sha, string path)
			{
				ClosePopup();
				RevealRevision(sha, path ?? _context.Path);
			};
			content.HistoryExpanded += delegate
			{
				// 展开历史本身就是明确的“接管浮窗”动作：立刻钉住，后续内容从加载态
				// 长成列表（卡片尺寸/位置变化）也不会因为指针暂时离开而关窗。
				_popupPinned = true;
				_closeTimer.Stop();
				LoadHistory();
			};
			content.ViewFullHistoryRequested += delegate
			{
				OpenFullHistory();
			};
			content.PointerEntered += delegate
			{
				_pointerOverContent = true;
				_popupPinned = true;
				_closeTimer.Stop();
			};
			content.AddHandler(InputElement.PointerMovedEvent, delegate(object sender, PointerEventArgs e)
			{
				// 指针确实在浮窗上移动过就视为用户接管（不再要求先按下）：
				// 之前只在 _pointerPressedOnPopup 时才钉住，而卡片上的按钮会把
				// PointerPressed 标记 handled，内容级 handler 收不到，导致点过
				// “变更历史”后浮窗仍可能被关闭。
				_pointerOverContent = true;
				_popupPinned = true;
				_closeTimer.Stop();
			}, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			content.PointerExited += delegate
			{
				_pointerOverContent = false;
				if (!_popupPinned)
				{
					StartCloseTimer();
				}
			};
			// handledEventsToo: true —— Button/HyperlinkButton 会在类处理器里把
			// PointerPressed 标记为 handled，普通 += 订阅收不到，钉住状态就丢了。
			content.AddHandler(InputElement.PointerPressedEvent, delegate(object sender, PointerPressedEventArgs e)
			{
				_pointerOverContent = true;
				_popupPinned = true;
				_pointerPressedOnPopup = true;
				_closeTimer.Stop();
			}, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			content.AddHandler(InputElement.PointerReleasedEvent, delegate(object sender, PointerReleasedEventArgs e)
			{
				_pointerPressedOnPopup = false;
			}, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			content.AddHandler(InputElement.PointerCaptureLostEvent, delegate(object sender, PointerCaptureLostEventArgs e)
			{
				_pointerPressedOnPopup = false;
			}, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			content.ShowLoading();

			Popup popup = new Popup
			{
				Placement = PlacementMode.AnchorAndGravity,
				PlacementAnchor = PopupAnchor.TopRight,
				PlacementGravity = PopupGravity.TopLeft,
				PlacementRect = new Rect(Math.Max(0.0, _margin.Bounds.Width - 1.0), Math.Max(0.0, hit.Top), 2.0, Math.Max(1.0, hit.Height)),
				HorizontalOffset = 6.0,
				VerticalOffset = 0.0,
				IsLightDismissEnabled = false,
				PlacementTarget = _margin
			};
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, content, GetType().Name + ".Popup");
			_popup = popup;
			AttachPopupToHost(popup);
			popup.IsOpen = true;
			HookEscape();
		}

		private void AttachPopupToHost(Popup popup)
		{
			Panel fallbackPanel = null;
			global::Avalonia.Visual ancestor = _margin;
			while ((ancestor = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(ancestor)) != null)
			{
				if (fallbackPanel == null && ancestor is Panel nearPanel)
				{
					fallbackPanel = nearPanel;
				}
				if (ancestor is global::ForkPlus.UI.UserControls.RepositoryUserControl)
				{
					foreach (global::Avalonia.Visual child in global::Avalonia.VisualTree.VisualExtensions.GetVisualChildren(ancestor))
					{
						if (child is Panel rootPanel)
						{
							_popupHost = rootPanel;
							break;
						}
					}
					break;
				}
			}
			if (_popupHost == null)
			{
				_popupHost = fallbackPanel;
			}
			if (_popupHost != null)
			{
				_popupHost.Children.Add(popup);
				Popup capturedPopup = popup;
				popup.Closed += delegate
				{
					DetachPopupFromHost(capturedPopup);
				};
			}
		}

		private void DetachPopupFromHost(Popup popup)
		{
			if (_popupHost != null)
			{
				_popupHost.Children.Remove(popup);
				_popupHost = null;
			}
		}

		private void HookEscape()
		{
			_topLevel = TopLevel.GetTopLevel(_margin);
			if (_topLevel == null)
			{
				return;
			}
			_escapeHandler = delegate(object sender, KeyEventArgs e)
			{
				if (e.Key == Key.Escape)
				{
					ClosePopup();
				}
			};
			_topLevel.AddHandler(InputElement.KeyDownEvent, _escapeHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
			// 真实鼠标移动才判定“离开”：滚动/布局变化对静止鼠标的重算不会走这里，
			// 避免浮窗刚打开就被假 PointerExited 关掉。
			_topLevelPointerMovedHandler = delegate(object sender, PointerEventArgs e)
			{
				OnTopLevelPointerMoved();
			};
			_topLevelPointerExitedHandler = delegate(object sender, PointerEventArgs e)
			{
				if (!_popupPinned)
				{
					StartCloseTimer();
				}
			};
			_topLevelPointerPressedHandler = delegate(object sender, PointerPressedEventArgs e)
			{
				if (_popup == null || !_popup.IsOpen)
				{
					return;
				}
				// 点击浮窗内部不关（overlay 模式下事件会到达主窗口顶层）；
				// 点击其它任何位置（编辑器、行号区、工具栏等）视为明确放弃浮窗。
				if (!IsPointerEventFromPopup(e.Source))
				{
					ClosePopup();
				}
			};
			// 鼠标在浮窗上时滚轮由浮窗接管：不滚动下面的 diff 编辑器，也不因此触发
			// ScrollOffsetChanged → 关窗（Windows 独立 HWND 下滚轮本会发给焦点主窗口，
			// Linux/overlay 模式下则会冒泡到主窗口，故统一在顶层 Tunnel 拦截）。
			_topLevelPointerWheelHandler = delegate(object sender, PointerWheelEventArgs e)
			{
				if (_popup == null || !_popup.IsOpen)
				{
					return;
				}
				if (_pointerOverContent || (_content != null && _content.IsPointerOver) || IsPointerEventFromPopup(e.Source))
				{
					e.Handled = true;
					_closeTimer.Stop();
				}
			};
			_topLevel.AddHandler(InputElement.PointerMovedEvent, _topLevelPointerMovedHandler,
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			_topLevel.AddHandler(InputElement.PointerExitedEvent, _topLevelPointerExitedHandler,
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			_topLevel.AddHandler(InputElement.PointerPressedEvent, _topLevelPointerPressedHandler,
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			_topLevel.AddHandler(InputElement.PointerWheelChangedEvent, _topLevelPointerWheelHandler,
				RoutingStrategies.Tunnel, handledEventsToo: true);
		}

		private bool IsPointerEventFromPopup(object source)
		{
			global::Avalonia.Visual visual = source as global::Avalonia.Visual;
			while (visual != null)
			{
				if (visual == _content)
				{
					return true;
				}
				visual = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);
			}
			return false;
		}

		private void OnTopLevelPointerMoved()
		{
			if (_popup == null || !_popup.IsOpen || _popupPinned)
			{
				return;
			}
			// 鼠标在行号区或浮窗内容上 → 保持打开；两者都不在 → 启动延迟关闭。
			// 注意 Windows 独立 HWND 模式下行号区/浮窗内的事件不会到达主窗口顶层时，
			// 浮窗内容自身的 Entered/Moved 会停止本定时器。
			bool overMargin = _margin.IsPointerOver;
			bool overPopup = _pointerOverContent || (_content != null && _content.IsPointerOver);
			if (overMargin || overPopup)
			{
				_closeTimer.Stop();
				return;
			}
			StartCloseTimer();
		}

		private void UnhookEscape()
		{
			if (_topLevel == null)
			{
				return;
			}
			if (_escapeHandler != null)
			{
				_topLevel.RemoveHandler(InputElement.KeyDownEvent, _escapeHandler);
				_escapeHandler = null;
			}
			if (_topLevelPointerMovedHandler != null)
			{
				_topLevel.RemoveHandler(InputElement.PointerMovedEvent, _topLevelPointerMovedHandler);
				_topLevelPointerMovedHandler = null;
			}
			if (_topLevelPointerExitedHandler != null)
			{
				_topLevel.RemoveHandler(InputElement.PointerExitedEvent, _topLevelPointerExitedHandler);
				_topLevelPointerExitedHandler = null;
			}
			if (_topLevelPointerPressedHandler != null)
			{
				_topLevel.RemoveHandler(InputElement.PointerPressedEvent, _topLevelPointerPressedHandler);
				_topLevelPointerPressedHandler = null;
			}
			if (_topLevelPointerWheelHandler != null)
			{
				_topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, _topLevelPointerWheelHandler);
				_topLevelPointerWheelHandler = null;
			}
			_topLevel = null;
		}

		private void ClosePopup()
		{
			_hoverTimer.Stop();
			_closeTimer.Stop();
			_generation++;
			_hoverHit = null;
			_openHit = null;
			_lastData = null;
			_pointerOverContent = false;
			_pointerPressedOnPopup = false;
			_popupPinned = false;
			UnhookEscape();
			if (_popup != null)
			{
				Popup popup = _popup;
				_popup = null;
				_content = null;
				try
				{
					popup.IsOpen = false;
				}
				catch (Exception ex)
				{
					Log.Warn("LineBlame close popup failed", ex);
				}
				VisualTreeAttachmentHelper.TrySetPopupChild(popup, null, GetType().Name + ".Popup");
				DetachPopupFromHost(popup);
			}
			else
			{
				_content = null;
			}
		}

		private void Load(int generation, LineBlameHitInfo hit)
		{
			BlameSide primarySide = (hit.OldSide ? BlameSide.Old : BlameSide.New);
			BlameRequest primaryRequest = CreateRequest(primarySide, hit);
			LineBlameLineItem primaryItem = TryCreateSynthesizedItem(primarySide, hit, primaryRequest);
			if (primaryItem != null)
			{
				LoadOtherAndShow(generation, hit, primarySide, primaryItem);
				return;
			}
			if (primaryRequest == null)
			{
				ShowError(generation, Current("Unable to read commit info"));
				return;
			}
			_context.RequestBlame(primaryRequest.Spec, primaryRequest.OldSide, delegate(GitCommandResult<GetBlameGitCommand.BlameChunk[]> result)
			{
				if (_disposed || generation != _generation || _content == null)
				{
					return;
				}
				if (!result.Succeeded)
				{
					ShowError(generation, GetErrorMessage(result.Error));
					return;
				}
				LineBlameLineItem item = CreateItem(primaryRequest, result.Result);
				if (item == null)
				{
					ShowError(generation, Current("Unable to read commit info"));
					return;
				}
				LoadOtherAndShow(generation, hit, primarySide, item);
			});
		}

		private LineBlameLineItem TryCreateSynthesizedItem(BlameSide primarySide, LineBlameHitInfo hit, [Null] BlameRequest primaryRequest)
		{
			if (_context.Info.Mode != LineBlameMode.WorkingTree || primarySide == BlameSide.Old)
			{
				return null;
			}
			LineBlameUncommittedKind kind = LineBlameUncommittedKind.None;
			if (_context.Info.IsUntracked)
			{
				kind = LineBlameUncommittedKind.Untracked;
			}
			else if (_context.Info.IsStaged && primaryRequest == null)
			{
				// 已暂存的新增行：HEAD 中不存在对应旧行（From 为空）。
				kind = LineBlameUncommittedKind.Staged;
			}
			if (kind == LineBlameUncommittedKind.None)
			{
				return null;
			}
			LineBlameLineItem item = new LineBlameLineItem
			{
				IsUncommitted = true,
				UncommittedKind = kind,
				Line = hit.NewLine,
				Path = _context.Path,
				OldSide = false
			};
			return item;
		}

		private void LoadOtherAndShow(int generation, LineBlameHitInfo hit, BlameSide primarySide, LineBlameLineItem primaryItem)
		{
			BlameSide otherSide = ((primarySide == BlameSide.Old) ? BlameSide.New : BlameSide.Old);
			BlameRequest otherRequest = CreateRequest(otherSide, hit);
			if (otherRequest == null)
			{
				ShowData(generation, hit, primaryItem, null);
				return;
			}
			_context.RequestBlame(otherRequest.Spec, otherRequest.OldSide, delegate(GitCommandResult<GetBlameGitCommand.BlameChunk[]> result)
			{
				if (_disposed || generation != _generation || _content == null)
				{
					return;
				}
				LineBlameLineItem otherItem = null;
				if (result.Succeeded)
				{
					otherItem = CreateItem(otherRequest, result.Result);
					if (otherItem != null && otherItem.IsUncommitted)
					{
						otherItem = null;
					}
				}
				ShowData(generation, hit, primaryItem, otherItem);
			});
		}

		private void ShowData(int generation, LineBlameHitInfo hit, LineBlameLineItem primaryItem, [Null] LineBlameLineItem otherItem)
		{
			if (_disposed || generation != _generation || _content == null)
			{
				return;
			}
			LineBlamePopupData data = BuildPopupData(hit, primaryItem, otherItem);
			_lastData = data;
			_content.ShowData(data);
		}

		private void ShowError(int generation, string message)
		{
			if (_disposed || generation != _generation || _content == null)
			{
				return;
			}
			_content.ShowError(message);
		}

		private LineBlamePopupData BuildPopupData(LineBlameHitInfo hit, LineBlameLineItem primaryItem, [Null] LineBlameLineItem otherItem)
		{
			LineBlamePopupData data = new LineBlamePopupData
			{
				Primary = primaryItem,
				Other = ((otherItem != null && ItemsDiffer(primaryItem, otherItem)) ? otherItem : null),
				FilePath = _context.Path
			};
			if (primaryItem.IsUncommitted || primaryItem.Revision == null)
			{
				return data;
			}
			data.HasHistory = true;
			if (_context.Info.Mode == LineBlameMode.Revision)
			{
				data.HistoryPath = primaryItem.Path;
				data.HistoryLine = primaryItem.Line;
				data.HistoryStartSha = TryParseSha(primaryItem.RevisionSpec);
			}
			else if (hit.OldLine > 0)
			{
				// 工作区模式：按 HEAD 一侧的原始行号查历史（工作区行号不能用于已提交版本）。
				// 文件路径同样取旧一侧路径，兼容重命名。
				data.HistoryPath = _context.GetBlamePath(oldSide: true);
				data.HistoryLine = hit.OldLine;
				data.HistoryStartSha = TryParseSha(_context.Info.OldRevisionSpec);
			}
			else
			{
				data.HistoryPath = primaryItem.Path;
				data.HistoryLine = primaryItem.Line;
				data.HistoryStartSha = null;
			}
			return data;
		}

		private static bool ItemsDiffer(LineBlameLineItem primary, LineBlameLineItem other)
		{
			if (other == null || other.Revision == null)
			{
				return false;
			}
			if (primary.IsUncommitted || primary.Revision == null)
			{
				return true;
			}
			return primary.Revision.Sha != other.Revision.Sha;
		}

		[Null]
		private BlameRequest CreateRequest(BlameSide side, LineBlameHitInfo hit)
		{
			if (side == BlameSide.Old)
			{
				if (hit.OldLine == 0)
				{
					return null;
				}
				return new BlameRequest
				{
					Spec = _context.GetOldRevisionSpec(),
					OldSide = true,
					Line = hit.OldLine
				};
			}
			if (_context.Info.Mode == LineBlameMode.Revision)
			{
				if (hit.NewLine == 0)
				{
					return null;
				}
				return new BlameRequest
				{
					Spec = _context.Info.NewRevisionSpec,
					OldSide = false,
					Line = hit.NewLine
				};
			}
			if (_context.Info.IsUntracked)
			{
				return null;
			}
			if (_context.Info.IsStaged)
			{
				if (hit.NewLine == 0 || hit.OldLine == 0)
				{
					return null;
				}
				// 已暂存内容：暂存区里能追溯到的内容都来自 HEAD，按 HEAD 的旧行号查；
				// 新增行（From 为空）在上层已合成为“已暂存未提交”占位。
				return new BlameRequest
				{
					Spec = _context.GetOldRevisionSpec(),
					OldSide = true,
					Line = hit.OldLine
				};
			}
			if (hit.NewLine == 0)
			{
				return null;
			}
			return new BlameRequest
			{
				Spec = null,
				OldSide = false,
				Line = hit.NewLine
			};
		}

		[Null]
		private LineBlameLineItem CreateItem(BlameRequest request, [Null] GetBlameGitCommand.BlameChunk[] chunks)
		{
			if (!LineBlameContext.TryFindChunk(chunks, request.Line, out GetBlameGitCommand.BlameChunk chunk))
			{
				return null;
			}
			Revision revision = chunk.Revision;
			bool uncommitted = revision == null || revision.Sha == Sha.Zero;
			LineBlameLineItem item = new LineBlameLineItem
			{
				IsUncommitted = uncommitted,
				UncommittedKind = (uncommitted ? (_context.Info.IsStaged ? LineBlameUncommittedKind.Staged : LineBlameUncommittedKind.WorkingTree) : LineBlameUncommittedKind.None),
				Revision = (uncommitted ? null : revision),
				Line = request.Line,
				Path = _context.GetBlamePath(request.OldSide),
				RevisionSpec = request.Spec,
				OldSide = request.OldSide
			};
			return item;
		}

		private void LoadHistory()
		{
			if (_disposed || _content == null)
			{
				return;
			}
			int generation = _generation;
			_content.ShowHistoryLoading();
			// 浮窗数据可能刚被替换：重新装配历史目标，避免使用已过期的行号。
			LineBlamePopupData data = _lastData;
			if (data == null)
			{
				return;
			}
			string historyPath = data.HistoryPath;
			int historyLine = data.HistoryLine;
			Sha? historyStartSha = data.HistoryStartSha;
			_context.RequestHistory(historyPath, historyLine, historyStartSha, delegate(GitCommandResult<(RevisionWithFiles[], string[])> result)
			{
				if (_disposed || generation != _generation || _content == null)
				{
					return;
				}
				if (!result.Succeeded)
				{
					_content.ShowHistoryError(GetErrorMessage(result.Error));
					return;
				}
				_content.ShowHistory(result.Result.Item1);
			});
		}

		private void OpenFullHistory()
		{
			LineBlamePopupData data = _lastData;
			if (data == null || data.HistoryPath == null || data.HistoryLine <= 0)
			{
				return;
			}
			global::ForkPlus.UI.Commands.ShowFileHistoryWindowCommand.Mode.Hunk mode =
				new global::ForkPlus.UI.Commands.ShowFileHistoryWindowCommand.Mode.Hunk(data.HistoryPath, new Range(data.HistoryLine, data.HistoryLine));
			global::ForkPlus.UI.UserControls.RepositoryUserControl repositoryUserControl =
				_context.RepositoryUserControl ?? global::ForkPlus.UI.MainWindow.ActiveRepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			ClosePopup();
			new global::ForkPlus.UI.Commands.ShowFileHistoryWindowCommand().Execute(repositoryUserControl, mode, data.HistoryStartSha);
		}

		private void RevealRevision(Sha sha, string path)
		{
			(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Activate();
			if (global::ForkPlus.UI.MainWindow.ActiveRepositoryUserControl?.GitModule != _context.GitModule)
			{
				global::Avalonia.Application.Current?.TabManager()?.OpenRepository(_context.GitModule.Path);
			}
			global::ForkPlus.UI.MainWindow.ActiveRepositoryUserControl?.ActivateRevisionView();
			global::ForkPlus.UI.MainWindow.ActiveRepositoryUserControl?.SelectRevision(sha, path);
		}

		[Null]
		private static Sha? TryParseSha([Null] string spec)
		{
			if (string.IsNullOrEmpty(spec))
			{
				return null;
			}
			return Sha.Parse(spec);
		}

		private static string GetErrorMessage(GitCommandError error)
		{
			if (error == null)
			{
				return Current("Unable to read commit info");
			}
			string message = error.FriendlyDescription;
			return string.IsNullOrEmpty(message) ? error.ToString() : message;
		}

		private static string Current(string text)
		{
			return PreferencesLocalization.Current(text);
		}
	}
}
