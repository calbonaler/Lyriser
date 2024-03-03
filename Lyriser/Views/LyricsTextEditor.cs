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

namespace Lyriser.Views;

public class LyricsTextEditor : TextEditor
{
	protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition) => new LyricsHighlightingColorizer(highlightingDefinition);
}

public class LyricsHighlightingColorizer(IHighlightingDefinition highlightingDefinition) : HighlightingColorizer(highlightingDefinition)
{
	protected IHighlightingDefinition HighlightingDefinition { get; } = highlightingDefinition;

	protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document) => new LyricsSyntaxHighlighter(document, HighlightingDefinition);
}

public class HighlightingDefinition : IHighlightingDefinition
{
	public string Name => nameof(HighlightingDefinition);
	public HighlightingRuleSet MainRuleSet { get; } = new HighlightingRuleSet();
	public HighlightDecorationCollection HighlightDecorations { get; set; } = [];
	public IEnumerable<HighlightingColor> NamedHighlightingColors => HighlightDecorations.NamedHighlightingColors;
	public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
	public HighlightingColor? GetNamedColor(string name) => HighlightDecorations.GetNamedColor(name);
	public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
}

public record struct HighlightDecoration(string Name, Color ForeColor, Color BackColor);

public class HighlightDecorationCollection : IList<HighlightDecoration>, IList
{
	readonly List<HighlightingColor> _list = [];
	readonly Dictionary<string, int> _dictionary = [];

	public IEnumerable<HighlightingColor> NamedHighlightingColors => _dictionary.Values.Select(x => _list[x]);
	public HighlightingColor? GetNamedColor(string name) => _dictionary.TryGetValue(name, out var index) ? _list[index] : null;

	public HighlightDecoration this[int index]
	{
		get => ConvertFrom(_list[index]);
		set => SetItem(index, value);
	}
	object? IList.this[int index]
	{
		get => this[index];
		set => SetItem(index, Cast(value));
	}

	public int Count => _list.Count;
	public bool IsReadOnly => false;
	bool IList.IsFixedSize => false;
	object ICollection.SyncRoot => this;
	bool ICollection.IsSynchronized => false;

	public void Add(HighlightDecoration item) => InsertItem(_list.Count, item);
	public void Clear() => ClearItems();
	public bool Contains(HighlightDecoration item) => IndexOf(item) >= 0;
	public void CopyTo(HighlightDecoration[] array, int arrayIndex)
	{
		for (var i = 0; i < Count; i++)
			array[arrayIndex++] = ConvertFrom(_list[i]);
	}
	public int IndexOf(HighlightDecoration item)
	{
		for (var i = 0; i < Count; i++)
		{
			if (ConvertFrom(_list[i]) == item)
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
	public IEnumerator<HighlightDecoration> GetEnumerator() => _list.Select(ConvertFrom).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	int IList.Add(object? value)
	{
		var index = _list.Count;
		InsertItem(index, Cast(value));
		return index;
	}
	bool IList.Contains(object? value) => Contains(Cast(value));
	void ICollection.CopyTo(Array array, int index)
	{
		if (array is HighlightDecoration[] typedArray)
			CopyTo(typedArray, index);
		else
		{
			for (var i = 0; i < Count; i++)
				array.SetValue(ConvertFrom(_list[i]), index++);
		}
	}
	int IList.IndexOf(object? value) => IndexOf(Cast(value));
	void IList.Insert(int index, object? value) => InsertItem(index, Cast(value));
	void IList.Remove(object? value) => Remove(Cast(value));

	static HighlightDecoration Cast(object? value) => value is null ? throw new ArgumentNullException(nameof(value)) : (HighlightDecoration)value;
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
			_dictionary.Add(item.Name, index);
		_list.Insert(index, ConvertTo(item));
	}
	void SetItem(int index, HighlightDecoration item)
	{
		var oldItem = _list[index];
		if (oldItem.Name != item.Name)
		{
			if (oldItem.Name != null)
				_dictionary.Remove(oldItem.Name);
			if (item.Name != null)
				_dictionary.Add(item.Name, index);
		}
		_list[index] = ConvertTo(item);
	}
	void RemoveItem(int index)
	{
		var oldItem = _list[index];
		if (oldItem.Name != null)
			_dictionary.Remove(oldItem.Name);
		_list.RemoveAt(index);
	}
	void ClearItems()
	{
		_dictionary.Clear();
		_list.Clear();
	}
}

public sealed class LyricsSyntaxHighlighter(IDocument document, IHighlightingDefinition highlightingDefinition) : IHighlighter
{
	public IHighlightingDefinition HighlightingDefinition { get; } = highlightingDefinition;
	public IDocument Document { get; } = document;
	public HighlightingColor? DefaultTextColor => null;

	[Obsolete("Unused for this class", true)]
	event HighlightingStateChangedEventHandler IHighlighter.HighlightingStateChanged
	{
		add { }
		remove { }
	}

	public void Dispose() { }

	public void BeginHighlighting() { }
	public void EndHighlighting() { }
	public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => [];
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
