using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Merge
{
    public class MergeConflict
    {
        public abstract class Chunk
        {
            public List<Line> ResultLines { get; } = new List<Line>();

            public abstract int Height { get; }
        }

        public class ContextChunk : Chunk
        {
            public Line[] Lines { get; }

            public override int Height => Math.Max(Lines.Length, base.ResultLines.Count);

            public ContextChunk(Line[] lines)
            {
                Lines = lines;
                foreach (Line line in lines)
                {
                    line.ParentChunk = this;
                    base.ResultLines.Add(line);
                }
            }
        }

        public class ChangeChunk : Chunk
        {
            public SelectableLine[] RemoteLines { get; }
            public SelectableLine[] LocalLines { get; }

            public override int Height => Math.Max(RemoteLines.Length, Math.Max(LocalLines.Length, base.ResultLines.Count));

            public ChangeChunk(SelectableLine[] remoteLines, SelectableLine[] localLines, bool selectAddedLines)
            {
                RemoteLines = remoteLines;
                LocalLines = localLines;
                SelectableLine[] array = remoteLines;
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].ParentChunk = this;
                }
                array = localLines;
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].ParentChunk = this;
                }
                if (!selectAddedLines)
                {
                    return;
                }
                array = remoteLines;
                foreach (SelectableLine selectableLine in array)
                {
                    if (selectableLine.ChangeType == ContextType.Add)
                    {
                        selectableLine.Select();
                    }
                }
                array = localLines;
                foreach (SelectableLine selectableLine2 in array)
                {
                    if (selectableLine2.ChangeType == ContextType.Add)
                    {
                        selectableLine2.Select();
                    }
                }
            }
        }

        public class ConflictChunk : ChangeChunk
        {
            public string RemoteName { get; }
            public string LocalName { get; }
            public bool IsResolved => base.ResultLines.Count > 0;

            public ConflictChunk(string remoteName, SelectableLine[] remoteLines, string localName, SelectableLine[] localLines)
                : base(remoteLines, localLines, selectAddedLines: false)
            {
                RemoteName = remoteName;
                LocalName = localName;
            }
        }

        public class Line
        {
            public Chunk ParentChunk { get; set; }
            public ContextType ChangeType { get; }
            public string CustomString { get; private set; }
            public string OriginalString { get; }
            public string ResultString => CustomString ?? OriginalString;

            public Line(ContextType changeType, string originalString)
            {
                ChangeType = changeType;
                OriginalString = originalString;
            }

            public void RemoveFromParentChunk()
            {
                if (ParentChunk != null)
                {
                    CustomString = null;
                    ParentChunk.ResultLines.Remove(this);
                }
            }

            public void MeldWith(Line otherLine)
            {
                CustomString = ResultString + otherLine.ResultString;
                if (otherLine is SelectableLine selectableLine)
                {
                    selectableLine.Deselect();
                }
                else
                {
                    otherLine.RemoveFromParentChunk();
                }
            }

            public void RemoveSubrange(Range range)
            {
                string resultString = ResultString;
                CustomString = resultString.Remove(range.Start, range.Length);
                if (CustomString.Length == 0)
                {
                    if (this is SelectableLine selectableLine2)
                    {
                        selectableLine2.Deselect();
                    }
                    else
                    {
                        RemoveFromParentChunk();
                    }
                }
            }

            public Line Insert(string line, int location)
            {
                if (location == ResultString.Length + 1)
                {
                    line = "\n" + line;
                    location--;
                }
                string text = ResultString.Insert(location, line);
                Range? range = FirstEolRange(text);
                if (range.HasValue)
                {
                    Range valueOrDefault = range.GetValueOrDefault();
                    if (valueOrDefault.End != text.Length)
                    {
                        CustomString = text.Substring(0, valueOrDefault.End);
                        string originalString = text.Substring(valueOrDefault.End);
                        return new Line(ContextType.None, originalString);
                    }
                }
                CustomString = text;
                return null;
            }

            private static Range? FirstEolRange(string line)
            {
                int num = line.IndexOf("\n");
                if (num != -1)
                {
                    return new Range(num, num + 1);
                }
                return null;
            }
        }

        public class SelectableLine : Line
        {
            public bool IsSelected { get; private set; }
            public MergeConflictPart ViewMode { get; }

            public SelectableLine(MergeConflictPart viewMode, ContextType changeType, string originalString)
                : base(changeType, originalString)
            {
                ViewMode = viewMode;
                IsSelected = false;
            }

            public void Select()
            {
                if (!IsSelected)
                {
                    if (base.ParentChunk != null)
                    {
                        base.ParentChunk.ResultLines.Add(this);
                    }
                    IsSelected = true;
                }
            }

            public void Deselect()
            {
                if (IsSelected)
                {
                    RemoveFromParentChunk();
                    IsSelected = false;
                }
            }
        }

        public class EmptyLine : SelectableLine
        {
            public EmptyLine(MergeConflictPart viewMode)
                : base(viewMode, ContextType.None, "")
            {
            }
        }

        public bool IsResolved
        {
            get
            {
                Chunk[] chunks = Chunks;
                for (int i = 0; i < chunks.Length; i++)
                {
                    if (chunks[i] is ConflictChunk { IsResolved: false })
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public ConflictStatus ConflictStatus
        {
            get
            {
                int num = 0;
                int num2 = 0;
                Chunk[] chunks = Chunks;
                for (int i = 0; i < chunks.Length; i++)
                {
                    if (chunks[i] is ConflictChunk conflictChunk)
                    {
                        if (conflictChunk.IsResolved)
                        {
                            num++;
                        }
                        num2++;
                    }
                }
                return new ConflictStatus(num, num2);
            }
        }

        public string FilePath { get; }
        public Chunk[] Chunks { get; }
        public bool NoNewLineAtEndOfFile { get; }

        public MergeConflict(string filePath, Chunk[] chunks, bool noNewLineAtEndOfFile)
        {
            FilePath = filePath;
            Chunks = chunks;
            NoNewLineAtEndOfFile = noNewLineAtEndOfFile;
        }
    }
}
