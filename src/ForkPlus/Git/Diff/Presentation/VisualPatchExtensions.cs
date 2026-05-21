namespace ForkPlus.Git.Diff.Presentation
{
	public static class VisualPatchExtensions
	{
		[Null]
		public static int? GetVisualLineNumber(this VisualPatch visualPatch, int charIndex)
		{
			VisualChunk[] visualChunks = visualPatch.VisualDiff.VisualChunks;
			foreach (VisualChunk visualChunk in visualChunks)
			{
				if (!visualChunk.CharRange.Contains(charIndex))
				{
					continue;
				}
				int num = 0;
				int num2 = 0;
				VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					if (!visualSubChunk.CharRange.Contains(charIndex))
					{
						num += visualSubChunk.PreContextLines.Length + visualSubChunk.DeletedLines.Length + visualSubChunk.PostContextLines.Length;
						num2 += visualSubChunk.PreContextLines.Length + visualSubChunk.AddedLines.Length + visualSubChunk.PostContextLines.Length;
						continue;
					}
					for (int k = visualSubChunk.PreContextLines.Start; k < visualSubChunk.PreContextLines.End; k++)
					{
						if (visualChunk.VisualLines[k].Range.Contains(charIndex))
						{
							return visualChunk.Node.FromStart + num;
						}
						num++;
						num2++;
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						if (visualChunk.VisualLines[l].Range.Contains(charIndex))
						{
							return visualChunk.Node.FromStart + num;
						}
						num++;
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						if (visualChunk.VisualLines[m].Range.Contains(charIndex))
						{
							return visualChunk.Node.FromStart + num2;
						}
						num2++;
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						if (visualChunk.VisualLines[n].Range.Contains(charIndex))
						{
							return visualChunk.Node.FromStart + num2;
						}
						num++;
						num2++;
					}
				}
			}
			return null;
		}
	}
}
