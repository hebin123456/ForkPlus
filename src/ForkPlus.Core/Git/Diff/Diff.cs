namespace ForkPlus.Git.Diff
{
    public class Diff
    {
        public enum FileType
        {
            Binary,
            Text,
            Submodule
        }

        [Null]
        public string OldFilepath { get; }

        [Null]
        public string NewFilepath { get; }

        public FileMode? OldFileMode { get; }

        public FileMode? NewFileMode { get; }

        [Null]
        public string SrcObject { get; }

        [Null]
        public string DstObject { get; }

        public string[] Lines { get; }

        public Chunk[] Chunks { get; }

        public int? Similarity { get; }

        public FileType Type { get; }

        public bool IsMinified { get; }

        public Diff([Null] string oldFilepath, [Null] string newFilepath, FileMode? oldFileMode, FileMode? newFileMode, [Null] string srcObject, [Null] string dstObject, string[] lines, Chunk[] chunks, int? similarity, FileType type, bool isMinified)
        {
            OldFilepath = oldFilepath;
            NewFilepath = newFilepath;
            OldFileMode = oldFileMode;
            NewFileMode = newFileMode;
            SrcObject = srcObject;
            DstObject = dstObject;
            Lines = lines;
            Chunks = chunks;
            Similarity = similarity;
            Type = type;
            IsMinified = isMinified;
        }
    }
}
