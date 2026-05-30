using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
		OriginalVersion = SourceDocument.Version;
		SourceDocument.TextChanged += (s, ev) =>
		{
			PropertyChanged.Raise(this, nameof(IsModified));
			_backingParserErrors.Clear();
			LyricsSource = new(_parser.Parse(SourceDocument.CreateSnapshot().Text).Select(x => x.Line));
			ParserErrors = [.. _backingParserErrors];
		};
	}

	readonly IMonoRubyProvider _monoRubyProvider;
	readonly LyricsParser _parser = new();

	ITextSourceVersion OriginalVersion { get; set => PropertyChangedUtils.Set(ref field, value, PropertyChanged, this, nameof(IsModified)); }
	public bool IsModified => SourceDocument.Version != OriginalVersion;

	string? _savedFilePath;
	public string? SavedFileNameWithoutExtension => Path.GetFileNameWithoutExtension(_savedFilePath);
	public FileEncoding CurrentEncoding { get; set => PropertyChangedUtils.Set(ref field, value, PropertyChanged, this); } = FileEncoding.Utf8;

	public TextDocument SourceDocument { get; } = new();
	public LyricsSource LyricsSource { get; set => PropertyChangedUtils.Set(ref field, value, PropertyChanged, this); } = LyricsSource.Empty;

	readonly List<ParserError> _backingParserErrors = [];
	public IReadOnlyList<ParserError> ParserErrors { get; private set => PropertyChangedUtils.Set(ref field, value, PropertyChanged, this); } = [];

	public void New()
	{
		SourceDocument.Remove(0, SourceDocument.TextLength);
		SourceDocument.UndoStack.ClearAll();
		_savedFilePath = null;
		CurrentEncoding = FileEncoding.Utf8;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
	}

	public void Open(string filePath) => Open(filePath, null);

	public void Reopen(FileEncoding encoding)
	{
		if (_savedFilePath == null)
			throw new InvalidOperationException("Current document is not saved.");
		Open(_savedFilePath, encoding);
	}

	void Open(string filePath, FileEncoding? encoding)
	{
		if (encoding is null)
		{
			using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = FileReader.OpenStream(fs, FileEncoding.Ansi.ToEncoding());
			SourceDocument.Text = reader.ReadToEnd();
			CurrentEncoding = FileEncoding.All.First(x => x.Creates(reader.CurrentEncoding));
		}
		else
		{
			SourceDocument.Text = File.ReadAllText(filePath, encoding.ToEncoding());
			CurrentEncoding = encoding;
		}
		SourceDocument.UndoStack.ClearAll();
		_savedFilePath = filePath;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
	}

	public void Save(string filePath) => Save(filePath, FileEncoding.Utf8);

	public void Save()
	{
		if (_savedFilePath == null)
			throw new InvalidOperationException("Current document is not saved. Use Save(string) overload.");
		Save(_savedFilePath, CurrentEncoding);
	}

	public void Save(string path, FileEncoding encoding)
	{
		using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
		using (var writer = new StreamWriter(fs, encoding.ToEncoding()))
			SourceDocument.WriteTextTo(writer);
		_savedFilePath = path;
		CurrentEncoding = encoding;
		PropertyChanged.Raise(this, nameof(SavedFileNameWithoutExtension));
		OriginalVersion = SourceDocument.Version;
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
			for (int i = baseTextElements.Length - 1, j = rubyTextElements.Length - 1; i >= 0 && j >= 0;)
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
				if (RubyBaseRegex.IsMatch(rubyBase))
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
	private static partial Regex RubyBaseRegex { get; }

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
				indexes.Add(new(text, oldIndex, length));
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
					rubyNodes.Add(new(rubySpec.Ruby.Substring(i, textLength), default));
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

public class FileEncoding
{
	FileEncoding(string name, int codePage, bool hasBom)
	{
		Name = name;
		CodePage = codePage;
		HasBom = hasBom;
	}

	public string Name { get; }
	public int CodePage { get; }
	public bool HasBom { get; }
	public Encoding ToEncoding() => CodePage == Utf8.CodePage ? new UTF8Encoding(HasBom) : Encoding.GetEncoding(CodePage);

	public static FileEncoding Utf8 { get; } = new("UTF-8", Encoding.UTF8.CodePage, false);
	public static FileEncoding Ansi => field ??= All.First(x => x.CodePage == CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
	public static IReadOnlyList<FileEncoding> All
	{
		get
		{
			if (field is not null) return field;
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			var list = new List<FileEncoding>
			{
				Utf8,
				new("UTF-8 (BOM)", Utf8.CodePage, true),
				new("UTF-16", Encoding.Unicode.CodePage, false),
				new("UTF-16 (Big Endian)", Encoding.BigEndianUnicode.CodePage, false),
				new("UTF-32", Encoding.UTF32.CodePage, false),
				new("UTF-32 (Big Endian)", Encoding.UTF32.CodePage + 1, false),
			};
			var ansiCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
			if (!list.Any(x => x.CodePage == ansiCodePage))
				list.Add(new("ANSI", ansiCodePage, false));
			field = [.. list];
			return field;
		}
	}
	public bool Creates(Encoding encoding)
		=> encoding.CodePage == CodePage && (encoding.CodePage != Utf8.CodePage || HasBom == ((UTF8Encoding)encoding).Preamble.Length > 0);
}
