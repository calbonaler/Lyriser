﻿using System;
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

namespace Lyriser.Models
{
	public class Model : INotifyPropertyChanged
	{
		public Model(IMonoRubyProvider monoRubyProvider)
		{
			m_MonoRubyProvider = monoRubyProvider;
			m_Parser.ErrorReporter = error => m_BackingParserErrors.Add(error);
			m_OriginalVersion = SourceDocument.Version;
			Observable.FromEventPattern(x => SourceDocument.TextChanged += x, x => SourceDocument.TextChanged -= x)
				.Do(_ => PropertyChanged.Raise(this, nameof(IsModified)))
				.Throttle(TimeSpan.FromMilliseconds(500))
				.Subscribe(_ =>
				{
					m_BackingParserErrors.Clear();
					LyricsSource = new LyricsSource(m_Parser.Parse(SourceDocument.CreateSnapshot().Text).Select(x => x.Line));
					ParserErrors = m_BackingParserErrors.ToArray();
				});
		}

		IMonoRubyProvider m_MonoRubyProvider;

		readonly LyricsParser m_Parser = new LyricsParser();
		ITextSourceVersion m_OriginalVersion;
		ITextSourceVersion OriginalVersion
		{
			get => m_OriginalVersion;
			set
			{
				if (m_OriginalVersion != value)
				{
					m_OriginalVersion = value;
					PropertyChanged?.Raise(this, nameof(IsModified));
				}
			}
		}

		public bool IsModified => SourceDocument.Version != OriginalVersion;

		EncodedFileInfo m_SavedFileInfo;
		public EncodedFileInfo SavedFileInfo
		{
			get => m_SavedFileInfo;
			private set => Utils.SetProperty(ref m_SavedFileInfo, value, PropertyChanged, this);
		}

		public TextDocument SourceDocument { get; } = new TextDocument();

		LyricsSource m_LyricsSource = LyricsSource.Empty;
		public LyricsSource LyricsSource
		{
			get => m_LyricsSource;
			set => Utils.SetProperty(ref m_LyricsSource, value, PropertyChanged, this);
		}

		readonly List<ParserError> m_BackingParserErrors = new List<ParserError>();
		IReadOnlyList<ParserError> m_ParserErrors = Array.Empty<ParserError>();
		public IReadOnlyList<ParserError> ParserErrors
		{
			get => m_ParserErrors;
			private set => Utils.SetProperty(ref m_ParserErrors, value, PropertyChanged, this);
		}

		public void New()
		{
			SourceDocument.Remove(0, SourceDocument.TextLength);
			SourceDocument.UndoStack.ClearAll();
			SavedFileInfo = null;
			OriginalVersion = SourceDocument.Version;
		}

		public void Open(EncodedFileInfo info)
		{
			if (info.Encoding == null)
			{
				using (var fs = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = FileReader.OpenStream(fs, new UTF8Encoding(false)))
				{
					SourceDocument.Text = reader.ReadToEnd();
					SavedFileInfo = new EncodedFileInfo(info.Path, reader.CurrentEncoding);
				}
			}
			else
			{
				SourceDocument.Text = File.ReadAllText(info.Path, info.Encoding);
				SourceDocument.UndoStack.ClearAll();
				SavedFileInfo = info;
			}
			OriginalVersion = SourceDocument.Version;
		}

		public void Save(EncodedFileInfo info)
		{
			using (var fs = new FileStream(info.Path, FileMode.Create, FileAccess.Write, FileShare.Read))
			using (var writer = new StreamWriter(fs, info.Encoding))
				SourceDocument.WriteTextTo(writer);
			SavedFileInfo = info;
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
				if (!(m_MonoRubyProvider.GetMonoRuby(baseText) is MonoRuby monoRuby))
				{
					newSourceBuilder.Append(LyricsNodeBase.GenerateSource(lyricsLine));
					newSourceBuilder.Append(lineTerminator);
					continue;
				}
				// GetMonoRubyでモノルビ分割がされない場合の送り仮名向け特殊処理
				var baseTextElements = GetTextElements(baseText);
				var rubyTextElements = GetTextElements(monoRuby.Text);
				for (int i = baseTextElements.Length - 1, j = rubyTextElements.Length - 1; i >= 0 && j >= 0; )
				{
					if (baseTextElements[i].Text != rubyTextElements[j].Text)
					{
						// ルビとベースで対応する文字が異なる場合はモノルビの対応関係が設定されている箇所まで進める
						while ((j = monoRuby.Indexes[baseTextElements[i].Index]) == MonoRuby.UnmatchedPosition)
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
					if (monoRuby.Indexes[i] == MonoRuby.UnmatchedPosition)
						continue;
					// 既存ルビの作成
					var rubyBase = baseText.Substring(rubyBaseStart, i - rubyBaseStart);
					var ruby = monoRuby.Text.Substring(rubyStart, monoRuby.Indexes[i] - rubyStart);
					// 下記のすべてに該当する場合のみルビを作成する
					// ・ルビのベースはカテゴリLoのCode Pointのみから生成される
					// ・ルビのベースにはひらがな、カタカナ、半角・全角形類を含まない
					if (Regex.IsMatch(rubyBase, "^[\\p{Lo}]+$") && !Regex.IsMatch(rubyBase, "^[\\p{IsHiragana}\\p{IsKatakana}\\p{IsKatakanaPhoneticExtensions}\\p{IsHalfwidthandFullwidthForms}]+$"))
					{
						var nodes = new List<SimpleNode>();
						for (var j = rubyBaseStart; j < i;)
						{
							var node = textToNodeMap[j];
							nodes.Add(node);
							j += node.PhoneticText.Length;
						}
						rubySpecifiers.Enqueue(new RubySpecifier(nodes, ruby));
					}
					// 新規ルビの記録開始
					rubyBaseStart = i;
					rubyStart = monoRuby.Indexes[i];
				}
				newSourceBuilder.Append(LyricsNodeBase.GenerateSource(SetRuby(lyricsLine, rubySpecifiers)));
				newSourceBuilder.Append(lineTerminator);
				Debug.Assert(rubySpecifiers.Count == 0);
			}
			SourceDocument.Replace(segment, newSourceBuilder.ToString());
		}

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
				if (!result) return indexes.ToArray();
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
					sb.Append(simpleNode.PhoneticText);
				}
				else if (node is CompositeNode compositeNode)
				{
					foreach (var subNode in compositeNode.Text)
					{
						textToNodeMap[sb.Length] = subNode;
						sb.Append(subNode.PhoneticText);
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
				var nodeIndex = rubySpecifiers.Count <= 0 ? -1 : Array.IndexOf(rubySpecifiers.Peek().Text, simpleNode);
				if (nodeIndex < 0)
				{
					newNodes.Add(simpleNode);
					return;
				}
				if (nodeIndex == rubySpecifiers.Peek().Text.Length - 1)
				{
					var rubySpec = rubySpecifiers.Dequeue();
					var rubyNodes = new List<SimpleNode>();
					for (var i = 0; i < rubySpec.Ruby.Length;)
					{
						var textLength = i + 1 < rubySpec.Ruby.Length && char.IsSurrogatePair(rubySpec.Ruby, i) ? 2 : 1;
						rubyNodes.Add(new SimpleNode(rubySpec.Ruby.Substring(i, textLength), default));
						i += textLength;
					}
					newNodes.Add(new CompositeNode(rubySpec.Text, rubyNodes, default));
				}
			}

			foreach (var node in nodes)
			{
				if (node is SimpleNode simpleNode)
					ProcessSimpleNode(simpleNode);
				else if (node is CompositeNode compositeNode)
				{
					foreach (var subNode in compositeNode.Text)
						ProcessSimpleNode(subNode);
				}
				else
					newNodes.Add(new SilentNode(SetRuby(((SilentNode)node).Nodes, rubySpecifiers), default));
			}

			return newNodes.ToArray();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		readonly struct TextElement : IEquatable<TextElement>
		{
			public TextElement(string wholeText, int index, int length)
			{
				WholeText = wholeText;
				Index = index;
				Length = length;
			}

			public readonly string WholeText;
			public readonly int Index;
			public readonly int Length;
			public string Text => WholeText.Substring(Index, Length);

			public bool Equals(TextElement other) => WholeText == other.WholeText && Index == other.Index && Length == other.Length;
			public override bool Equals(object obj) => obj is TextElement other && Equals(other);
			public override int GetHashCode()
			{
				var hashCode = 518592628;
				hashCode = hashCode * -1521134295 + WholeText.GetHashCode();
				hashCode = hashCode * -1521134295 + Index.GetHashCode();
				hashCode = hashCode * -1521134295 + Length.GetHashCode();
				return hashCode;
			}
			public static bool operator ==(TextElement left, TextElement right) => left.Equals(right);
			public static bool operator !=(TextElement left, TextElement right) => !(left == right);
		}

		readonly struct RubySpecifier : IEquatable<RubySpecifier>
		{
			public RubySpecifier(IEnumerable<SimpleNode> text, string ruby)
			{
				Text = (text ?? throw new ArgumentNullException(nameof(text))).ToArray();
				Ruby = ruby ?? throw new ArgumentNullException(nameof(ruby));
			}

			public readonly SimpleNode[] Text;
			public readonly string Ruby;

			public override bool Equals(object obj) => obj is RubySpecifier other && Equals(other);
			public bool Equals(RubySpecifier other) => Text == other.Text && Ruby == other.Ruby;
			public override int GetHashCode()
			{
				var hashCode = 518592628;
				hashCode = hashCode * -1521134295 + Text.GetHashCode();
				hashCode = hashCode * -1521134295 + Ruby.GetHashCode();
				return hashCode;
			}
			public static bool operator ==(RubySpecifier left, RubySpecifier right) => left.Equals(right);
			public static bool operator !=(RubySpecifier left, RubySpecifier right) => !(left == right);
		}
	}

	public class EncodedFileInfo
	{
		public EncodedFileInfo(string path, Encoding encoding)
		{
			Path = path ?? throw new ArgumentNullException(nameof(path));
			Encoding = encoding;
		}

		public string Path { get; }
		public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);
		public Encoding Encoding { get; }
	}
}
