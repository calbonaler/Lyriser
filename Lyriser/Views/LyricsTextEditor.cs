using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Lyriser.Models;

namespace Lyriser.Views
{
	public class LyricsTextEditor : TextEditor
	{
		protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition) => new LyricsHighlightingColorizer(highlightingDefinition);
	}

	public class LyricsHighlightingColorizer : HighlightingColorizer
	{
		public LyricsHighlightingColorizer(IHighlightingDefinition highlightingDefinition) : base(highlightingDefinition) => HighlightingDefinition = highlightingDefinition;

		protected IHighlightingDefinition HighlightingDefinition { get; }

		protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document) => new LyricsSyntaxHighlighter(document, HighlightingDefinition);
	}

	public class HighlightingDefinition : IHighlightingDefinition
	{
		public string Name => nameof(HighlightingDefinition);
		public HighlightingRuleSet MainRuleSet { get; } = new HighlightingRuleSet();
		public HighlightDecorationCollection HighlightDecorations { get; set; } = new HighlightDecorationCollection();
		public IEnumerable<HighlightingColor> NamedHighlightingColors => HighlightDecorations.NamedHighlightingColors;
		public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
		public HighlightingColor GetNamedColor(string name) => HighlightDecorations.GetNamedColor(name);
		public HighlightingRuleSet GetNamedRuleSet(string name) => null;
	}

	public struct HighlightDecoration : IEquatable<HighlightDecoration>
	{
		public string Name { get; set; }
		public Color ForeColor { get; set; }
		public Color BackColor { get; set; }

		public bool Equals(HighlightDecoration other) => Name == other.Name && ForeColor == other.ForeColor && BackColor == other.BackColor;
		public override bool Equals(object obj) => obj is HighlightDecoration other && Equals(other);
		public override int GetHashCode() => Name.GetHashCode() ^ ForeColor.GetHashCode() ^ BackColor.GetHashCode();
		public static bool operator ==(HighlightDecoration left, HighlightDecoration right) => left.Equals(right);
		public static bool operator !=(HighlightDecoration left, HighlightDecoration right) => !(left == right);
	}

	public class HighlightDecorationCollection : IList<HighlightDecoration>, IList
	{
		readonly List<HighlightingColor> m_List = new();
		readonly Dictionary<string, int> m_Dictionary = new();

		public IEnumerable<HighlightingColor> NamedHighlightingColors => m_Dictionary.Values.Select(x => m_List[x]);
		public HighlightingColor GetNamedColor(string name) => m_Dictionary.TryGetValue(name, out var index) ? m_List[index] : null;

		public HighlightDecoration this[int index]
		{
			get => ConvertFrom(m_List[index]);
			set => SetItem(index, value);
		}
		object IList.this[int index]
		{
			get => this[index];
			set => SetItem(index, (HighlightDecoration)value);
		}

		public int Count => m_List.Count;
		public bool IsReadOnly => false;
		bool IList.IsFixedSize => false;
		object ICollection.SyncRoot => this;
		bool ICollection.IsSynchronized => false;

		public void Add(HighlightDecoration item) => InsertItem(m_List.Count, item);
		public void Clear() => ClearItems();
		public bool Contains(HighlightDecoration item) => IndexOf(item) >= 0;
		public void CopyTo(HighlightDecoration[] array, int arrayIndex)
		{
			for (var i = 0; i < Count; i++)
				array[arrayIndex++] = ConvertFrom(m_List[i]);
		}
		public int IndexOf(HighlightDecoration item)
		{
			for (var i = 0; i < Count; i++)
			{
				if (ConvertFrom(m_List[i]) == item)
					return i;
			}
			return -1;
		}
		public void Insert(int index, HighlightDecoration item) => InsertItem(index, item);
		public bool Remove(HighlightDecoration item)
		{
			var index = IndexOf(item);
			if (index < 0)
				return false;
			RemoveItem(index);
			return true;
		}
		public void RemoveAt(int index) => RemoveItem(index);
		public IEnumerator<HighlightDecoration> GetEnumerator() => m_List.Select(ConvertFrom).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		int IList.Add(object value)
		{
			var index = m_List.Count;
			InsertItem(index, (HighlightDecoration)value);
			return index;
		}
		bool IList.Contains(object value) => Contains((HighlightDecoration)value);
		void ICollection.CopyTo(Array array, int index)
		{
			if (array is HighlightDecoration[] typedArray)
				CopyTo(typedArray, index);
			else
			{
				for (var i = 0; i < Count; i++)
					array.SetValue(ConvertFrom(m_List[i]), index++);
			}
		}
		int IList.IndexOf(object value) => IndexOf((HighlightDecoration)value);
		void IList.Insert(int index, object value) => InsertItem(index, (HighlightDecoration)value);
		void IList.Remove(object value) => Remove((HighlightDecoration)value);

		static HighlightingColor ConvertTo(HighlightDecoration highlightDecoration)
		{
			var highlightingColor = new HighlightingColor();
			if (highlightDecoration.ForeColor != default)
				highlightingColor.Foreground = new SimpleHighlightingBrush(highlightDecoration.ForeColor);
			if (highlightDecoration.BackColor != default)
				highlightingColor.Background = new SimpleHighlightingBrush(highlightDecoration.BackColor);
			highlightingColor.Name = highlightDecoration.Name;
			return highlightingColor;
		}
		HighlightDecoration ConvertFrom(HighlightingColor highlightingColor)
		{
			var highlightDecoration = new HighlightDecoration();
			if (highlightingColor != null)
			{
				highlightDecoration.Name = highlightingColor.Name;
				if (highlightingColor.Foreground != null)
				{
					var color = highlightingColor.Foreground.GetColor(null);
					if (color != null)
						highlightDecoration.ForeColor = color.Value;
				}
				if (highlightingColor.Background != null)
				{
					var color = highlightingColor.Background.GetColor(null);
					if (color != null)
						highlightDecoration.BackColor = color.Value;
				}
			}
			return highlightDecoration;
		}

		void InsertItem(int index, HighlightDecoration item)
		{
			if (item.Name != null)
				m_Dictionary.Add(item.Name, index);
			m_List.Insert(index, ConvertTo(item));
		}
		void SetItem(int index, HighlightDecoration item)
		{
			var oldItem = m_List[index];
			if (oldItem.Name != item.Name)
			{
				if (oldItem.Name != null)
					m_Dictionary.Remove(oldItem.Name);
				if (item.Name != null)
					m_Dictionary.Add(item.Name, index);
			}
			m_List[index] = ConvertTo(item);
		}
		void RemoveItem(int index)
		{
			var oldItem = m_List[index];
			if (oldItem.Name != null)
				m_Dictionary.Remove(oldItem.Name);
			m_List.RemoveAt(index);
		}
		void ClearItems()
		{
			m_Dictionary.Clear();
			m_List.Clear();
		}
	}

	public sealed class LyricsSyntaxHighlighter : IHighlighter
	{
		public LyricsSyntaxHighlighter(IDocument document, IHighlightingDefinition highlightingDefinition)
		{
			Document = document;
			HighlightingDefinition = highlightingDefinition;
		}

		public IHighlightingDefinition HighlightingDefinition { get; }
		public IDocument Document { get; }
		public HighlightingColor DefaultTextColor => null;

		[Obsolete("Unused for this class", true)]
		event HighlightingStateChangedEventHandler IHighlighter.HighlightingStateChanged
		{
			add { }
			remove { }
		}

		public void Dispose() { }

		public void BeginHighlighting() { }
		public void EndHighlighting() { }
		public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => Enumerable.Empty<HighlightingColor>();
		public HighlightingColor GetNamedColor(string name) => HighlightingDefinition.GetNamedColor(name);
		public HighlightedLine HighlightLine(int lineNumber)
		{
			var documentLine = Document.GetLineByNumber(lineNumber);
			var lineText = Document.GetText(documentLine);
			var highlightedLine = new HighlightedLine(Document, documentLine);
			var parser = new LyricsParser();
			foreach (var node in parser.ParseLine(lineText))
			{
				foreach (var token in node.Tokens)
				{
					highlightedLine.Sections.Add(new HighlightedSection
					{
						Offset = token.Span.Start.Index + documentLine.Offset,
						Length = token.Span.Length,
						Color = GetNamedColor(token.Label)
					});
				}
			}
			return highlightedLine;
		}
		public void UpdateHighlightingState(int lineNumber) { }
	}
}
