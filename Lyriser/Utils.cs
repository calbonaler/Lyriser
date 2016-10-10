using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Lyriser
{
	static class GraphicsUtils
	{
		public static int[] MeasureCharacterRangeWidths(this Graphics graphics, string text, Font font, IEnumerable<CharacterRange> ranges, out int offset)
		{
			var allRanges = ranges.ToArray();
			int subRangeStartIndex = 0;
			List<RectangleF> rects = new List<RectangleF>();
			using (StringFormat format = new StringFormat(StringFormatFlags.MeasureTrailingSpaces))
			{
				while (subRangeStartIndex < allRanges.Length)
				{
					var subRange = new CharacterRange[Math.Min(allRanges.Length - subRangeStartIndex, 32)];
					Array.Copy(allRanges, subRangeStartIndex, subRange, 0, subRange.Length);
					subRangeStartIndex += subRange.Length;
					format.SetMeasurableCharacterRanges(subRange);
					using (var regions = new DisposableArray<Region>(graphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, float.PositiveInfinity, float.PositiveInfinity), format)))
						rects.AddRange(regions.Select(r => r.GetBounds(graphics)));
				}
			}
			for (int i = 0; i < allRanges.Length; i++)
			{
				if (allRanges[i].Length == 0)
				{
					var x =
						i < rects.Count ? rects[i].X :
						i > 0 ? rects[i - 1].Right :
						0;
					rects.Insert(i, new RectangleF(x, 0, 0, 0));
				}
			}
			var result = new int[rects.Count];
			for (int i = 0; i < rects.Count; i++)
				result[i] = (int)Math.Round(i + 1 >= rects.Count ? rects[i].Width : rects[i + 1].X - rects[i].X);
			offset = rects.Count > 0 ? (int)Math.Round(rects[0].X) : 0;
			return result;
		}
	}

	static class MathUtils
	{
		public static int CeilingDivide(int dividend, int divisor) => (dividend + divisor - 1) / divisor;
	}

	sealed class DisposableArray<T> : IList<T>, IList, IReadOnlyList<T>, IDisposable where T : IDisposable
	{
		public DisposableArray(T[] items) { _items = items; }

		T[] _items;

		public T this[int index]
		{
			get { return _items[index]; }
			set { _items[index] = value; }
		}
		public int Count => _items.Length;
		public bool IsReadOnly => _items.IsReadOnly;
		public bool IsFixedSize => _items.IsFixedSize;
		public object SyncRoot => _items.SyncRoot;
		public bool IsSynchronized => _items.IsSynchronized;
		object IList.this[int index]
		{
			get { return _items.GetValue(index); }
			set { _items.SetValue(value, index); }
		}

		void ICollection<T>.Add(T item) { throw new NotSupportedException(); }
		void ICollection<T>.Clear() { throw new NotSupportedException(); }
		public bool Contains(T item) => IndexOf(item) >= 0;
		public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
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
		public int IndexOf(T item) => Array.IndexOf(_items, item);
		void IList<T>.Insert(int index, T item) { throw new NotSupportedException(); }
		bool ICollection<T>.Remove(T item) { throw new NotSupportedException(); }
		void IList<T>.RemoveAt(int index) { throw new NotSupportedException(); }
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		int IList.Add(object value) { throw new NotSupportedException(); }
		public bool Contains(object value) => IndexOf(value) >= 0;
		void IList.Clear() { throw new NotSupportedException(); }
		public int IndexOf(object value) => Array.IndexOf(_items, value);
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }
		void IList.Remove(object value) { throw new NotSupportedException(); }
		void IList.RemoveAt(int index) { throw new NotSupportedException(); }
		public void CopyTo(Array array, int index) => _items.CopyTo(array, index);
	}
}
