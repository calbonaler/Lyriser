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

		internal const char RubyBaseStartChar = '|';
		internal const char RubyStartChar = '(';
		internal const char RubyEndChar = ')';
		internal const char SilentStartChar = '[';
		internal const char SilentEndChar = ']';
		internal const char EscapeChar = '`';

		bool Accept(Scanner scanner, char ch)
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
			var (silentSubNodes, silentSpan) = ParseSilentNode(scanner, ParseLyricsNode);
			if (silentSubNodes != null)
				return new SilentNode(silentSubNodes, silentSpan);
			var start = scanner.Location;
			if (Accept(scanner, RubyBaseStartChar))
			{
				var rubyBase = new List<SimpleNode>();
				while (scanner.Peek() != null && scanner.Peek() != RubyStartChar)
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
					: (LyricsNode)simpleNode;
			}
		}

		List<IPhoneticNode> ParseRubyNodes(Scanner scanner)
		{
			var rubyNodes = new List<IPhoneticNode>();
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
					var (silentSubNodes, silentSpan) = ParseSilentNode(scanner, ParseSimpleNode);
					if (silentSubNodes != null)
						rubyNodes.Add(new RubySilentNode(silentSubNodes, silentSpan));
					else
						rubyNodes.Add(ParseSimpleNode(scanner));
				}
				if (rubyNodes.Count <= 0)
				{
					ErrorReporter?.Invoke(ErrorRubyRequired(rubyStart));
					rubyNodes.Add(new SimpleNode("_", false, new SourceSpan(rubyStart, rubyStart)));
				}
				else if (rubyNodes.All(x => string.IsNullOrWhiteSpace(x.PhoneticText)))
				{
					ErrorReporter?.Invoke(ErrorRubyMustNotBeOnlyWhitespaces(rubyStart));
					rubyNodes.Add(new SimpleNode("_", false, new SourceSpan(rubyNodes[rubyNodes.Count - 1].Span.End, rubyNodes[rubyNodes.Count - 1].Span.End)));
				}
			}
			return rubyNodes;
		}

		(List<T> Nodes, SourceSpan Span) ParseSilentNode<T>(Scanner scanner, Func<Scanner, T> subParser)
		{
			var start = scanner.Location;
			if (!Accept(scanner, SilentStartChar))
				return (null, default);
			var nodes = new List<T>();
			while (!Accept(scanner, SilentEndChar))
			{
				if (scanner.Peek() == null)
				{
					ErrorReporter?.Invoke(ErrorSilentImproperlyEnded(scanner.Location));
					break;
				}
				nodes.Add(subParser(scanner));
			}
			return (nodes, new SourceSpan(start, scanner.Location));
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
			if (char.IsHighSurrogate(sb[sb.Length - 1]) && scanner.Peek() != null && char.IsLowSurrogate((char)scanner.Peek()))
				sb.Append(scanner.Read());
			return new SimpleNode(sb.ToString(), escaping, new SourceSpan(start, scanner.Location));
		}

		public IEnumerable<(LyricsNode[] Line, string Terminator)> Parse(string source) => Parse(new Scanner(source));

		public IEnumerable<LyricsNode> ParseLine(string line) => ParseLyricsLine(new Scanner(line));

		public static bool IsEscapeRequired(string text)
		{
			if (text.Length == 0)
				return false;
			var ch = char.ConvertToUtf32(text, 0);
			return ch == RubyBaseStartChar ||
				ch == RubyStartChar ||
				ch == RubyEndChar ||
				ch == SilentStartChar ||
				ch == SilentEndChar ||
				ch == EscapeChar;
		}
	}

	class Scanner
	{
		public Scanner(string text) => _text = text;

		readonly string _text;
		int _lineIndex = 0;
		int _lineStartIndex = 0;
		int _textIndex = 0;

		public SourceLocation Location => new SourceLocation(_textIndex, _lineIndex + 1, _textIndex - _lineStartIndex + 1);

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

	public class SyllableStore
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

	public interface ILyricsNodeBase
	{
		string GenerateSource();

		IEnumerable<HighlightToken> Tokens { get; }

		SourceSpan Span { get; }
	}

	public abstract class LyricsNodeBase : ILyricsNodeBase
	{
		protected LyricsNodeBase(SourceSpan span) => Span = span;

		public abstract IEnumerable<HighlightToken> Tokens { get; }

		public SourceSpan Span { get; }

		public abstract string GenerateSource();

		public static string GenerateSource(IEnumerable<ILyricsNodeBase> nodes) => string.Concat(nodes.Select(x => x.GenerateSource()));
	}

	public abstract class LyricsNode : LyricsNodeBase
	{
		protected LyricsNode(SourceSpan span) : base(span) { }

		public abstract void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore);
	}

	public interface IPhoneticNode : ILyricsNodeBase
	{
		void Transform(int attached, StringBuilder textBuilder, SyllableStore syllableStore);

		string PhoneticText { get; }
	}

	public class SimpleNode : LyricsNode, IPhoneticNode
	{
		public SimpleNode(string text, bool escaped, SourceSpan span) : base(span)
		{
			Text = text;
			IsEscaped = escaped;
		}

		public SimpleNode(string text, SourceSpan span) : this(text, LyricsParser.IsEscapeRequired(text) || text == StartGroupingChar || text == StopGroupingChar, span) { }

		const string StartGroupingChar = "{";
		const string StopGroupingChar = "}";

		public string Text { get; }

		public bool IsEscaped { get; }

		public (string, CharacterState) PhoneticTextAndState =>
			!IsEscaped && Text == StartGroupingChar ? (string.Empty, CharacterState.StartGrouping) :
			!IsEscaped && Text == StopGroupingChar  ? (string.Empty, CharacterState.StopGrouping ) :
			                                          (Text,         CharacterState.Default      );

		public string PhoneticText => PhoneticTextAndState.Item1;

		public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore) => Transform(-1, textBuilder, syllableStore);

		public void Transform(int attached, StringBuilder textBuilder, SyllableStore syllableStore)
		{
			var (phoneticText, state) = PhoneticTextAndState;
			if (state == CharacterState.StartGrouping)
				syllableStore?.StartGrouping();
			else if (state == CharacterState.StopGrouping)
				syllableStore?.StopGrouping();
			else
			{
				if (syllableStore != null && !string.IsNullOrWhiteSpace(phoneticText))
					syllableStore.Add(new SubSyllable(attached, textBuilder.Length));
				textBuilder.Append(phoneticText);
			}
		}

		public override string GenerateSource() => IsEscaped ? $"{LyricsParser.EscapeChar}{Text}" : Text;

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				var (_, state) = PhoneticTextAndState;
				if (state == CharacterState.StartGrouping || state == CharacterState.StopGrouping)
					yield return new HighlightToken("SyllableGrouping", Span);
			}
		}
	}

	public class SilentNode : LyricsNode
	{
		public SilentNode(IEnumerable<LyricsNode> nodes, SourceSpan span) : base(span) => Nodes = nodes.ToArray();

		public IReadOnlyList<LyricsNode> Nodes { get; }

		public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore)
		{
			foreach (var node in Nodes)
				node.Transform(textBuilder, attachedSpecifiers, null);
		}

		public override string GenerateSource() => GenerateSource(Nodes);

		public override IEnumerable<HighlightToken> Tokens => GetTokens(Span, Nodes);

		internal static IEnumerable<HighlightToken> GetTokens(SourceSpan span, IEnumerable<LyricsNode> nodes)
		{
			yield return new HighlightToken("Silent", span);
			foreach (var node in nodes)
			{
				foreach (var token in node.Tokens)
					yield return token;
			}
		}

		internal static string GenerateSource(IEnumerable<LyricsNode> nodes) => $"{LyricsParser.SilentStartChar}{LyricsNodeBase.GenerateSource(nodes)}{LyricsParser.SilentEndChar}";
	}

	public class RubySilentNode : LyricsNodeBase, IPhoneticNode
	{
		public RubySilentNode(IEnumerable<SimpleNode> nodes, SourceSpan span) : base(span) => Nodes = nodes.ToArray();

		public IReadOnlyList<SimpleNode> Nodes { get; }

		public string PhoneticText => string.Concat(Nodes.Select(x => x.PhoneticText));

		public void Transform(int attached, StringBuilder textBuilder, SyllableStore syllableStore)
		{
			foreach (var node in Nodes)
				node.Transform(attached, textBuilder, null);
		}

		public override string GenerateSource() => SilentNode.GenerateSource(Nodes);

		public override IEnumerable<HighlightToken> Tokens => SilentNode.GetTokens(Span, Nodes);
	}

	public class CompositeNode : LyricsNode
	{
		public CompositeNode(IEnumerable<SimpleNode> text, IEnumerable<IPhoneticNode> ruby, bool complex, SourceSpan span) : base(span)
		{
			Text = text.ToArray();
			if (Text.Count <= 0)
				throw new ArgumentException($"Must contains at least one item", nameof(text));
			Ruby = ruby.ToArray();
			IsComplex = complex;
		}

		public CompositeNode(IEnumerable<SimpleNode> text, IEnumerable<IPhoneticNode> ruby, SourceSpan span) : this(text, ruby, false, span) => IsComplex = Text.Count > 1;

		public IReadOnlyList<SimpleNode> Text { get; }

		public IReadOnlyList<IPhoneticNode> Ruby { get; }

		public bool IsComplex { get; }

		public override void Transform(StringBuilder textBuilder, ICollection<AttachedSpecifier> attachedSpecifiers, SyllableStore syllableStore)
		{
			var rubyTextBuilder = new StringBuilder();
			var syllableDivision = true;
			foreach (var rubyNode in Ruby)
			{
				if (!IsSyllableDivisionComponent(rubyNode))
					syllableDivision = false;
				rubyNode.Transform(attachedSpecifiers.Count, rubyTextBuilder, syllableStore);
			}
			var text = string.Concat(Text.Select(x => x.Text));
			if (syllableDivision)
				attachedSpecifiers.Add(new SyllableDivisionSpecifier(Core.DirectWrite.TextRange.FromStartLength(textBuilder.Length, text.Length), rubyTextBuilder.Length));
			else
				attachedSpecifiers.Add(new RubySpecifier(Core.DirectWrite.TextRange.FromStartLength(textBuilder.Length, text.Length), rubyTextBuilder.ToString()));
			textBuilder.Append(text);
		}

		public override string GenerateSource() =>
			IsComplex ? $"{LyricsParser.RubyBaseStartChar}{GenerateSource(Text)}{LyricsParser.RubyStartChar}{GenerateSource(Ruby)}{LyricsParser.RubyEndChar}" :
						$"{GenerateSource(Text)}{LyricsParser.RubyStartChar}{GenerateSource(Ruby)}{LyricsParser.RubyEndChar}";

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				var syllableDivision = Ruby.All(IsSyllableDivisionComponent);
				yield return new HighlightToken("AttachedBase", new SourceSpan(Text[0].Span.Start, Text[Text.Count - 1].Span.End));
				foreach (var ruby in Ruby)
				{
					yield return new HighlightToken(syllableDivision ? "SyllableDivision" : "Ruby", ruby.Span);
					foreach (var token in ruby.Tokens)
						yield return token;
				}
			}
		}

		static bool IsSyllableDivisionComponent(IPhoneticNode node)
		{
			var phoneticText = node.PhoneticText;
			return phoneticText == "#" || string.IsNullOrEmpty(phoneticText);
		}
	}

	public enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
