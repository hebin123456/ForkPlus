using System;
using Avalonia;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Rect → Avalonia.Rect。
	internal static class BtLayoutTreemapResultExtensions
	{
		public static GitCommandResult<(int, Rect)[]> Into(this ref BtLayoutTreemapResult btLayoutTreemapResult)
		{
			return GitCommandResult<(int, Rect)[]>.Success(btLayoutTreemapResult.items.GetStructArray(btLayoutTreemapResult.items_len, (BtTreemapItem btTreemapItem) => ((int, Rect))new ValueTuple<int, Rect>(item2: new Rect(btTreemapItem.rect.x, btTreemapItem.rect.y, btTreemapItem.rect.w, btTreemapItem.rect.h), item1: (int)btTreemapItem.index)));
		}
	}
}
