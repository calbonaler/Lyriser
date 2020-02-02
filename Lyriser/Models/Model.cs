using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.Utils;

namespace Lyriser.Models
{
	class Model : INotifyPropertyChanged
	{
		public Model()
		{
			m_Parser.ErrorReporter = error => m_BackingParserErrors.Add(error);
			this.AsPropertyChanged(nameof(Source))
				.Do(_ => IsModified = true)
				.Throttle(TimeSpan.FromMilliseconds(500))
				.Subscribe(_ =>
				{
					m_BackingParserErrors.Clear();
					LyricsSource = m_Parser.Parse(Source);
					ParserErrors = m_BackingParserErrors.ToArray();
				});
		}

		readonly LyricsParser m_Parser = new LyricsParser();

		bool m_IsModified = false;
		public bool IsModified
		{
			get => m_IsModified;
			private set => Utils.SetProperty(ref m_IsModified, value, PropertyChanged, this);
		}

		EncodedFileInfo m_SavedFileInfo;
		public EncodedFileInfo SavedFileInfo
		{
			get => m_SavedFileInfo;
			private set => Utils.SetProperty(ref m_SavedFileInfo, value, PropertyChanged, this);
		}

		string m_Source = string.Empty;
		public string Source
		{
			get => m_Source;
			set => Utils.SetProperty(ref m_Source, value, PropertyChanged, this);
		}

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
			Source = string.Empty;
			SavedFileInfo = null;
			IsModified = false;
		}

		public void Open(EncodedFileInfo info)
		{
			if (info.Encoding == null)
			{
				using (var fs = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (var reader = FileReader.OpenStream(fs, new UTF8Encoding(false)))
				{
					Source = reader.ReadToEnd();
					SavedFileInfo = new EncodedFileInfo(info.Path, reader.CurrentEncoding);
				}
			}
			else
			{
				Source = File.ReadAllText(info.Path, info.Encoding);
				SavedFileInfo = info;
			}
			IsModified = false;
		}

		public void Save(EncodedFileInfo info)
		{
			File.WriteAllText(info.Path, Source, info.Encoding);
			SavedFileInfo = info;
			IsModified = false;
		}

		public event PropertyChangedEventHandler PropertyChanged;
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
