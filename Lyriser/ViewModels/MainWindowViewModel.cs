using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Livet;
using Livet.Commands;
using Livet.EventListeners;
using Livet.Messaging;
using Lyriser.Models;

namespace Lyriser.ViewModels;

class MainWindowViewModel : ViewModel
{
	public MainWindowViewModel()
	{
		var supportedEncodings = new ObservableCollection<EncodingViewModel>();
		foreach (var model in FileEncoding.All)
		{
			var vm = new EncodingViewModel(model);
			CompositeDisposable.Add(vm);
			supportedEncodings.Add(vm);
		}
		SupportedEncodings = new(supportedEncodings);
		CurrentEncoding = SupportedEncodings.First(x => x.Model == _model.CurrentEncoding);

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
				case nameof(_model.CurrentEncoding):
					CurrentEncoding = SupportedEncodings.First(x => x.Model == _model.CurrentEncoding);
					break;
			}
		}));
		var synchronizationContext = SynchronizationContext.Current;
		Debug.Assert(synchronizationContext != null, "SynchronizationContext.Current is null");
		CompositeDisposable.Add(new PropertyChangedEventListener(_model, (s, ev) =>
		{
			if (ev.PropertyName == nameof(_model.ParserErrors))
			{
				synchronizationContext.Post(_ =>
				{
					ParserErrors.Clear();
					foreach (var item in _model.ParserErrors)
						ParserErrors.Add(item);
				}, null);
			}
		}));
		CompositeDisposable.Add(new PropertyChangedEventListener(this, async (s, ev) =>
		{
			switch (ev.PropertyName)
			{
				case nameof(CaretLocation):
					if (LyricsSource != null)
					{
						var logicalLineIndex = LyricsSource.SyllableLines.FindIndexByPhysicalLineIndex(CaretLocation.Line - 1);
						if (logicalLineIndex >= 0 && CurrentSyllable.Line != logicalLineIndex)
						{
							CurrentSyllable = new(logicalLineIndex, 0);
							await ScrollViewerIntoCurrentSyllableAsync();
						}
					}
					break;
				case nameof(CurrentSyllable):
					if (LyricsSource != null)
					{
						var physicalLineNumber = (LyricsSource.SyllableLines.Count > 0 ?
							LyricsSource.SyllableLines[CurrentSyllable.Line].PhysicalLineIndex :
							0) + 1;
						if (CaretLocation.Line != physicalLineNumber)
						{
							CaretLocation = new(physicalLineNumber, 0);
							await ScrollEditorIntoCaretAsync(false);
						}
					}
					break;
			}
		}));

		NewCommand = new(async () => await NewAsync()) { Gesture = new(Key.N, ModifierKeys.Control) };
		OpenCommand = new(async () => await OpenAsync()) { Gesture = new(Key.O, ModifierKeys.Control) };
		SaveAsCommand = new(async () => await SaveAsAsync());
		SaveCommand = new(async () => await SaveAsync()) { Gesture = new(Key.S, ModifierKeys.Control) };
		AutoSetRubyInSelectionCommand = new(AutoSetRubyInSelection);
		HighlightFirstCommand = new(async () => await HighlightLyricsAsync(LyricsHighlightRequest.First));
		HighlightNextCommand = new(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Next));
		HighlightPreviousCommand = new(async () => await HighlightLyricsAsync(LyricsHighlightRequest.Previous));
		HighlightNextLineCommand = new(async () => await HighlightLyricsAsync(LyricsHighlightRequest.NextLine));
		HighlightPreviousLineCommand = new(async () => await HighlightLyricsAsync(LyricsHighlightRequest.PreviousLine));
		MoveCaretToSelectedErrorCommand = new(async () => await MoveCaretToSelectedErrorAsync());
		SelectEncodingCommand = new(async ev => await SelectEncodingAsync(ev));
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

	public ReadOnlyObservableCollection<EncodingViewModel> SupportedEncodings { get; }
	public EncodingViewModel CurrentEncoding { get; private set => RaisePropertyChangedIfSet(ref field, value); }

	public TextLocation CaretLocation { get; set => RaisePropertyChangedIfSet(ref field, value); }
	public SyllableLocation CurrentSyllable { get; set => RaisePropertyChangedIfSet(ref field, value); }
	public ParserError? SelectedError { get; set => RaisePropertyChangedIfSet(ref field, value); }
	public Selection? Selection { get; set => RaisePropertyChangedIfSet(ref field, value); }

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
	public ListenerCommand<SelectionChangedEventArgs> SelectEncodingCommand { get; }

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
		var filePath = await GetOpeningFileAysnc();
		if (filePath == null)
			return;
		_model.Open(filePath);
		await HighlightLyricsAsync(LyricsHighlightRequest.First);
	}
	public async Task SaveAsAsync()
	{
		var filePath = await GetSavingFileAsync();
		if (filePath == null)
			return;
		_model.Save(filePath);
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

	async Task SelectEncodingAsync(SelectionChangedEventArgs selection)
	{
		var newEncoding = (EncodingViewModel)selection.AddedItems[0]!;
		if (CurrentEncoding == newEncoding) return;
		if (_model.SavedFileNameWithoutExtension != null)
		{
			var result = await QueryEncodingChangeAsync(newEncoding.Name);
			if (result == null)
			{
				RaisePropertyChanged(nameof(CurrentEncoding));
				return;
			}
			if (result.Value)
			{
				if (_model.IsModified && !await WarnReopenWithEncodingAsync(newEncoding.Name))
				{
					RaisePropertyChanged(nameof(CurrentEncoding));
					return;
				}
				_model.Reopen(newEncoding.Model);
				await HighlightLyricsAsync(LyricsHighlightRequest.First);
				return;
			}
		}
		_model.CurrentEncoding = newEncoding.Model;
	}
	async Task<bool?> QueryEncodingChangeAsync(string newEncodingName)
	{
		var message = await Messenger.GetResponseAsync(new QueryYesNoCancelMessage("QueryEncodingChange", [DocumentName, newEncodingName]));
		return message.Response;
	}
	async Task<bool> WarnReopenWithEncodingAsync(string newEncodingName)
	{
		var message = await Messenger.GetResponseAsync(new QueryDoCancelMessage("WarnReopenWithEncoding", [DocumentName, newEncodingName]));
		return message.Response;
	}
	async Task<bool?> WarnUnsavedChangeAsync()
	{
		var message = await Messenger.GetResponseAsync(new QueryYesNoCancelMessage("WarnUnsavedChange", [DocumentName]));
		return message.Response;
	}
	async Task<string?> GetOpeningFileAysnc()
	{
		var message = await Messenger.GetResponseAsync(new ResponsiveInteractionMessage<string?>("GetOpeningFile"));
		return message.Response;
	}
	async Task<string?> GetSavingFileAsync()
	{
		var message = await Messenger.GetResponseAsync(new ResponsiveInteractionMessage<string?>("GetSavingFile"));
		return message.Response;
	}
	Task ScrollViewerIntoCurrentSyllableAsync() => Messenger.RaiseAsync(new InteractionMessage("ScrollViewerIntoCurrentSyllable"));
	Task ScrollEditorIntoCaretAsync(bool focus) => Messenger.RaiseAsync(new GenericInteractionMessage<bool>(focus, "ScrollEditorIntoCaret"));
	Task HighlightLyricsAsync(LyricsHighlightRequest request) => Messenger.RaiseAsync(new GenericInteractionMessage<LyricsHighlightRequest>(request, "HighlightLyrics"));
	async Task MoveCaretToSelectedErrorAsync()
	{
		if (SelectedError == null)
			return;
		CaretLocation = new(SelectedError.Location.Line, SelectedError.Location.Column);
		await ScrollEditorIntoCaretAsync(true);
	}
}

public class EncodingViewModel(FileEncoding model) : ViewModel
{
	public FileEncoding Model { get; } = model;
	public string Name => Model.Name;
}

public enum LyricsHighlightRequest
{
	Next,
	Previous,
	NextLine,
	PreviousLine,
	First,
	Last,
}
