using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public static class CodeEditorScrollPositionCacheExtensions
	{
		public static void SaveScrollPosition(this CodeEditorScrollPositionCache positionCache, DiffCodeEditor editor)
		{
			VisualDiff visualDiff = editor.VisualPatch?.VisualDiff;
			if (visualDiff != null)
			{
				(int, double) firstVisibleCharacterPosition = editor.GetFirstVisibleCharacterPosition();
				int item = firstVisibleCharacterPosition.Item1;
				double item2 = firstVisibleCharacterPosition.Item2;
				CodeEditorScrollPositionCache.Position? originalLinePosition = visualDiff.GetOriginalLinePosition(item, item2);
				if (originalLinePosition.HasValue)
				{
					CodeEditorScrollPositionCache.Position valueOrDefault = originalLinePosition.GetValueOrDefault();
					positionCache.SetPosition(visualDiff, valueOrDefault);
				}
			}
		}

		public static void SaveScrollPosition(this CodeEditorScrollPositionCache positionCache, DiffCodeEditor srcEditor, DiffCodeEditor dstEditor)
		{
			VisualDiff visualDiff = srcEditor.VisualPatch?.VisualDiff;
			if (visualDiff != null)
			{
				VisualDiff visualDiff2 = dstEditor.VisualPatch?.VisualDiff;
				if (visualDiff2 != null)
				{
					(int, double) firstVisibleCharacterPosition = srcEditor.GetFirstVisibleCharacterPosition();
					int item = firstVisibleCharacterPosition.Item1;
					double item2 = firstVisibleCharacterPosition.Item2;
					(int, double) firstVisibleCharacterPosition2 = dstEditor.GetFirstVisibleCharacterPosition();
					int item3 = firstVisibleCharacterPosition2.Item1;
					double item4 = firstVisibleCharacterPosition2.Item2;
					CodeEditorScrollPositionCache.Position? originalLinePosition = visualDiff.GetOriginalLinePosition(item, item2);
					CodeEditorScrollPositionCache.Position? originalLinePosition2 = visualDiff2.GetOriginalLinePosition(item3, item4);
					CodeEditorScrollPositionCache.Position position = new CodeEditorScrollPositionCache.Position(originalLinePosition?.Src, originalLinePosition2?.Dst, item2);
					positionCache.SetPosition(visualDiff, position);
				}
			}
		}

		public static void RestoreScrollPosition(this CodeEditorScrollPositionCache positionCache, DiffCodeEditor editor)
		{
			VisualDiff visualDiff = editor.VisualPatch?.VisualDiff;
			if (visualDiff != null)
			{
				CodeEditorScrollPositionCache.Position? position = positionCache.GetPosition(visualDiff);
				if (position.HasValue)
				{
					CodeEditorScrollPositionCache.Position valueOrDefault = position.GetValueOrDefault();
					double valueOrDefault2 = editor.GetScrollPositionByCharacterIndex(visualDiff.GetVisualCharacterIndex(valueOrDefault).GetValueOrDefault()).GetValueOrDefault();
					editor.SetScrollPosition(valueOrDefault2 + valueOrDefault.OffsetY);
				}
				else
				{
					int valueOrDefault3 = visualDiff.GetFirstChunkVisualCharacterIndex().GetValueOrDefault();
					double valueOrDefault4 = editor.GetScrollPositionByCharacterIndex(valueOrDefault3).GetValueOrDefault();
					editor.SetScrollPosition(valueOrDefault4);
				}
			}
		}

		public static void RestoreScrollPosition(this CodeEditorScrollPositionCache positionCache, DiffCodeEditor srcEditor, DiffCodeEditor dstEditor)
		{
			VisualDiff visualDiff = srcEditor.VisualPatch?.VisualDiff;
			if (visualDiff == null)
			{
				return;
			}
			VisualDiff visualDiff2 = dstEditor.VisualPatch?.VisualDiff;
			if (visualDiff2 == null)
			{
				return;
			}
			CodeEditorScrollPositionCache.Position? position = positionCache.GetPosition(visualDiff);
			if (position.HasValue)
			{
				CodeEditorScrollPositionCache.Position valueOrDefault = position.GetValueOrDefault();
				if (valueOrDefault.Src.HasValue)
				{
					int valueOrDefault2 = visualDiff.GetVisualCharacterIndex(valueOrDefault).GetValueOrDefault();
					double valueOrDefault3 = srcEditor.GetScrollPositionByCharacterIndex(valueOrDefault2).GetValueOrDefault();
					srcEditor.SetScrollPosition(valueOrDefault3 + valueOrDefault.OffsetY);
					dstEditor.SetScrollPosition(valueOrDefault3 + valueOrDefault.OffsetY);
				}
				else if (valueOrDefault.Dst.HasValue)
				{
					int valueOrDefault4 = visualDiff2.GetVisualCharacterIndex(valueOrDefault).GetValueOrDefault();
					double valueOrDefault5 = dstEditor.GetScrollPositionByCharacterIndex(valueOrDefault4).GetValueOrDefault();
					srcEditor.SetScrollPosition(valueOrDefault5 + valueOrDefault.OffsetY);
					dstEditor.SetScrollPosition(valueOrDefault5 + valueOrDefault.OffsetY);
				}
			}
			else
			{
				int valueOrDefault6 = visualDiff.GetFirstChunkVisualCharacterIndex().GetValueOrDefault();
				double valueOrDefault7 = dstEditor.GetScrollPositionByCharacterIndex(valueOrDefault6).GetValueOrDefault();
				srcEditor.SetScrollPosition(valueOrDefault7);
				dstEditor.SetScrollPosition(valueOrDefault7);
			}
		}

		private static int? GetFirstChunkVisualCharacterIndex(this VisualDiff visualDiff)
		{
			VisualChunk[] visualChunks = visualDiff.VisualChunks;
			int num = 0;
			if (num < visualChunks.Length)
			{
				VisualChunk visualChunk = visualChunks[num];
				if (!visualChunk.HeaderCharRange.HasValue && visualChunk.CustomHunks.Length != 0)
				{
					return visualChunk.VisualLines[visualChunk.CustomHunks[0].Start].Range.Start;
				}
				return visualChunk.CharRange.Start;
			}
			return null;
		}
	}
}
