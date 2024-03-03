using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Livet;
using Livet.Commands;
using Livet.EventListeners;
using Livet.Messaging;
using Lyriser.Models;
using System.Threading;
using System.Diagnostics;

namespace Lyriser.ViewModels;

class MainWindowViewModel : ViewModel
{
	public MainWindowViewModel()
	{
		CompositeDisposable.Add(new PropertyChangedEventListener(_model, (s, ev) =>
		{
			switch (ev.PropertyName)
			{
				case nameof(_model.LyricsSource):
					RaisePropertyChanged(nameof(LyricsSource));
					break;
				case nameof(_model.IsModified):
					RaisePropertyChanged(nameof(IsModified));
					break;
				case nameof(_model.SavedFileNameWithoutExtension):
					RaisePropertyChanged(nameof(DocumentName));
					break;
			}
		}));
		var synchronizationContext = SynchronizationContext.Current;
		Debug.Assert(synchronizationContext != null, "SynchronizationContext.Current is null");
		CompositeDisposable.Add(
			_model.AsPropertyChanged(nameof(_model.ParserErrors))
				.Throttle(TimeSpan.FromMilliseconds(1000))
				.ObserveOn(synchronizationContext)
				.Subscribe(_ =>
				{
					ParserErrors.Clear();
					foreach (var item in _model.ParserErrors)
						ParserErrors.Add(item);
				})
		);
		CompositeDisposable.Add(new PropertyChangedEventListener(this, async (s, ev) =>
		{
			switch (ev.PropertyName)
			{
				case nameof(CaretLocation):
					if (LyricsSource != null)
					{
						var logicalLineIndex = LyricsSource.LineMap.GetLogicalLineIndexByPhysical(CaretLocation.Line - 1);
						if (logicalLineIndex >= 0 && CurrentSyllable.Line != logicalLineIndex)
						{
							CurrentSyllable = new SyllableLocation(logicalLineIndex, 0);
							await ScrollViewerIntoCurrentSyllableAsync();
						}
					}
					break;
				case nameof(CurrentSyllable):
					if (LyricsSource != null)
					{
						var physicalLineNumber = LyricsSource.LineMap.GetPhysicalLineIndexByLogical(CurrentSyllable.Line) + 1;
						if (CaretLocation.Line != physicalLineNumber)
						{
							CaretLocation = new TextLocation(physicalLineNumber, 0);
							await ScrollEditorIntoCaretAsync(false);
						}
					}
					break;
			}
		}));

		NewCommand = new HotKeyCommand(async () => await NewAsync()) { Gesture = new KeyGesture(Key.N, ModifierKeys.Control) };
		OpenCommand = new HotKeyCommand(async () => await OpenAsync()) { Gesture = new KeyGesture(Key.O, ModifierKeys.Control) };
		SaveAsCommand = new ViewModelCommand(async () => await SaveAsAsync());
		SaveCommand = new HotKeyCommand(async () => await SaveAsync()) { Gesture = new KeyGesture(Key.S, ModifierKeys.Control) };
		AutoSetRubyInSelectionCommand = new ViewModelCommand(AutoSetRubyInSelection);
		HighlightFirstCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.First));
		HighlightNextCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Next));
		HighlightPreviousCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Previous));
		HighlightNextLineCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.NextLine));
		HighlightPreviousLineCommand = new ViewModelCommand(async () => await HighlightLyricsAsync(LyricsHighlightRequest.PreviousLine));
		MoveCaretToSelectedErrorCommand = new ViewModelCommand(async () => await MoveCaretToSelectedErrorAsync());
	}

	readonly Model _model = new(ImeLanguage.Instance);

	public TextDocument SourceDocument => _model.SourceDocument;
	public LyricsSource LyricsSource
	{
		get => _model.LyricsSource;
		set => _model.LyricsSource = value;
	}
	public ObservableCollection<ParserError> ParserErrors { get; } = [];
	public bool IsModified => _model.IsModified;
	public string DocumentName => _model.SavedFileNameWithoutExtension ?? "無題";

	TextLocation _caretLocation;
	public TextLocation CaretLocation
	{
		get => _caretLocation;
		set => RaisePropertyChangedIfSet(ref _caretLocation, value);
	}

	SyllableLocation _currentSyllable;
	public SyllableLocation CurrentSyllable
	{
		get => _currentSyllable;
		set => RaisePropertyChangedIfSet(ref _currentSyllable, value);
	}

	ParserError? _selectedError;
	public ParserError? SelectedError
	{
		get => _selectedError;
		set => RaisePropertyChangedIfSet(ref _selectedError, value);
	}

	Selection? _selection;
	public Selection? Selection
	{
		get => _selection;
		set => RaisePropertyChangedIfSet(ref _selection, value);
	}

	public HotKeyCommand NewCommand { get; }
	public HotKeyCommand OpenCommand { get; }
	public ViewModelCommand SaveAsCommand { get; }
	public HotKeyCommand SaveCommand { get; }
	public ViewModelCommand AutoSetRubyInSelectionCommand { get; }
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
		_model.New();
		await HighlightLyricsAsync(LyricsHighlightRequest.First);
	}
	public async Task OpenAsync()
	{
		if (!await ConfirmSaveAsync())
			return;
		var metadata = await GetOpeningFileAysnc();
		if (metadata == null)
			return;
		_model.Open(metadata);
		await HighlightLyricsAsync(LyricsHighlightRequest.First);
	}
	public async Task SaveAsAsync()
	{
		var metadata = await GetSavingFileAsync();
		if (metadata == null)
			return;
		_model.Save(metadata);
	}
	public async Task SaveAsync()
	{
		if (_model.SavedFileNameWithoutExtension == null)
		{
			await SaveAsAsync();
			return;
		}
		_model.Save();
	}
	public async Task<bool> ConfirmSaveAsync()
	{
		if (_model.IsModified)
		{
			var action = await WarnUnsavedChangeAsync();
			if (action == null)
				return false;
			if (action == true)
				await SaveAsync();
		}
		return true;
	}
	public void AutoSetRubyInSelection()
	{
		if (Selection != null)
			_model.AutoSetRuby(Selection.SurroundingSegment);
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
	Task ScrollViewerIntoCurrentSyllableAsync() => Messenger.RaiseAsync(new InteractionMessage("ScrollViewerIntoCurrentSyllable"));
	Task ScrollEditorIntoCaretAsync(bool focus) => Messenger.RaiseAsync(new GenericInteractionMessage<bool>(focus, "ScrollEditorIntoCaret"));
	Task HighlightLyricsAsync(LyricsHighlightRequest request) => Messenger.RaiseAsync(new GenericInteractionMessage<LyricsHighlightRequest>(request, "HighlightLyrics"));
	async Task MoveCaretToSelectedErrorAsync()
	{
		if (SelectedError == null)
			return;
		CaretLocation = new TextLocation(SelectedError.Location.Line, SelectedError.Location.Column);
		await ScrollEditorIntoCaretAsync(true);
	}
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
