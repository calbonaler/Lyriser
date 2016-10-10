using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Lyriser
{
	static class GraphicsUtils
	{
		public static int[] MeasureCharacterRangeWidths(this Graphics graphics, string text, Font font, IEnumerable<CharacterRange> ranges, out int offset)
		{
			using (StringFormat format = new StringFormat(StringFormatFlags.MeasureTrailingSpaces))
			{
				List<RectangleF> rects = new List<RectangleF>();
				while (ranges.Any())
				{
					var rangeArray = ranges.Take(32).ToArray();
					format.SetMeasurableCharacterRanges(rangeArray);
					int baseIndex = rects.Count;
					rects.AddRange(graphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, float.PositiveInfinity, float.PositiveInfinity), format).Select(r => r.GetBounds(graphics)));
					if (rangeArray.Length != rects.Count - baseIndex)
					{
						for (int i = 0; i < rangeArray.Length; i++)
						{
							if (rangeArray[i].Length == 0)
								rects.Insert(i + baseIndex, new RectangleF(i + baseIndex >= rects.Count ? 0 : rects[i + baseIndex].X, 0, 0, 0));
						}
					}
					ranges = ranges.Skip(32);
				}
				var result = new int[rects.Count];
				for (int i = 0; i < rects.Count; i++)
					result[i] = (int)(i + 1 >= rects.Count ? rects[i].Width : rects[i + 1].X - rects[i].X);
				offset = rects.Count > 0 ? (int)rects[0].X : 0;
				return result;
			}
		}

		public static int MeasureStringWidth(this Graphics graphics, string text, Font font)
		{
			int dummy;
			return MeasureCharacterRangeWidths(graphics, text, font, Enumerable.Repeat(new CharacterRange(0, text.Length), 1), out dummy)[0];
		}
	}

	static class MathUtils
	{
		public static int CeilingDivide(int dividend, int divisor) => (dividend + divisor - 1) / divisor;
	}
}
