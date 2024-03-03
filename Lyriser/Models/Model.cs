using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Utils;

namespace Lyriser.Models;

public partial class Model : INotifyPropertyChanged
{
	public Model(IMonoRubyProvider monoRubyProvider)
	{
		_monoRubyProvider = monoRubyProvider;
		_parser.ErrorReporter = _backingParserErrors.Add;
		_originalVersion = SourceDocument.Version;
		Observable.FromEventPattern(x => SourceDocument.TextChanged += x, x => SourceDocument.TextChanged -= x)
			.Do(_ => PropertyChanged.Raise(this, nameof(IsModified)))
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Subscribe(_ =>
			{
				_backingParserErrors.Clear();
				LyricsSource = new LyricsSource(_parser.Parse(SourceDocument.CreateSnapshot().Text).Select(x => x.Line));
				ParserErrors = [.. _backingParserErrors];
			});
	}

	readonly IMonoRubyProvider _monoRubyProvider;

	readonly LyricsParser _parser = new();
	ITextSourceVersion _originalVersion;
	ITextSourceVersion OriginalVersion
	{
		get => _originalVersion;
		set
		{
			if (_originalVersion != value)
			{
				_originalVersion = value;
				PropertyChanged?.Raise(this, nameof(IsModified));
			}
		}
	}

	public bool IsModified => SourceDocument.Version != OriginalVersion;

	string? _savedFilePath;
	Encoding? _savedEncoding;
	public string? SavedFileNameWithoutExtension => Path.GetFileNameWithoutExtension(_savedFilePath);

	public TextDocument SourceDocument { get; } = new();

	LyricsSource _lyricsSource = LyricsSource.Empty;
	public LyricsSource LyricsSource
	{
		get => _lyricsSource;
		set => Utils.SetProperty(ref _lyricsSource, value, PropertyChanged, this);
	}

	readonly List<ParserError> _backingParserErrors = [];
	IReadOnlyList<ParserError> _parserErrors = [];
	public IReadOnlyList<ParserError> ParserErrors
	{
		get => _parserErrors;
		private set => Utils.SetProperty(ref _parserErrors, value, PropertyChanged, this);
	}

	public void New()
	{
		SourceDocument.Remove(0, SourceDocument.TextLength);
		SourceDocument.UndoStack.ClearAll();
		_savedFilePath = null;
		_savedEncoding = null;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
	}

	public void Open(EncodedFileInfo info)
	{
		if (info.Encoding == FileEncoding.AutoDetect)
		{
			using var fs = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = FileReader.OpenStream(fs, AnsiEncoding);
			SourceDocument.Text = reader.ReadToEnd();
			_savedEncoding = reader.CurrentEncoding;
		}
		else
		{
			var encoding = ToEncoding(info.Encoding);
			SourceDocument.Text = File.ReadAllText(info.Path, encoding);
			_savedEncoding = encoding;
		}
		SourceDocument.UndoStack.ClearAll();
		_savedFilePath = info.Path;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
	}

	public void Save(EncodedFileInfo info)
	{
		if (info.Encoding == FileEncoding.AutoDetect)
			throw new ArgumentException($"{nameof(FileEncoding.AutoDetect)} cannot be used");
		Save(info.Path, ToEncoding(info.Encoding));
	}

	public void Save()
	{
		if (_savedFilePath == null || _savedEncoding == null)
			throw new InvalidOperationException("Current document is not saved. Use Save(EncodedFileInfo) overload.");
		Save(_savedFilePath, _savedEncoding);
	}

	public void Save(string path, Encoding encoding)
	{
		using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
		using (var writer = new StreamWriter(fs, encoding))
			SourceDocument.WriteTextTo(writer);
		_savedFilePath = path;
		_savedEncoding = encoding;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
	}

	static Encoding ToEncoding(FileEncoding fileEncoding) => fileEncoding switch
	{
		FileEncoding.UTF8WithBom => new UTF8Encoding(true),
		FileEncoding.Ansi => AnsiEncoding,
		_ => new UTF8Encoding(false),
	};

	static Encoding AnsiEncoding
	{
		get
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
		}
	}

	public void AutoSetRuby(ISegment segment)
	{
		// 非発音領域などのルビ以外の構造を保持したままルビの再設定を行う
		var parsingErrorOccurred = false;
		var parser = new LyricsParser { ErrorReporter = _ => parsingErrorOccurred = true };
		var lyricsLines = parser.Parse(SourceDocument.GetText(segment)).ToArray();
		if (parsingErrorOccurred)
			return;
		var newSourceBuilder = new StringBuilder();
		foreach (var (lyricsLine, lineTerminator) in lyricsLines)
		{
			var (baseText, textToNodeMap) = CollectBaseText(lyricsLine);
			var monoRuby = _monoRubyProvider.GetMonoRuby(baseText);
			// GetMonoRubyでモノルビ分割がされない場合の送り仮名向け特殊処理
			var baseTextElements = GetTextElements(baseText);
			var rubyTextElements = GetTextElements(monoRuby.Text);
			for (int i = baseTextElements.Length - 1, j = rubyTextElements.Length - 1; i >= 0 && j >= 0; )
			{
				if (baseTextElements[i].Text != rubyTextElements[j].Text)
				{
					// ルビとベースで対応する文字が異なる場合はモノルビの対応関係が設定されている箇所まで進める
					while ((j = monoRuby.Indexes[baseTextElements[i].Index]) == Core.Ime.MonoRuby.UnmatchedPosition)
						i--;
				}
				// ルビとベースで対応する文字が同じ場合はモノルビの対応関係を設定する
				monoRuby.Indexes[baseTextElements[i].Index] = (ushort)rubyTextElements[j].Index;
				i--;
				j--;
			}
			var rubySpecifiers = new Queue<RubySpecifier>();
			var rubyBaseStart = 0;
			var rubyStart = monoRuby.Indexes[0];
			for (var i = 1; i <= baseText.Length; i++)
			{
				if (monoRuby.Indexes[i] == Core.Ime.MonoRuby.UnmatchedPosition)
					continue;
				// 既存ルビの作成
				var rubyBase = baseText[rubyBaseStart..i];
				var ruby = monoRuby.Text[rubyStart..monoRuby.Indexes[i]];
				// 下記のすべてに該当する場合のみルビを作成する
				// ・ルビのベースはカテゴリLoのCode Pointまたは文字「々」のみから生成される
				// ・ルビのベースにはひらがな、カタカナ、半角・全角形類を含まない
				if (RubyBaseRegex().IsMatch(rubyBase))
				{
					var nodes = new List<SimpleNode>();
					for (var j = rubyBaseStart; j < i;)
					{
						var node = textToNodeMap[j];
						nodes.Add(node);
						j += node.Text.Length;
					}
					rubySpecifiers.Enqueue(new([.. nodes], ruby));
				}
				// 新規ルビの記録開始
				rubyBaseStart = i;
				rubyStart = monoRuby.Indexes[i];
			}
			newSourceBuilder.Append(LyricsNode.GenerateSource(SetRuby(lyricsLine, rubySpecifiers)));
			newSourceBuilder.Append(lineTerminator);
			Debug.Assert(rubySpecifiers.Count == 0);
		}
		SourceDocument.Replace(segment, newSourceBuilder.ToString());
	}

	[GeneratedRegex("^(?=[\\p{Lo}々]+$)(?![\\p{IsHiragana}\\p{IsKatakana}\\p{IsKatakanaPhoneticExtensions}\\p{IsHalfwidthandFullwidthForms}]+$)")]
	private static partial Regex RubyBaseRegex();

	static TextElement[] GetTextElements(string text)
	{
		var enumerator = StringInfo.GetTextElementEnumerator(text);
		var indexes = new List<TextElement>();
		var oldIndex = -1;
		while (true)
		{
			var result = enumerator.MoveNext();
			if (oldIndex >= 0)
			{
				var length = (result ? enumerator.ElementIndex : text.Length) - oldIndex;
				indexes.Add(new TextElement(text, oldIndex, length));
			}
			if (!result) return [.. indexes];
			oldIndex = enumerator.ElementIndex;
		}
	}

	static (string BaseText, Dictionary<int, SimpleNode> TextToNodeMap) CollectBaseText(IEnumerable<LyricsNode> nodes)
	{
		var sb = new StringBuilder();
		var textToNodeMap = new Dictionary<int, SimpleNode>();
		foreach (var node in nodes)
		{
			if (node is SimpleNode simpleNode)
			{
				textToNodeMap[sb.Length] = simpleNode;
				sb.Append(simpleNode.Text);
			}
			else if (node is CompositeNode compositeNode)
			{
				foreach (var subNode in compositeNode.Base)
				{
					textToNodeMap[sb.Length] = subNode;
					sb.Append(subNode.Text);
				}
			}
			else
			{
				var (baseText, innerTextToNodeMap) = CollectBaseText(((SilentNode)node).Nodes);
				foreach (var kvp in innerTextToNodeMap)
					textToNodeMap.Add(sb.Length + kvp.Key, kvp.Value);
				sb.Append(baseText);
			}
		}
		return (sb.ToString(), textToNodeMap);
	}

	static LyricsNode[] SetRuby(IEnumerable<LyricsNode> nodes, Queue<RubySpecifier> rubySpecifiers)
	{
		var newNodes = new List<LyricsNode>();

		void ProcessSimpleNode(SimpleNode simpleNode)
		{
			var nodeIndex = rubySpecifiers.Count <= 0 ? -1 : Array.IndexOf(rubySpecifiers.Peek().Base, simpleNode);
			if (nodeIndex < 0)
			{
				newNodes.Add(simpleNode);
				return;
			}
			if (nodeIndex == rubySpecifiers.Peek().Base.Length - 1)
			{
				var rubySpec = rubySpecifiers.Dequeue();
				var rubyNodes = new List<SimpleNode>();
				for (var i = 0; i < rubySpec.Ruby.Length;)
				{
					var textLength = i + 1 < rubySpec.Ruby.Length && char.IsSurrogatePair(rubySpec.Ruby, i) ? 2 : 1;
					rubyNodes.Add(new SimpleNode(rubySpec.Ruby.Substring(i, textLength), default));
					i += textLength;
				}
				newNodes.Add(new CompositeNode(rubySpec.Base, rubyNodes, default));
			}
		}

		foreach (var node in nodes)
		{
			if (node is SimpleNode simpleNode)
				ProcessSimpleNode(simpleNode);
			else if (node is CompositeNode compositeNode)
			{
				foreach (var subNode in compositeNode.Base)
					ProcessSimpleNode(subNode);
			}
			else
				newNodes.Add(new SilentNode(SetRuby(((SilentNode)node).Nodes, rubySpecifiers), default));
		}

		return [.. newNodes];
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	readonly record struct TextElement(string WholeText, int Index, int Length)
	{
		public string Text => WholeText.Substring(Index, Length);
	}

	readonly record struct RubySpecifier(SimpleNode[] Base, string Ruby);
}

public class EncodedFileInfo(string path, FileEncoding encoding)
{
	public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
	public FileEncoding Encoding { get; } = encoding;
}

public enum FileEncoding
{
	AutoDetect,
	UTF8,
	UTF8WithBom,
	Ansi,
}
