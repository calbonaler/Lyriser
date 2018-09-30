using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Lyriser
{
	static class GraphicsUtils
	{
		public static RectangleF[] MeasureCharacterRanges(this Graphics graphics, string text, Font font, IEnumerable<CharacterRange> ranges)
		{
			var allRanges = ranges.ToArray();
			var subRangeStartIndex = 0;
			var rects = new List<RectangleF>();
			using (var format = new StringFormat(StringFormat.GenericTypographic))
			{
				format.FormatFlags = StringFormatFlags.MeasureTrailingSpaces;
				while (subRangeStartIndex < allRanges.Length)
				{
					var subRange = new CharacterRange[Math.Min(allRanges.Length - subRangeStartIndex, 32)];
					Array.Copy(allRanges, subRangeStartIndex, subRange, 0, subRange.Length);
					subRangeStartIndex += subRange.Length;
					format.SetMeasurableCharacterRanges(subRange);
					using (var regions = new CompositeDisposable<Region>(graphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, float.PositiveInfinity, float.PositiveInfinity), format)))
						rects.AddRange(regions.Select(r => r.GetBounds(graphics)));
				}
			}
			for (var i = 0; i < allRanges.Length; i++)
			{
				if (allRanges[i].Length == 0)
					rects.Insert(i, new RectangleF(i < rects.Count ? rects[i].X : i > 0 ? rects[i - 1].Right : 0, 0, 0, 0));
			}
			return rects.ToArray();
		}
	}

	sealed class CompositeDisposable<T> : IEnumerable<T>, IDisposable where T : IDisposable
	{
		public CompositeDisposable(T[] items) => _items = items;

		T[] _items;

		public void Dispose()
		{
			if (_items != null)
			{
				foreach (var item in _items)
					item.Dispose();
				_items = null;
			}
		}
		public IEnumerator<T> GetEnumerator() => _items.AsEnumerable().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
