using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Controls;
using SharpDX.DirectWrite;

namespace Lyriser
{
	public class LyricsParser : IHighlightTokenizer
	{
		public LyricsParser(IErrorSink errorSink) => ErrorSink = errorSink;

		bool Accept(Scanner scanner, char ch)
		{
			if (Predict(scanner, ch))
			{
				scanner.Read();
				return true;
			}
			return false;
		}

		bool Predict(Scanner scanner, char ch) => scanner.Peek() == ch;

		public IErrorSink ErrorSink { get; }

		public IEnumerable<(AttachedLine Line, CharacterIndex[][] Keys)> Transform(string source)
		{
			using (var reader = new StringReader(source))
			{
				var lineIndex = 0;
				foreach (var nodes in Parse(reader))
				{
					var baseTextBuilder = new StringBuilder();
					var attachedSpecs = new List<AttachedSpecifier>();
					var keyStore = new KeyStore();
					foreach (var node in nodes)
						node.Transform(lineIndex, baseTextBuilder, attachedSpecs, keyStore);
					yield return (new AttachedLine(baseTextBuilder.ToString(), attachedSpecs), keyStore.ToArray());
					lineIndex++;
				}
			}
		}

		IEnumerable<LyricsNode[]> Parse(TextReader reader)
		{
			ErrorSink.Clear();
			var scanner = new Scanner(reader);
			while (scanner.MoveNextLine())
				yield return ParseLyricsLine(scanner);
		}

		LyricsNode[] ParseLyricsLine(Scanner scanner)
		{
			var nodes = new List<LyricsNode>();
			while (scanner.Peek() != null)
				nodes.Add(ParseLyricsNode(scanner));
			return nodes.ToArray();
		}

		LyricsNode ParseLyricsNode(Scanner scanner)
		{
			var start = scanner.SerialIndex;
			if (Accept(scanner, '('))
			{
				var nodes = new List<LyricsNode>();
				while (!Accept(scanner, ')'))
				{
					if (scanner.Peek() == null)
					{
						ErrorSink.ReportError("非発音領域が適切に終了されていません。", scanner.SerialIndex);
						break;
					}
					nodes.Add(ParseLyricsNode(scanner));
				}
				return new SkippedNode(nodes, start, scanner.SerialIndex - start);
			}
			if (Accept(scanner, '|'))
			{
				var mainNodes = new List<SimpleNode>();
				var rubyBaseStartIndex = scanner.SerialIndex;
				var rubyNotFound = false;
				while (!Accept(scanner, '"'))
				{
					if (scanner.Peek() == null)
					{
						rubyNotFound = true;
						break;
					}
					mainNodes.Add(ParseSimpleNode(scanner));
				}
				if (mainNodes.Count <= 0)
				{
					ErrorSink.ReportError("ルビ領域でルビを振る対象を省略することはできません。", rubyBaseStartIndex);
					mainNodes.Add(new SimpleNode("_", CharacterState.Default, rubyBaseStartIndex, 0));
				}
				if (rubyNotFound)
				{
					ErrorSink.ReportError("ルビ領域でのルビの開始位置が見つかりません。", scanner.SerialIndex);
					return new CompositeNode(mainNodes, new[] { new SimpleNode("_", CharacterState.Default, scanner.SerialIndex, 0) }, start, scanner.SerialIndex - start);
				}
				var rubyNodes = new List<SimpleNode>();
				var rubyStartIndex = scanner.SerialIndex;
				while (!Accept(scanner, '"'))
				{
					if (scanner.Peek() == null)
					{
						ErrorSink.ReportError("ルビが適切に終了されていません。", scanner.SerialIndex);
						break;
					}
					rubyNodes.Add(ParseSimpleNode(scanner));
				}
				ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(rubyNodes, rubyStartIndex);
				return new CompositeNode(mainNodes, rubyNodes, start, scanner.SerialIndex - start);
			}
			var node = ParseSimpleNode(scanner);
			if (Accept(scanner, '"'))
			{
				var rubyStartIndex = scanner.SerialIndex;
				var rubyNodes = new List<SimpleNode>();
				while (!Accept(scanner, '"'))
				{
					if (scanner.Peek() == null)
					{
						ErrorSink.ReportError("ルビが適切に終了されていません。", scanner.SerialIndex);
						break;
					}
					rubyNodes.Add(ParseSimpleNode(scanner));
				}
				ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(rubyNodes, rubyStartIndex);
				return new CompositeNode(new[] { node }, rubyNodes, start, scanner.SerialIndex - start);
			}
			return node;
		}

		void ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(List<SimpleNode> rubyNodes, int rubyStartIndex)
		{
			if (rubyNodes.Count <= 0)
			{
				ErrorSink.ReportError("ルビを省略することはできません。", rubyStartIndex);
				rubyNodes.Add(new SimpleNode("_", CharacterState.Default, rubyStartIndex, 0));
			}
			else if (rubyNodes.All(x => string.IsNullOrWhiteSpace(x.Text)))
			{
				ErrorSink.ReportError("ルビを空白文字のみとすることはできません。", rubyStartIndex);
				rubyNodes.Add(new SimpleNode("_", CharacterState.Default, rubyNodes[rubyNodes.Count - 1].StartIndex + rubyNodes[rubyNodes.Count - 1].Length, 0));
			}
		}

		SimpleNode ParseSimpleNode(Scanner scanner)
		{
			var start = scanner.SerialIndex;
			var escaping = false;
			if (Accept(scanner, '`'))
				escaping = true;
			var sb = new StringBuilder();
			if (scanner.Peek() != null)
				sb.Append(scanner.Read());
			else
			{
				ErrorSink.ReportError("何らかの文字が必要です。", scanner.SerialIndex);
				sb.Append('_');
			}
			if (char.IsHighSurrogate(sb[sb.Length - 1]) && scanner.Peek() != null && char.IsLowSurrogate((char)scanner.Peek()))
				sb.Append(scanner.Read());
			var text = sb.ToString();
			var state = CharacterState.Default;
			if (!escaping)
			{
				if (text == "{")
					state = CharacterState.StartGrouping;
				else if (text == "}")
					state = CharacterState.StopGrouping;
			}
			return new SimpleNode(text, state, start, scanner.SerialIndex - start);
		}

		[CLSCompliant(false)]
		public IEnumerable<HighlightToken> GetTokens(string text)
		{
			using (var reader = new StringReader(text))
				return Parse(reader).SelectMany(x => x.SelectMany(y => y.Tokens)).ToArray();
		}
	}

	class Scanner
	{
		public Scanner(TextReader reader) => _reader = reader;

		TextReader _reader;
		string _line;
		int _baseIndex = 0;
		int _lineIndex;
		int _lineLengthIncludingNewLineChars = 0;

		public int SerialIndex => _baseIndex + _lineIndex;

		public bool MoveNextLine()
		{
			_baseIndex += _lineLengthIncludingNewLineChars;
			_line = ReadLineWithLength(out _lineLengthIncludingNewLineChars);
			_lineIndex = 0;
			return _line != null;
		}

		public char? Peek() => _line == null || _lineIndex >= _line.Length ? default(char?) : _line[_lineIndex];

		public char Read()
		{
			if (_line == null)
				throw new InvalidOperationException("Already reached end of file.");
			if (_lineIndex >= _line.Length)
				throw new InvalidOperationException("Already reached end of line.");
			return _line[_lineIndex++];
		}

		string ReadLineWithLength(out int length)
		{
			var sb = new StringBuilder();
			length = 0;
			int ch;
			while (true)
			{
				ch = _reader.Read();
				if (ch == -1)
				{
					if (sb.Length > 0)
						return sb.ToString();
					return null;
				}
				length++;
				if (ch == 13 && _reader.Peek() == 10)
				{
					_reader.Read();
					length++;
				}
				if (ch == 13 || ch == 10)
					return sb.ToString();
				sb.Append((char)ch);
			}
		}
	}

	class KeyStore
	{
		readonly List<CharacterIndex[]> _keys = new List<CharacterIndex[]>();
		List<CharacterIndex> _subKeys;

		public void StartGrouping()
		{
			if (_subKeys == null)
				_subKeys = new List<CharacterIndex>();
		}

		public void StopGrouping()
		{
			if (_subKeys != null)
			{
				_keys.Add(_subKeys.ToArray());
				_subKeys = null;
			}
		}

		public void Add(CharacterIndex index)
		{
			if (_subKeys != null)
				_subKeys.Add(index);
			else
				_keys.Add(new[] { index });
		}

		public CharacterIndex[][] ToArray() => _keys.ToArray();
	}

	abstract class LyricsNode
	{
		protected LyricsNode(int start, int length)
		{
			StartIndex = start;
			Length = length;
		}

		public abstract void Transform(int lineIndex, StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, KeyStore keyStore);

		public abstract IEnumerable<HighlightToken> Tokens { get; }

		public int StartIndex { get; }

		public int Length { get; }
	}

	class SimpleNode : LyricsNode
	{
		public SimpleNode(string text, CharacterState state, int start, int length) : base(start, length)
		{
			_state = state;
			_text = text;
		}

		readonly CharacterState _state;
		readonly string _text;

		public string Text => _state == CharacterState.Default ? _text : string.Empty;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, KeyStore keyStore) => Transform(lineIndex, textBuilder, -1, keyStore);

		public void Transform(int lineIndex, StringBuilder textBuilder, int attachedIndex, KeyStore keyStore)
		{
			if (_state == CharacterState.StartGrouping)
				keyStore?.StartGrouping();
			else if (_state == CharacterState.StopGrouping)
				keyStore?.StopGrouping();
			else
			{
				if (keyStore != null && !string.IsNullOrWhiteSpace(Text))
					keyStore.Add(new CharacterIndex(lineIndex, attachedIndex, textBuilder.Length));
				textBuilder.Append(Text);
			}
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				if (_state == CharacterState.StartGrouping || _state == CharacterState.StopGrouping)
					yield return new HighlightToken(StartIndex, Length, Color.Red, Color.Empty);
			}
		}

		public override string ToString() => _state == CharacterState.Default ? _text : "(" + _state.ToString() + ")";
	}

	class SkippedNode : LyricsNode
	{
		public SkippedNode(IEnumerable<LyricsNode> nodes, int start, int length) : base(start, length) => _nodes = nodes.ToArray();

		readonly LyricsNode[] _nodes;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, KeyStore keyStore)
		{
			foreach (var node in _nodes)
				node.Transform(lineIndex, textBuilder, attachedSpecifiers, null);
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				var first = StartIndex;
				foreach (var node in _nodes)
				{
					foreach (var token in node.Tokens)
					{
						if (first < token.First)
							yield return new HighlightToken(first, token.First - first, Color.Empty, Color.Gainsboro);
						yield return new HighlightToken(token.First, token.Length, token.ForeColor, System.Windows.Media.Colors.Gainsboro);
						first = token.First + token.Length;
					}
				}
				var last = StartIndex + Length;
				if (first < last)
					yield return new HighlightToken(first, last - first, Color.Empty, Color.Gainsboro);
			}
		}
	}

	class CompositeNode : LyricsNode
	{
		public CompositeNode(IEnumerable<SimpleNode> rawText, IEnumerable<SimpleNode> ruby, int start, int length) : base(start, length)
		{
			_text = string.Concat(rawText.Select(x => x.Text));
			RawTextStartIndex = rawText.First().StartIndex;
			_rawTextLength = rawText.Sum(x => x.Length);
			_ruby = ruby.ToArray();
			_syllableDivision = _ruby.All(x => x.Text == "#" || x.Text == string.Empty);
		}

		readonly string _text;
		readonly int _rawTextLength;
		readonly SimpleNode[] _ruby;
		readonly bool _syllableDivision;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<AttachedSpecifier> attachedSpecifiers, KeyStore keyStore)
		{
			var rubyTextBuilder = new StringBuilder();
			foreach (var rubyNode in _ruby)
				rubyNode.Transform(lineIndex, rubyTextBuilder, attachedSpecifiers.Count, keyStore);
			if (_syllableDivision)
				attachedSpecifiers.Add(new SyllableDivisionSpecifier(new TextRange(textBuilder.Length, _text.Length), rubyTextBuilder.Length));
			else
				attachedSpecifiers.Add(new RubySpecifier(new TextRange(textBuilder.Length, _text.Length), rubyTextBuilder.ToString()));
			textBuilder.Append(_text);
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken(RawTextStartIndex, _rawTextLength, Color.Red, Color.Empty);
				foreach (var ruby in _ruby)
				{
					var phtokens = ruby.Tokens.ToArray();
					if (phtokens.Length > 0)
					{
						foreach (var token in phtokens)
							yield return token;
					}
					else
						yield return new HighlightToken(ruby.StartIndex, ruby.Length, _syllableDivision ? Color.Purple: Color.Blue, Color.Empty);
				}
			}
		}

		public int RawTextStartIndex { get; }

		public override string ToString() => _text + "(" + string.Concat(_ruby.Select(x => x.Text)) + ")";
	}

	enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
