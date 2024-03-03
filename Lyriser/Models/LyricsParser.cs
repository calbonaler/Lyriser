using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Lyriser.Models;

public class LyricsParser
{
	public Action<ParserError>? ErrorReporter { get; set; }

	static ParserError ErrorSilentImproperlyEnded       (SourceLocation location) => new("E0001", "非発音領域が適切に終了されていません。",       location);
	static ParserError ErrorRubyBaseRequired            (SourceLocation location) => new("E0002", "ルビを振る対象を省略することはできません。",   location);
	static ParserError ErrorRubyStartNotFound           (SourceLocation location) => new("E0003", "ルビ領域でのルビの開始位置が見つかりません。", location);
	static ParserError ErrorRubyImproperlyEnded         (SourceLocation location) => new("E0004", "ルビが適切に終了されていません。",             location);
	static ParserError ErrorRubyRequired                (SourceLocation location) => new("E0005", "ルビを省略することはできません。",             location);
	static ParserError ErrorRubyMustNotBeOnlyWhitespaces(SourceLocation location) => new("E0006", "ルビを空白文字のみとすることはできません。",   location);
	static ParserError ErrorAnyCharacterRequired        (SourceLocation location) => new("E0007", "何らかの文字が必要です。",                     location);

	internal const char RubyBaseStartChar = '|';
	internal const char RubyStartChar = '(';
	internal const char RubyEndChar = ')';
	internal const char SilentStartChar = '[';
	internal const char SilentEndChar = ']';
	internal const char EscapeChar = '`';

	static bool Accept(Scanner scanner, char ch)
	{
		if (scanner.Peek() == ch)
		{
			scanner.Read();
			return true;
		}
		return false;
	}

	IEnumerable<(LyricsNode[] Line, string Terminator)> Parse(Scanner scanner)
	{
		while (true)
		{
			var line = ParseLyricsLine(scanner).ToArray();
			var result = scanner.MoveNextLine(out var rest);
			yield return (line, rest);
			if (!result) break;
		}
	}

	IEnumerable<LyricsNode> ParseLyricsLine(Scanner scanner)
	{
		while (scanner.Peek() != null)
			yield return ParseLyricsNode(scanner);
	}

	LyricsNode ParseLyricsNode(Scanner scanner)
	{
		var silentNode = ParseSilentNode(scanner, ParseLyricsNode);
		if (silentNode != null)
			return silentNode;
		var start = scanner.Location;
		if (Accept(scanner, RubyBaseStartChar))
		{
			var rubyBase = new List<SimpleNode>();
			while (scanner.Peek() is not null and not RubyStartChar)
				rubyBase.Add(ParseSimpleNode(scanner));
			if (rubyBase.Count <= 0)
			{
				ErrorReporter?.Invoke(ErrorRubyBaseRequired(scanner.Location));
				rubyBase.Add(new SimpleNode("_", false, new SourceSpan(scanner.Location, scanner.Location)));
			}
			var rubyNodes = ParseRubyNodes(scanner);
			if (rubyNodes.Count <= 0)
			{
				ErrorReporter?.Invoke(ErrorRubyStartNotFound(scanner.Location));
				rubyNodes.Add(new SimpleNode("_", false, new SourceSpan(scanner.Location, scanner.Location)));
			}
			return new CompositeNode(rubyBase, rubyNodes, true, new SourceSpan(start, scanner.Location));
		}
		else
		{
			var simpleNode = ParseSimpleNode(scanner);
			var rubyNodes = ParseRubyNodes(scanner);
			return rubyNodes.Count > 0
				? new CompositeNode(Enumerable.Repeat(simpleNode, 1), rubyNodes, false, new SourceSpan(start, scanner.Location))
				: simpleNode;
		}
	}

	List<LyricsNode> ParseRubyNodes(Scanner scanner)
	{
		var rubyNodes = new List<LyricsNode>();
		if (Accept(scanner, RubyStartChar))
		{
			var rubyStart = scanner.Location;
			while (!Accept(scanner, RubyEndChar))
			{
				if (scanner.Peek() == null)
				{
					ErrorReporter?.Invoke(ErrorRubyImproperlyEnded(scanner.Location));
					break;
				}
				rubyNodes.Add((LyricsNode?)ParseSilentNode(scanner, ParseSimpleNode) ?? ParseSimpleNode(scanner));
			}
			if (rubyNodes.Count <= 0)
			{
				ErrorReporter?.Invoke(ErrorRubyRequired(rubyStart));
				rubyNodes.Add(new SimpleNode("_", false, new SourceSpan(rubyStart, rubyStart)));
			}
			else if (rubyNodes.All(x => string.IsNullOrWhiteSpace(x.Text)))
			{
				ErrorReporter?.Invoke(ErrorRubyMustNotBeOnlyWhitespaces(rubyStart));
				rubyNodes.Add(new SimpleNode("_", false, new SourceSpan(rubyNodes[^1].Span.End, rubyNodes[^1].Span.End)));
			}
		}
		return rubyNodes;
	}

	SilentNode? ParseSilentNode(Scanner scanner, Func<Scanner, LyricsNode> subParser)
	{
		var start = scanner.Location;
		if (!Accept(scanner, SilentStartChar))
			return null;
		var nodes = new List<LyricsNode>();
		while (!Accept(scanner, SilentEndChar))
		{
			if (scanner.Peek() == null)
			{
				ErrorReporter?.Invoke(ErrorSilentImproperlyEnded(scanner.Location));
				break;
			}
			nodes.Add(subParser(scanner));
		}
		return new SilentNode(nodes, new SourceSpan(start, scanner.Location));
	}

	SimpleNode ParseSimpleNode(Scanner scanner)
	{
		var start = scanner.Location;
		var escaping = false;
		if (Accept(scanner, EscapeChar))
			escaping = true;
		var sb = new StringBuilder();
		if (scanner.Peek() != null)
			sb.Append(scanner.Read());
		else
		{
			ErrorReporter?.Invoke(ErrorAnyCharacterRequired(scanner.Location));
			sb.Append('_');
		}
		// nullability: Because Peek() is tested immediately before, Peek() must not be null
		if (char.IsHighSurrogate(sb[^1]) && scanner.Peek() != null && char.IsLowSurrogate((char)scanner.Peek()!))
			sb.Append(scanner.Read());
		return new SimpleNode(sb.ToString(), escaping, new SourceSpan(start, scanner.Location));
	}

	public IEnumerable<(LyricsNode[] Line, string Terminator)> Parse(string source) => Parse(new Scanner(source));

	public IEnumerable<LyricsNode> ParseLine(string line) => ParseLyricsLine(new Scanner(line));

	public static bool IsEscapeRequired(string code)
	{
		if (code.Length == 0)
			return false;
		var ch = char.ConvertToUtf32(code, 0);
		return ch is RubyBaseStartChar or RubyStartChar or RubyEndChar or SilentStartChar or SilentEndChar or EscapeChar;
	}
}

class Scanner(string text)
{
	readonly string _text = text;
	int _lineIndex = 0;
	int _lineStartIndex = 0;
	int _textIndex = 0;

	public SourceLocation Location => new(_textIndex, _lineIndex + 1, _textIndex - _lineStartIndex + 1);

	public bool MoveNextLine(out string rest)
	{
		var sb = new StringBuilder();
		while (true)
		{
			if (_textIndex >= _text.Length)
			{
				rest = sb.ToString();
				return false;
			}
			var ch = _text[_textIndex++];
			sb.Append(ch);
			if (ch == '\r')
			{
				if (_textIndex < _text.Length && _text[_textIndex] == '\n')
					sb.Append(_text[_textIndex++]);
				break;
			}
			if (ch == '\n')
				break;
		}
		_lineIndex++;
		_lineStartIndex = _textIndex;
		rest = sb.ToString();
		return true;
	}

	public char? Peek() => _textIndex >= _text.Length || _text[_textIndex] == '\r' || _text[_textIndex] == '\n' ? default(char?) : _text[_textIndex];

	public char Read()
	{
		var ch = Peek() ?? throw new InvalidOperationException("Already reached end of line.");
		_textIndex++;
		return ch;
	}
}

public class ParserError(string code, string description, SourceLocation location) : INotifyPropertyChanged
{
	public string Code { get; } = code;
	public string Description { get; } = description;
	public SourceLocation Location { get; } = location;

	[Obsolete("Workaround for .NET memory leak bug", true)]
	event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
	{
		add { }
		remove { }
	}
}

public readonly struct SourceLocation(int index, int line, int column) : IEquatable<SourceLocation>, IComparable<SourceLocation>
{
	public int Index { get; } = index;
	public int Line { get; } = line;
	public int Column { get; } = column;

	public bool Equals(SourceLocation other) => Index == other.Index;
	public int CompareTo(SourceLocation other) => Index.CompareTo(other.Index);
	public override bool Equals(object? obj) => obj is SourceLocation other && Equals(other);
	public override int GetHashCode() => Index;

	public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);
	public static bool operator !=(SourceLocation left, SourceLocation right) => !(left == right);
	public static bool operator <(SourceLocation left, SourceLocation right) => left.CompareTo(right) < 0;
	public static bool operator <=(SourceLocation left, SourceLocation right) => left.CompareTo(right) <= 0;
	public static bool operator >(SourceLocation left, SourceLocation right) => left.CompareTo(right) > 0;
	public static bool operator >=(SourceLocation left, SourceLocation right) => left.CompareTo(right) >= 0;

	public int Difference(SourceLocation reference) => Index - reference.Index;
}

public readonly record struct SourceSpan(SourceLocation Start, SourceLocation End)
{
	public int Length => End.Difference(Start);
}

public readonly record struct HighlightToken(string Label, SourceSpan Span);

public class SyllableStore
{
	readonly List<SubSyllable[]> _syllables = [];
	List<SubSyllable>? _subSyllables;
	int _attachedIndex;

	public void StartGrouping() => _subSyllables ??= [];

	public void StopGrouping()
	{
		if (_subSyllables != null)
		{
			_syllables.Add([.. _subSyllables]);
			_subSyllables = null;
		}
	}

	public void ClearAttachedIndex() => _attachedIndex = -1;

	public void SetAttachedIndex(int value) => _attachedIndex = value;

	public void Add(int characterIndex)
	{
		var subSyllable = new SubSyllable(_attachedIndex, characterIndex);
		if (_subSyllables != null)
			_subSyllables.Add(subSyllable);
		else
			_syllables.Add([subSyllable]);
	}

	public bool HasAnySyllable => _syllables.Count > 0;

	public SubSyllable[][] ToArray() => [.. _syllables];
}

public abstract class LyricsNode(SourceSpan span)
{
	public SourceSpan Span { get; } = span;

	public abstract string GenerateSource();

	public static string GenerateSource(IEnumerable<LyricsNode> nodes) => string.Concat(nodes.Select(x => x.GenerateSource()));

	public abstract void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier>? attachedSpecifiers, SyllableStore? syllableStore);

	public abstract IEnumerable<HighlightToken> Tokens { get; }

	public abstract string Text { get; }
}

public class SimpleNode(string code, bool escaped, SourceSpan span) : LyricsNode(span)
{
	public SimpleNode(string code, SourceSpan span) : this(code, LyricsParser.IsEscapeRequired(code) || code == StartGroupingChar || code == StopGroupingChar, span) { }

	const string StartGroupingChar = "{";
	const string StopGroupingChar = "}";

	public string Code { get; } = code;

	public bool IsEscaped { get; } = escaped;

	public (string, CharacterState) TextAndState =>
		!IsEscaped && Code == StartGroupingChar ? (string.Empty, CharacterState.StartGrouping) :
		!IsEscaped && Code == StopGroupingChar  ? (string.Empty, CharacterState.StopGrouping ) :
		                                          (Code,         CharacterState.Default      );

	public override string Text => TextAndState.Item1;

	public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier>? attachedSpecifiers, SyllableStore? syllableStore)
	{
		if (attachedSpecifiers != null)
			syllableStore?.ClearAttachedIndex();
		var (text, state) = TextAndState;
		if (state == CharacterState.StartGrouping)
			syllableStore?.StartGrouping();
		else if (state == CharacterState.StopGrouping)
			syllableStore?.StopGrouping();
		else
		{
			if (syllableStore != null && !string.IsNullOrWhiteSpace(text))
				syllableStore.Add(textBuilder.Length);
			textBuilder.Append(text);
		}
	}

	public override string GenerateSource() => IsEscaped ? $"{LyricsParser.EscapeChar}{Code}" : Code;

	public override IEnumerable<HighlightToken> Tokens
	{
		get
		{
			var (_, state) = TextAndState;
			if (state is CharacterState.StartGrouping or CharacterState.StopGrouping)
				yield return new HighlightToken("SyllableGrouping", Span);
		}
	}
}

public class SilentNode(IEnumerable<LyricsNode> nodes, SourceSpan span) : LyricsNode(span)
{
	public IReadOnlyList<LyricsNode> Nodes { get; } = nodes.ToArray();

	public override string Text => string.Concat(Nodes.Select(x => x.Text));

	public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier>? attachedSpecifiers, SyllableStore? syllableStore)
	{
		foreach (var node in Nodes)
			node.Transform(textBuilder, attachedSpecifiers, null);
	}

	public override string GenerateSource() => $"{LyricsParser.SilentStartChar}{GenerateSource(Nodes)}{LyricsParser.SilentEndChar}";

	public override IEnumerable<HighlightToken> Tokens
	{
		get
		{
			yield return new HighlightToken("Silent", Span);
			foreach (var node in Nodes)
			{
				foreach (var token in node.Tokens)
					yield return token;
			}
		}
	}
}

public class CompositeNode : LyricsNode
{
	public CompositeNode(IEnumerable<SimpleNode> @base, IEnumerable<LyricsNode> ruby, bool complex, SourceSpan span) : base(span)
	{
		Base = @base.ToArray();
		if (Base.Count <= 0)
			throw new ArgumentException($"Must contains at least one item", nameof(@base));
		Ruby = ruby.ToArray();
		IsComplex = complex;
	}

	public CompositeNode(IEnumerable<SimpleNode> @base, IEnumerable<LyricsNode> ruby, SourceSpan span) : this(@base, ruby, false, span) => IsComplex = Base.Count > 1;

	public IReadOnlyList<SimpleNode> Base { get; }

	public IReadOnlyList<LyricsNode> Ruby { get; }

	public bool IsComplex { get; }

	public override string Text => string.Concat(Base.Select(x => x.Text));

	public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier>? attachedSpecifiers, SyllableStore? syllableStore)
	{
		if (attachedSpecifiers == null)
			throw new NotSupportedException("CompositeNode in ruby not supported");
		var rubyTextBuilder = new StringBuilder();
		var syllableDivision = true;
		foreach (var rubyNode in Ruby)
		{
			if (!IsSyllableDivisionComponent(rubyNode))
				syllableDivision = false;
			syllableStore?.SetAttachedIndex(attachedSpecifiers.Count);
			rubyNode.Transform(rubyTextBuilder, null, syllableStore);
		}
		var text = Text;
		if (syllableDivision)
			attachedSpecifiers.Add(new SyllableDivisionSpecifier(Core.DirectWrite.TextRange.FromStartLength(textBuilder.Length, text.Length), rubyTextBuilder.Length));
		else
			attachedSpecifiers.Add(new RubySpecifier(Core.DirectWrite.TextRange.FromStartLength(textBuilder.Length, text.Length), rubyTextBuilder.ToString()));
		textBuilder.Append(text);
	}

	public override string GenerateSource() =>
		IsComplex ? $"{LyricsParser.RubyBaseStartChar}{GenerateSource(Base)}{LyricsParser.RubyStartChar}{GenerateSource(Ruby)}{LyricsParser.RubyEndChar}" :
					$"{GenerateSource(Base)}{LyricsParser.RubyStartChar}{GenerateSource(Ruby)}{LyricsParser.RubyEndChar}";

	public override IEnumerable<HighlightToken> Tokens
	{
		get
		{
			var syllableDivision = Ruby.All(IsSyllableDivisionComponent);
			yield return new HighlightToken("AttachedBase", new SourceSpan(Base[0].Span.Start, Base[Base.Count - 1].Span.End));
			foreach (var ruby in Ruby)
			{
				yield return new HighlightToken(syllableDivision ? "SyllableDivision" : "Ruby", ruby.Span);
				foreach (var token in ruby.Tokens)
					yield return token;
			}
		}
	}

	static bool IsSyllableDivisionComponent(LyricsNode node)
	{
		var rubyText = node.Text;
		return rubyText == "#" || string.IsNullOrEmpty(rubyText);
	}
}

public enum CharacterState
{
	Default,
	StartGrouping,
	StopGrouping,
}
