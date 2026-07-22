using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/CodeEditorScrollPositionCache.cs（82 行）：
    //   - public class CodeEditorScrollPositionCache
    //   - 内嵌 struct Position（Src/Dst/OffsetY/LastAccessTime）
    //   - GetPosition/SetPosition 按 VisualDiff 或 string key 缓存滚动位置
    //   - FlushIfNeeded：超 hardLimit 时按 LastAccessTime 淘汰到 softLimit
    //   - MakeKey：VisualDiff.Node.OldFilepath/NewFilepath + Location/DstObject
    //
    // Avalonia 版差异：无（纯 C#，依赖 VisualDiff 来自 ForkPlus.Git.Diff.Presentation，
    // Core 工程可访问）。仅 namespace 改为 ForkPlus.Avalonia.Controls.Editor。
    public class CodeEditorScrollPositionCache
    {
        public struct Position
        {
            public readonly DateTime LastAccessTime;

            public int? Src;

            public int? Dst;

            public double OffsetY;

            public static Position Empty => new Position(0, 0, 0.0);

            public Position(int? src, int? dst, double offsetY)
            {
                Src = src;
                Dst = dst;
                OffsetY = offsetY;
                LastAccessTime = DateTime.UtcNow;
            }
        }

        private readonly int _softLimit = 30;

        private readonly int _hardLimit = 60;

        private Dictionary<string, Position> _cache = new Dictionary<string, Position>();

        public Position? GetPosition(VisualDiff visualDiff)
        {
            string key = MakeKey(visualDiff);
            return GetPosition(key);
        }

        public Position? GetPosition(string key)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        public void SetPosition(VisualDiff visualDiff, Position position)
        {
            string key = MakeKey(visualDiff);
            SetPosition(key, position);
        }

        public void SetPosition(string key, Position position)
        {
            FlushIfNeeded();
            _cache[key] = position;
        }

        private void FlushIfNeeded()
        {
            if (_cache.Count >= _hardLimit)
            {
                KeyValuePair<string, Position>[] array = _cache.OrderBy((KeyValuePair<string, Position> x) => x.Value.LastAccessTime).Skip(_softLimit).ToArray();
                foreach (KeyValuePair<string, Position> keyValuePair in array)
                {
                    _cache.Remove(keyValuePair.Key);
                }
            }
        }

        private string MakeKey(VisualDiff visualDiff)
        {
            string text = visualDiff.Node.OldFilepath ?? visualDiff.Node.NewFilepath;
            string text2 = ((visualDiff.Location != 0) ? visualDiff.Location.ToString() : (visualDiff.Node.DstObject ?? "0000000000000000000000000000000000000000"));
            return text2 + ":" + text;
        }
    }
}
