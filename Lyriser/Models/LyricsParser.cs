using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Lyriser.Models
{
	public class LyricsParser
	{
		public Action<ParserError> ErrorReporter { get; set; }

		static ParserError ErrorSilentImproperlyEnded       (SourceLocation location) => new ParserError("E0001", "非発音領域が適切に終了されていません。",       location);
		static ParserError ErrorRubyBaseRequired            (SourceLocation location) => new ParserError("E0002", "ルビを振る対象を省略することはできません。",   location);
		static ParserError ErrorRubyStartNotFound           (SourceLocation location) => new ParserError("E0003", "ルビ領域でのルビの開始位置が見つかりません。", location);
		static ParserError ErrorRubyImproperlyEnded         (SourceLocation location) => new ParserError("E0004", "ルビが適切に終了されていません。",             location);
		static ParserError ErrorRubyRequired                (SourceLocation location) => new ParserError("E0005", "ルビを省略することはできません。",             location);
		static ParserError ErrorRubyMustNotBeOnlyWhitespaces(SourceLocation location) => new ParserError("E0006", "ルビを空白文字のみとすることはできません。",   location);
		static ParserError ErrorAnyCharacterRequired        (SourceLocation location) => new ParserError("E0007", "何らかの文字が必要です。",                     location);

		bool Accept(Scanner scanner, char ch)
		{
			if (scanner.Peek() == ch)
			{
				scanner.Read();
				return true;
			}
			return false;
		}

		IEnumerable<LyricsNode[]> Parse(Scanner scanner)
		{
			while (scanner.MoveNextLine())
				yield return ParseLyricsLine(scanner).ToArray();
		}

		IEnumerable<LyricsNode> ParseLyricsLine(Scanner scanner)
		{
			while (scanner.Peek() != null)
				yield return ParseLyricsNode(scanner);
		}

		LyricsNode ParseLyricsNode(Scanner scanner)
		{
			var start = scanner.Location;
			if (Accept(scanner, '('))
			{
				var nodes = new List<LyricsNode>();
				while (!Accept(scanner, ')'))
				{
					if (scanner.Peek() == null)
					{
						ErrorReporter?.Invoke(ErrorSilentImproperlyEnded(scanner.Location));
						break;
					}
					nodes.Add(ParseLyricsNode(scanner));
				}
				return new SilentNode(nodes, new SourceSpan(start, scanner.Location));
			}
			else if (Accept(scanner, '|'))
			{
				var rubyBaseBuilder = new StringBuilder();
				var rubyBaseStart = scanner.Location;
				while (scanner.Peek() != null && scanner.Peek() != '"')
					rubyBaseBuilder.Append(ParseSimpleCharacter(scanner).Text);
				var rubyBaseEnd = scanner.Location;
				if (rubyBaseBuilder.Length <= 0)
				{
					ErrorReporter?.Invoke(ErrorRubyBaseRequired(rubyBaseStart));
					rubyBaseBuilder.Append("_");
				}
				var rubyNodes = ParseRubyNodes(scanner);
				if (rubyNodes.Count <= 0)
				{
					ErrorReporter?.Invoke(ErrorRubyStartNotFound(scanner.Location));
					rubyNodes.Add(new SimpleNode("_", CharacterState.Default, new SourceSpan(scanner.Location, scanner.Location)));
				}
				return new CompositeNode(rubyBaseBuilder.ToString(), new SourceSpan(rubyBaseStart, rubyBaseEnd), rubyNodes, new SourceSpan(start, scanner.Location));
			}
			else
			{
				var (text, escaping) = ParseSimpleCharacter(scanner);
				var end = scanner.Location;
				var rubyNodes = ParseRubyNodes(scanner);
				return rubyNodes.Count > 0
					? new CompositeNode(text, new SourceSpan(start, end), rubyNodes, new SourceSpan(start, scanner.Location))
					: (LyricsNode)CreateSimpleNode(text, escaping, new SourceSpan(start, end));
			}
		}

		List<SimpleNode> ParseRubyNodes(Scanner scanner)
		{
			var rubyNodes = new List<SimpleNode>();
			if (Accept(scanner, '"'))
			{
				var rubyStart = scanner.Location;
				while (!Accept(scanner, '"'))
				{
					if (scanner.Peek() == null)
					{
						ErrorReporter?.Invoke(ErrorRubyImproperlyEnded(scanner.Location));
						break;
					}
					var start = scanner.Location;
					var (text, escaping) = ParseSimpleCharacter(scanner);
					rubyNodes.Add(CreateSimpleNode(text, escaping, new SourceSpan(start, scanner.Location)));
				}
				if (rubyNodes.Count <= 0)
				{
					ErrorReporter?.Invoke(ErrorRubyRequired(rubyStart));
					rubyNodes.Add(new SimpleNode("_", CharacterState.Default, new SourceSpan(rubyStart, rubyStart)));
				}
				else if (rubyNodes.All(x => string.IsNullOrWhiteSpace(x.Text)))
				{
					ErrorReporter?.Invoke(ErrorRubyMustNotBeOnlyWhitespaces(rubyStart));
					rubyNodes.Add(new SimpleNode("_", CharacterState.Default, new SourceSpan(rubyNodes[rubyNodes.Count - 1].Span.End, rubyNodes[rubyNodes.Count - 1].Span.End)));
				}
			}
			return rubyNodes;
		}

		(string Text, bool Escaping) ParseSimpleCharacter(Scanner scanner)
		{
			var escaping = false;
			if (Accept(scanner, '`'))
				escaping = true;
			var sb = new StringBuilder();
			if (scanner.Peek() != null)
				sb.Append(scanner.Read());
			else
			{
				ErrorReporter?.Invoke(ErrorAnyCharacterRequired(scanner.Location));
				sb.Append('_');
			}
			if (char.IsHighSurrogate(sb[sb.Length - 1]) && scanner.Peek() != null && char.IsLowSurrogate((char)scanner.Peek()))
				sb.Append(scanner.Read());
			return (sb.ToString(), escaping);
		}

		SimpleNode CreateSimpleNode(string text, bool escaping, SourceSpan span)
		{
			var state = CharacterState.Default;
			if (!escaping)
			{
				if (text == "{")
					state = CharacterState.StartGrouping;
				else if (text == "}")
					state = CharacterState.StopGrouping;
			}
			return new SimpleNode(text, state, span);
		}

		public LyricsSource Parse(string source)
		{
			var attachedSpecs = new List<AttachedSpecifier>();
			var baseTextBuilder = new StringBuilder();
			var physicalLines = new List<PhysicalLine>();
			var logicalToPhysicalMap = new List<int>();
			var syllableLines = new List<SubSyllable[][]>();
			var firstLine = true;
			foreach (var nodes in Parse(new Scanner(source)))
			{
				if (!firstLine)
					baseTextBuilder.Append('\n');
				var textStart = baseTextBuilder.Length;
				var attachedStart = attachedSpecs.Count;
				var syllableStore = new SyllableStore();
				foreach (var node in nodes)
					node.Transform(baseTextBuilder, attachedSpecs, syllableStore);
				physicalLines.Add(new PhysicalLine(textStart, baseTextBuilder.Length - textStart, attachedStart, attachedSpecs.Count - attachedStart));
				if (syllableStore.HasAnySyllable)
				{
					logicalToPhysicalMap.Add(physicalLines.Count - 1);
					syllableLines.Add(syllableStore.ToArray());
				}
				firstLine = false;
			}
			return new LyricsSource(baseTextBuilder.ToString(), attachedSpecs, new LineMap(physicalLines, logicalToPhysicalMap), syllableLines);
		}

		public HighlightToken[] ParseLine(string line)
		{
			var tokens = new List<HighlightToken>();
			var scanner = new Scanner(line);
			if (scanner.MoveNextLine())
			{
				foreach (var node in ParseLyricsLine(scanner))
					tokens.AddRange(node.Tokens);
			}
			return tokens.ToArray();
		}
	}

	class Scanner
	{
		public Scanner(string text) => _text = text;

		readonly string _text;
		int _lineIndex = -1;
		int _lineStartIndex;
		int _textIndex = 0;

		public SourceLocation Location => new SourceLocation(_textIndex, _lineIndex + 1, _textIndex - _lineStartIndex + 1);

		public bool MoveNextLine()
		{
			if (_lineIndex >= 0)
			{
				while (true)
				{
					if (_textIndex >= _text.Length)
						return false;
					var ch = _text[_textIndex++];
					if (ch == '\r')
					{
						if (_textIndex < _text.Length && _text[_textIndex] == '\n')
							_textIndex++;
						break;
					}
					if (ch == '\n')
						break;
				}
			}
			_lineIndex++;
			_lineStartIndex = _textIndex;
			return true;
		}

		public char? Peek() => _textIndex >= _text.Length || _text[_textIndex] == '\r' || _text[_textIndex] == '\n' ? default(char?) : _text[_textIndex];

		public char Read()
		{
			var ch = Peek();
			if (ch == null)
				throw new InvalidOperationException("Already reached end of line.");
			_textIndex++;
			return ch.Value;
		}
	}

	public class ParserError : INotifyPropertyChanged
	{
		public ParserError(string code, string description, SourceLocation location)
		{
			Code = code;
			Description = description;
			Location = location;
		}

		public string Code { get; }
		public string Description { get; }
		public SourceLocation Location { get; }

		[Obsolete("Workaround for .NET memory leak bug", true)]
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
		{
			add { }
			remove { }
		}
	}

	public readonly struct SourceLocation : IEquatable<SourceLocation>, IComparable<SourceLocation>
	{
		public SourceLocation(int index, int line, int column)
		{
			Index = index;
			Line = line;
			Column = column;
		}

		public int Index { get; }
		public int Line { get; }
		public int Column { get; }

		public bool Equals(SourceLocation other) => Index == other.Index;
		public int CompareTo(SourceLocation other) => Index.CompareTo(other.Index);
		public override bool Equals(object obj) => obj is SourceLocation other && Equals(other);
		public override int GetHashCode() => Index;

		public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);
		public static bool operator !=(SourceLocation left, SourceLocation right) => !(left == right);
		public static bool operator <(SourceLocation left, SourceLocation right) => left.CompareTo(right) < 0;
		public static bool operator <=(SourceLocation left, SourceLocation right) => left.CompareTo(right) <= 0;
		public static bool operator >(SourceLocation left, SourceLocation right) => left.CompareTo(right) > 0;
		public static bool operator >=(SourceLocation left, SourceLocation right) => left.CompareTo(right) >= 0;

		public int Difference(SourceLocation reference) => Index - reference.Index;
	}

	public readonly struct SourceSpan : IEquatable<SourceSpan>
	{
		public SourceSpan(SourceLocation start, SourceLocation end)
		{
			Start = start;
			End = end;
		}

		public SourceLocation Start { get; }
		public SourceLocation End { get; }
		public int Length => End.Difference(Start);

		public bool Equals(SourceSpan other) => Start == other.Start && End == other.End;
		public override bool Equals(object obj) => obj is SourceSpan other && Equals(other);
		public override int GetHashCode() => Start.GetHashCode() ^ End.GetHashCode();
		public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);
		public static bool operator !=(SourceSpan left, SourceSpan right) => !(left == right);
	}

	public struct HighlightToken : IEquatable<HighlightToken>
	{
		public HighlightToken(string label, SourceSpan span)
		{
			Label = label;
			Span = span;
		}

		public string Label { get; }
		public SourceSpan Span { get; }

		public bool Equals(HighlightToken other) => Label == other.Label && Span == other.Span;
		public override bool Equals(object obj) => obj is HighlightToken other && Equals(other);
		public override int GetHashCode() => Label.GetHashCode() ^ Span.GetHashCode();
		public static bool operator ==(HighlightToken left, HighlightToken right) => left.Equals(right);
		public static bool operator !=(HighlightToken left, HighlightToken right) => !(left == right);
	}

	class SyllableStore
	{
		readonly List<SubSyllable[]> _syllables = new List<SubSyllable[]>();
		List<SubSyllable> _subSyllables;

		public void StartGrouping()
		{
			if (_subSyllables == null)
				_subSyllables = new List<SubSyllable>();
		}

		public void StopGrouping()
		{
			if (_subSyllables != null)
			{
				_syllables.Add(_subSyllables.ToArray());
				_subSyllables = null;
			}
		}

		public void Add(SubSyllable index)
		{
			if (_subSyllables != null)
				_subSyllables.Add(index);
			else
				_syllables.Add(new[] { index });
		}

		public bool HasAnySyllable => _syllables.Count > 0;

		public SubSyllable[][] ToArray() => _syllables.ToArray();
	}

	abstract class LyricsNode
	{
		protected LyricsNode(SourceSpan span) => Span = span;

		public abstract void Transform(StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore);

		public abstract IEnumerable<HighlightToken> Tokens { get; }

		public SourceSpan Span { get; }
	}

	class SimpleNode : LyricsNode
	{
		public SimpleNode(string text, CharacterState state, SourceSpan span) : base(span)
		{
			_state = state;
			_text = text;
		}

		readonly CharacterState _state;
		readonly string _text;

		public string Text => _state == CharacterState.Default ? _text : string.Empty;

		public override void Transform(StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore) => Transform(new SubSyllable(textBuilder.Length), textBuilder, syllableStore);

		public void Transform(SubSyllable subSyllable, StringBuilder textBuilder, SyllableStore syllableStore)
		{
			if (_state == CharacterState.StartGrouping)
				syllableStore?.StartGrouping();
			else if (_state == CharacterState.StopGrouping)
				syllableStore?.StopGrouping();
			else
			{
				if (syllableStore != null && !string.IsNullOrWhiteSpace(Text))
					syllableStore.Add(subSyllable);
				textBuilder.Append(Text);
			}
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				if (_state == CharacterState.StartGrouping || _state == CharacterState.StopGrouping)
					yield return new HighlightToken("SyllableGrouping", Span);
			}
		}

		public override string ToString() => _state == CharacterState.Default ? _text : "(" + _state.ToString() + ")";
	}

	class SilentNode : LyricsNode
	{
		public SilentNode(IEnumerable<LyricsNode> nodes, SourceSpan span) : base(span) => _nodes = nodes.ToArray();

		readonly LyricsNode[] _nodes;

		public override void Transform(StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore)
		{
			foreach (var node in _nodes)
				node.Transform(textBuilder, attachedSpecifiers, null);
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken("Silent", Span);
				foreach (var node in _nodes)
				{
					foreach (var token in node.Tokens)
						yield return token;
				}
			}
		}
	}

	class CompositeNode : LyricsNode
	{
		public CompositeNode(string text, SourceSpan baseSpan, IEnumerable<SimpleNode> ruby, SourceSpan span) : base(span)
		{
			_text = text;
			_baseSpan = baseSpan;
			_ruby = ruby.ToArray();
			_syllableDivision = _ruby.All(x => x.Text == "#" || string.IsNullOrEmpty(x.Text));
		}

		readonly string _text;
		readonly SourceSpan _baseSpan;
		readonly SimpleNode[] _ruby;
		readonly bool _syllableDivision;

		public override void Transform(StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore)
		{
			var rubyTextBuilder = new StringBuilder();
			foreach (var rubyNode in _ruby)
				rubyNode.Transform(new SubSyllable(attachedSpecifiers.Count, rubyTextBuilder.Length), rubyTextBuilder, syllableStore);
			if (_syllableDivision)
				attachedSpecifiers.Add(new SyllableDivisionSpecifier(new SharpDX.DirectWrite.TextRange(textBuilder.Length, _text.Length), rubyTextBuilder.Length));
			else
				attachedSpecifiers.Add(new RubySpecifier(new SharpDX.DirectWrite.TextRange(textBuilder.Length, _text.Length), rubyTextBuilder.ToString()));
			textBuilder.Append(_text);
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken("AttachedBase", _baseSpan);
				foreach (var ruby in _ruby)
				{
					var phtokens = ruby.Tokens.ToArray();
					if (phtokens.Length > 0)
					{
						foreach (var token in phtokens)
							yield return token;
					}
					else
						yield return new HighlightToken(_syllableDivision ? "SyllableDivision" : "Ruby", ruby.Span);
				}
			}
		}

		public override string ToString() => _text + "(" + string.Concat(_ruby.Select(x => x.Text)) + ")";
	}

	enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
