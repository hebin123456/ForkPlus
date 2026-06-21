namespace ForkPlus.Git.Diff
{
    public class SubChunk
    {
        public Range PreContext { get; }

        public Range Deleted { get; }

        public Range Added { get; }

        public Range PostContext { get; }

        public NoNewLineAtEndOfFile NoNewLineAtEndOfFile { get; }

        public SubChunk(Range preContext, Range deleted, Range added, Range postContext, NoNewLineAtEndOfFile noNewLineAtEndOfFile)
        {
            PreContext = preContext;
            Deleted = deleted;
            Added = added;
            PostContext = postContext;
            NoNewLineAtEndOfFile = noNewLineAtEndOfFile;
        }
    }
}
