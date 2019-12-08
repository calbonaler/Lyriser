﻿using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using Livet;
using Livet.Commands;
using Livet.EventListeners;
using Livet.Messaging;
using Lyriser.Models;

namespace Lyriser.ViewModels
{
	class MainWindowViewModel : ViewModel
	{
		public MainWindowViewModel()
		{
			SourceDocument = new TextDocument();
			SourceDocument.TextChanged += OnSourceDocumentTextChanged;
			ParserErrors = Utils.CreateReadOnlyDispatcherCollection(m_Model.ParserErrors, DispatcherHelper.UIDispatcher);
			CompositeDisposable.Add(new PropertyChangedEventListener(m_Model, (s, ev) =>
			{
				switch (ev.PropertyName)
				{
					case nameof(m_Model.Source):
						if (SourceDocument.Text != m_Model.Source)
							SourceDocument.Text = m_Model.Source;
						break;
					case nameof(m_Model.LyricsSource):
						RaisePropertyChanged(nameof(LyricsSource));
						break;
					case nameof(m_Model.IsModified):
						RaisePropertyChanged(nameof(IsModified));
						break;
					case nameof(m_Model.SavedFileInfo):
						RaisePropertyChanged(nameof(DocumentName));
						break;
				}
			}));
			CompositeDisposable.Add(new PropertyChangedEventListener(this, async (s, ev) =>
			{
				switch (ev.PropertyName)
				{
					case nameof(CaretLocation):
						if (LyricsSource != null)
						{
							var logicalLineIndex = LyricsSource.LineMap.GetLogicalLineIndexByPhysical(CaretLocation.Line - 1);
							if (logicalLineIndex >= 0)
								await ForcusViewSyllableAsync(logicalLineIndex, 0);
						}
						break;
					case nameof(CurrentSyllable):
						if (LyricsSource != null)
						{
							var physicalLineIndex = LyricsSource.LineMap.GetPhysicalLineIndexByLogical(CurrentSyllable.Line);
							await FocusEditorCharacterAsync(physicalLineIndex + 1, -1, false);
						}
						break;
				}
			}));

			NewCommand = new HotKeyCommand(async () => await NewAsync()) { Gesture = new KeyGesture(Key.N, ModifierKeys.Control) };
			OpenCommand = new HotKeyCommand(async () => await OpenAsync()) { Gesture = new KeyGesture(Key.O, ModifierKeys.Control) };
			SaveAsCommand = new ViewModelCommand(async () => await SaveAsAsync());
			SaveCommand = new HotKeyCommand(async () => await SaveAsync()) { Gesture = new KeyGesture(Key.S, ModifierKeys.Control) };
			HighlightFirstCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.First));
			HighlightNextCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Next));
			HighlightPreviousCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Previous));
			HighlightNextLineCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.NextLine));
			HighlightPreviousLineCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.PreviousLine));
			MoveCaretToSelectedErrorCommand = new ViewModelCommand(async () => await MoveCaretToSelectedErrorAsync());
		}

		readonly Model m_Model = new Model();

		public TextDocument SourceDocument { get; }
		public LyricsSource LyricsSource
		{
			get => m_Model.LyricsSource;
			set => m_Model.LyricsSource = value;
		}
		public ReadOnlyDispatcherCollection<ParserError> ParserErrors { get; }
		public bool IsModified => m_Model.IsModified;
		public string DocumentName => m_Model.SavedFileInfo?.FileNameWithoutExtension ?? "無題";

		TextLocation m_CaretLocation;
		public TextLocation CaretLocation
		{
			get => m_CaretLocation;
			set => RaisePropertyChangedIfSet(ref m_CaretLocation, value);
		}

		SyllableLocation m_CurrentSyllable;
		public SyllableLocation CurrentSyllable
		{
			get => m_CurrentSyllable;
			set => RaisePropertyChangedIfSet(ref m_CurrentSyllable, value);
		}

		ParserError m_SelectedError;
		public ParserError SelectedError
		{
			get => m_SelectedError;
			set => RaisePropertyChangedIfSet(ref m_SelectedError, value);
		}

		public HotKeyCommand NewCommand { get; }
		public HotKeyCommand OpenCommand { get; }
		public ViewModelCommand SaveAsCommand { get; }
		public HotKeyCommand SaveCommand { get; }
		public ViewModelCommand HighlightFirstCommand { get; }
		public ViewModelCommand HighlightNextCommand { get; }
		public ViewModelCommand HighlightPreviousCommand { get; }
		public ViewModelCommand HighlightNextLineCommand { get; }
		public ViewModelCommand HighlightPreviousLineCommand { get; }
		public ViewModelCommand MoveCaretToSelectedErrorCommand { get; }

		public async Task NewAsync()
		{
			if (!await ConfirmSaveAsync())
				return;
			m_Model.New();
			SourceDocument.UndoStack.ClearAll();
			await HighlightLyricsAsync(LyricsHighlightRequest.First);
		}
		public async Task OpenAsync()
		{
			if (!await ConfirmSaveAsync())
				return;
			var metadata = await GetOpeningFileAysnc();
			if (metadata == null)
				return;
			m_Model.Open(metadata);
			SourceDocument.UndoStack.ClearAll();
			await HighlightLyricsAsync(LyricsHighlightRequest.First);
		}
		public async Task SaveAsAsync()
		{
			var metadata = await GetSavingFileAsync();
			if (metadata == null)
				return;
			m_Model.Save(metadata);
		}
		public async Task SaveAsync()
		{
			if (m_Model.SavedFileInfo == null)
			{
				await SaveAsAsync();
				return;
			}
			m_Model.Save(m_Model.SavedFileInfo);
		}
		public async Task<bool> ConfirmSaveAsync()
		{
			if (m_Model.IsModified)
			{
				var action = await WarnUnsavedChangeAsync();
				if (action == null)
					return false;
				if (action == true)
					await SaveAsync();
			}
			return true;
		}

		async Task<bool?> WarnUnsavedChangeAsync()
		{
			var message = await Messenger.GetResponseAsync(new WarnUnsavedChangeMessage("WarnUnsavedChange", DocumentName));
			return message.Response;
		}
		async Task<EncodedFileInfo> GetOpeningFileAysnc()
		{
			var message = await Messenger.GetResponseAsync(new ResponsiveInteractionMessage<EncodedFileInfo>("GetOpeningFile"));
			return message.Response;
		}
		async Task<EncodedFileInfo> GetSavingFileAsync()
		{
			var message = await Messenger.GetResponseAsync(new ResponsiveInteractionMessage<EncodedFileInfo>("GetSavingFile"));
			return message.Response;
		}
		Task ForcusViewSyllableAsync(int line, int column) => Messenger.RaiseAsync(new GenericInteractionMessage<SyncFocusArgument>(new SyncFocusArgument(line, column, false), "FocusViewSyllable"));
		Task FocusEditorCharacterAsync(int line, int column, bool force) => Messenger.RaiseAsync(new GenericInteractionMessage<SyncFocusArgument>(new SyncFocusArgument(line, column, force), "FocusEditorCharacter"));
		Task HighlightLyricsAsync(LyricsHighlightRequest request) => Messenger.RaiseAsync(new GenericInteractionMessage<LyricsHighlightRequest>(request, "HighlightLyrics"));
		async Task MoveCaretToSelectedErrorAsync()
		{
			if (SelectedError != null)
				await FocusEditorCharacterAsync(SelectedError.Location.Line, SelectedError.Location.Column, true);
		}
		void OnSourceDocumentTextChanged(object sender, EventArgs e) => m_Model.Source = SourceDocument.Text;
	}

	public enum LyricsHighlightRequest
	{
		None,
		Next,
		Previous,
		NextLine,
		PreviousLine,
		First,
		Last,
	}

	public class SyncFocusArgument
	{
		public SyncFocusArgument(int line, int column, bool force)
		{
			Line = line;
			Column = column;
			Force = force;
		}

		public int Line { get; }
		public int Column { get; }
		public bool Force { get; }
	}
}
