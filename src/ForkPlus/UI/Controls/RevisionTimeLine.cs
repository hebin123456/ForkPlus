using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Controls
{
	public class RevisionTimeLine : FrameworkElement
	{
		private readonly Typeface _typeface = new Typeface(Consts.Fonts.Proportional, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

		private Brush _labelBrush;

		private Brush _alternationBrush;

		private Pen _tickPen;

		private Pen _revisionPen;

		private Brush _activeRevisionBrush;

		private Pen _activeRevisionPen;

		private Sha? _activeRevision;

		private Sha? _activeRevision2;

		private RevisionWithFiles[] _revisions = new RevisionWithFiles[0];

		public Sha? ActiveRevision
		{
			get
			{
				return _activeRevision;
			}
			set
			{
				if (!(_activeRevision == value))
				{
					_activeRevision = value;
					InvalidateVisual();
				}
			}
		}

		public Sha? ActiveRevision2
		{
			get
			{
				return _activeRevision2;
			}
			set
			{
				if (!(_activeRevision2 == value))
				{
					_activeRevision2 = value;
					InvalidateVisual();
				}
			}
		}

		public RevisionWithFiles[] Revisions
		{
			get
			{
				return _revisions;
			}
			set
			{
				if (_revisions != value)
				{
					_revisions = value;
					InvalidateVisual();
				}
			}
		}

		public RevisionTimeLine()
		{
			RefreshBrushes();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			Size renderSize = base.RenderSize;
			drawingContext.DrawRectangle(Theme.RevisionTimeLine.BackgroundBrush, null, new Rect(new Point(0.0, 0.0), new Size(renderSize.Width, renderSize.Height - 30.0)));
			RevisionWithFiles[] revisions = Revisions;
			if (revisions == null || revisions.Length < 2)
			{
				return;
			}
			drawingContext.DrawLine(_tickPen, new Point(0.0, renderSize.Height - 30.0), new Point(renderSize.Width, renderSize.Height - 30.0));
			DateTime dateTime = FirstDayOfMonth(revisions.Map((RevisionWithFiles x) => x.AuthorDate).Min());
			DateTime dateTime2 = FirstDayOfNextMonth(revisions[0].AuthorDate);
			double num = (dateTime2 - dateTime).TotalHours / renderSize.Width;
			DrawRuler(drawingContext, renderSize, dateTime, dateTime2, num);
			RevisionWithFiles revisionWithFiles = null;
			RevisionWithFiles revisionWithFiles2 = null;
			for (int i = 0; i < revisions.Length; i++)
			{
				Sha sha = revisions[i].Sha;
				Sha? activeRevision = ActiveRevision;
				if (sha == activeRevision)
				{
					revisionWithFiles = revisions[i];
					continue;
				}
				sha = revisions[i].Sha;
				activeRevision = ActiveRevision2;
				if (sha == activeRevision)
				{
					revisionWithFiles2 = revisions[i];
					continue;
				}
				double x2 = (revisions[i].AuthorDate - dateTime).TotalHours / num;
				drawingContext.DrawLine(_revisionPen, new Point(x2, 0.0), new Point(x2, renderSize.Height - 30.0));
			}
			if (revisionWithFiles != null)
			{
				DrawActiveRevision(drawingContext, revisionWithFiles, dateTime, num, renderSize.Height - 30.0);
			}
			if (revisionWithFiles2 != null)
			{
				DrawActiveRevision(drawingContext, revisionWithFiles2, dateTime, num, renderSize.Height - 30.0);
			}
		}

		private void DrawActiveRevision(DrawingContext ctx, RevisionWithFiles revision, DateTime startPeriod, double ratio, double height)
		{
			double x = GetX(startPeriod, revision.AuthorDate, ratio);
			PathFigure pathFigure = new PathFigure
			{
				StartPoint = new Point(x - 4.0, 0.0),
				IsClosed = true
			};
			pathFigure.Segments.Add(new PolyLineSegment
			{
				Points = new PointCollection
				{
					new Point(x + 4.0, 0.0),
					new Point(x, 5.0)
				},
				IsStroked = false
			});
			PathGeometry pathGeometry = new PathGeometry();
			pathGeometry.Figures.Add(pathFigure);
			ctx.DrawGeometry(_activeRevisionBrush, null, pathGeometry);
			ctx.DrawLine(_activeRevisionPen, new Point(x, 0.0), new Point(x, height));
		}

		private void DrawRuler(DrawingContext ctx, Size size, DateTime startPeriod, DateTime endPeriod, double ratio)
		{
			if (ratio < 10.0)
			{
				DateTime dateTime = startPeriod;
				while (dateTime <= endPeriod)
				{
					double x = GetX(startPeriod, dateTime, ratio);
					ctx.DrawLine(_tickPen, new Point(x, size.Height - 25.0), new Point(x, size.Height - 30.0));
					ctx.DrawText(CreateFormattedText(dateTime.Month + "/" + dateTime.Year), new Point(x, size.Height - 25.0));
					dateTime = dateTime.AddMonths(1);
				}
				return;
			}
			if (ratio < 60.0)
			{
				DateTime dateTime2 = startPeriod;
				while (dateTime2 < endPeriod)
				{
					DateTime dateTime3 = FirstDayOfNextYear(dateTime2);
					double x2 = GetX(startPeriod, dateTime2, ratio);
					double x3 = GetX(startPeriod, (dateTime3 < endPeriod) ? dateTime3 : endPeriod, ratio);
					Rect rectangle = new Rect(new Point(x2, size.Height - 15.0), new Point(x3, size.Height - 30.0));
					if (dateTime2.Year % 2 == 0)
					{
						ctx.DrawRectangle(_alternationBrush, null, rectangle);
					}
					ctx.DrawText(CreateFormattedText(dateTime2.Year.ToString()), new Point(rectangle.X + rectangle.Width / 2.0, size.Height - 14.0));
					dateTime2 = ((dateTime3 < endPeriod) ? dateTime3 : endPeriod);
				}
				DateTime dateTime4 = startPeriod;
				while (dateTime4 <= endPeriod)
				{
					double x4 = GetX(startPeriod, dateTime4, ratio);
					ctx.DrawLine(_tickPen, new Point(x4, size.Height - 25.0), new Point(x4, size.Height - 30.0));
					ctx.DrawText(CreateFormattedText(dateTime4.Month.ToString()), new Point(x4, size.Height - 26.0));
					dateTime4 = dateTime4.AddMonths(1);
				}
				return;
			}
			DateTime dateTime5 = startPeriod;
			while (dateTime5 <= endPeriod)
			{
				double x5 = GetX(startPeriod, dateTime5, ratio);
				if (dateTime5.Month == 1)
				{
					ctx.DrawLine(_tickPen, new Point(x5, size.Height - 15.0), new Point(x5, size.Height - 25.0));
					ctx.DrawText(CreateFormattedText(dateTime5.Year.ToString()), new Point(x5, size.Height - 15.0));
				}
				else
				{
					ctx.DrawLine(_tickPen, new Point(x5, size.Height - 20.0), new Point(x5, size.Height - 25.0));
				}
				dateTime5 = dateTime5.AddMonths(1);
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
			InvalidateVisual();
		}

		private FormattedText CreateFormattedText(string text, TextAlignment alignment = TextAlignment.Center)
		{
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 9.0, _labelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
			{
				TextAlignment = alignment
			};
		}

		private static DateTime FirstDayOfNextMonth(DateTime dateTime)
		{
			return new DateTime(dateTime.Year, dateTime.Month, 1).AddMonths(1);
		}

		private static DateTime FirstDayOfMonth(DateTime dateTime)
		{
			return new DateTime(dateTime.Year, dateTime.Month, 1);
		}

		private static DateTime FirstDayOfNextYear(DateTime dateTime)
		{
			return new DateTime(dateTime.Year, 1, 1).AddYears(1);
		}

		private static double GetX(DateTime start, DateTime date, double ratio)
		{
			return (date - start).TotalHours / ratio;
		}

		private void RefreshBrushes()
		{
			_labelBrush = Theme.LabelBrush;
			_alternationBrush = Theme.RevisionTimeLine.AlternationBrush;
			_tickPen = new Pen(Theme.RevisionTimeLine.TickBrush, 1.0);
			_revisionPen = new Pen(Theme.RevisionTimeLine.RevisionBrush, 1.0);
			_activeRevisionBrush = Theme.SystemAccentBrush;
			_activeRevisionPen = new Pen(_activeRevisionBrush, 1.0);
		}
	}
}
