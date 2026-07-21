using System;
using System.Text;

namespace ForkPlus.Avalonia.Controls.Editor.Hex
{
    // Phase 2.9：Avalonia 版 HexFormatter（从 WPF 工程原样迁移，纯 BCL 零 AvalonEdit 依赖）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Hex/HexFormatter.cs（158 行）：
    //   - internal static class HexFormatter + internal struct ByteRange
    //   - 把字节数组格式化为固定宽度 hex 文本
    //   - 每行格式（bytesPerRow=16）：
    //     "00000000  48 65 6C 6C 6F 20 57 6F  72 6C 64 21 0A 00 00 00  Hello Wo rld!...."
    //   - 列：offset(8 hex) | 2 空格 | hex（每 8 字节后多 1 空格分组）| 2 空格 | ascii
    //   - 公共方法：Format(byte[], int, bool, bool) / CharOffsetsToByteRange(...)
    //   - 完全不依赖 AvalonEdit，只引用 System / System.Text
    //
    // 迁移策略：原样复制，仅改 namespace（ForkPlus.UI → ForkPlus.Avalonia）。
    internal static class HexFormatter
    {
        /// <summary>格式化整个字节数组为 hex 文本。</summary>
        public static string Format(byte[] bytes, int bytesPerRow, bool showOffset, bool showAscii)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }
            int rowCount = (bytes.Length + bytesPerRow - 1) / bytesPerRow;
            StringBuilder sb = new StringBuilder(bytes.Length * 4 + rowCount * 2);
            for (int row = 0; row < rowCount; row++)
            {
                int offset = row * bytesPerRow;
                AppendLine(sb, bytes, offset, bytesPerRow, showOffset, showAscii);
                if (row < rowCount - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, byte[] bytes, int offset, int bytesPerRow, bool showOffset, bool showAscii)
        {
            int remaining = bytes.Length - offset;
            int count = Math.Min(bytesPerRow, remaining);

            // offset 列
            if (showOffset)
            {
                sb.Append(offset.ToString("X8"));
                sb.Append("  ");
            }

            // hex 列
            int half = bytesPerRow / 2;
            for (int i = 0; i < bytesPerRow; i++)
            {
                if (i < count)
                {
                    AppendHexByte(sb, bytes[offset + i]);
                }
                else
                {
                    sb.Append("  ");
                }
                if (i < bytesPerRow - 1)
                {
                    sb.Append(' ');
                }
                // 半行分组：每 half 字节后多一个空格
                if (half > 0 && i == half - 1)
                {
                    sb.Append(' ');
                }
            }

            // ascii 列
            if (showAscii)
            {
                sb.Append("  ");
                for (int i = 0; i < count; i++)
                {
                    byte b = bytes[offset + i];
                    sb.Append(IsPrintable(b) ? (char)b : '.');
                }
            }
        }

        private static void AppendHexByte(StringBuilder sb, byte b)
        {
            sb.Append(NibbleToHex(b >> 4));
            sb.Append(NibbleToHex(b & 0x0F));
        }

        private static char NibbleToHex(int n)
        {
            return n < 10 ? (char)('0' + n) : (char)('A' + n - 10);
        }

        private static bool IsPrintable(byte b)
        {
            return b >= 0x20 && b < 0x7F;
        }

        /// <summary>
        /// 根据字符偏移区间推算字节区间。
        /// 用于"复制为原始字节"功能：用户在 hex 文本里选中一段，反推对应字节范围。
        /// 实现思路：根据格式化的列宽布局反算每个字符偏移对应的字节索引。
        /// </summary>
        public static ByteRange CharOffsetsToByteRange(int charStart, int charEnd, int bytesPerRow, bool showOffset, bool showAscii)
        {
            // 计算每行的布局宽度（字符数）
            int lineLength = ComputeLineLength(bytesPerRow, showOffset, showAscii);
            if (lineLength <= 0) return new ByteRange(0, 0);

            // 每行末尾有 \n（最后一行除外），按 lineLength+1 切分
            int lineSpan = lineLength + 1;
            int startRow = charStart / lineSpan;
            int endRow = charEnd / lineSpan;
            int startCol = charStart % lineSpan;
            int endCol = charEnd % lineSpan;

            int startByte = startRow * bytesPerRow + ColToByteIndex(startCol, bytesPerRow, showOffset);
            int endByte = endRow * bytesPerRow + ColToByteIndex(endCol, bytesPerRow, showOffset);
            return new ByteRange(startByte, endByte);
        }

        /// <summary>计算每行字符长度（不含换行符）。</summary>
        private static int ComputeLineLength(int bytesPerRow, bool showOffset, bool showAscii)
        {
            int len = 0;
            if (showOffset) len += 8 + 2; // "XXXXXXXX" + "  "
            // hex 列：每字节 2 字符 + 1 空格（最后一字节没空格）+ 半行分组多 1 空格
            len += bytesPerRow * 3 - 1;
            int half = bytesPerRow / 2;
            if (half > 0) len += 1;
            if (showAscii) len += 2 + bytesPerRow; // "  " + ascii
            return len;
        }

        /// <summary>把列位置映射到字节索引（相对于当前行起点）。</summary>
        private static int ColToByteIndex(int col, int bytesPerRow, bool showOffset)
        {
            int offsetWidth = showOffset ? 8 + 2 : 0;
            if (col < offsetWidth) return 0;
            int colInHex = col - offsetWidth;
            int half = bytesPerRow / 2;
            // 每字节占 3 字符（hex + 空格），半行后多 1 空格
            // 字节 i 的起始列 = i * 3 + (i >= half ? 1 : 0)
            int byteIdx = colInHex / 3;
            if (byteIdx >= half && half > 0)
            {
                // 调整：colInHex 包含了半行分组的空格
                byteIdx = (colInHex - 1) / 3;
            }
            if (byteIdx < 0) byteIdx = 0;
            if (byteIdx > bytesPerRow) byteIdx = bytesPerRow;
            return byteIdx;
        }
    }

    internal struct ByteRange
    {
        public int Start { get; }
        public int End { get; }
        public ByteRange(int start, int end) { Start = start; End = end; }
    }
}
