using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Lyriser.Models;

namespace Lyriser.ViewModels;

public partial class MainWindowViewModel : ObservableRecipient
{
	public MainWindowViewModel()
	{
		var synchronizationContext = SynchronizationContext.Current;
		Debug.Assert(synchronizationContext != null, "SynchronizationContext.Current is null");
		_model.PropertyChanged += (s, ev) =>
		{
			switch (ev.PropertyName)
			{
				case nameof(_model.LyricsSource):
					OnPropertyChanged(nameof(LyricsSource));
					break;
				case nameof(_model.IsModified):
					OnPropertyChanged(nameof(IsModified));
					break;
				case nameof(_model.SavedFileNameWithoutExtension):
					OnPropertyChanged(nameof(DocumentName));
					break;
				case nameof(_model.CurrentEncoding):
					OnPropertyChanged(nameof(CurrentEncoding));
					break;
				case nameof(_model.ParserErrors):
					synchronizationContext.Post(_ =>
					{
						ParserErrors.Clear();
						foreach (var item in _model.ParserErrors)
							ParserErrors.Add(item);
					}, null);
					break;
			}
		};
		PropertyChanged += (s, ev) =>
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
							ScrollViewerIntoCurrentSyllable();
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
							ScrollEditorIntoCaret(false);
						}
					}
					break;
			}
		};

		NewCommand = new(New) { Gesture = new(Key.N, ModifierKeys.Control) };
		OpenCommand = new(Open) { Gesture = new(Key.O, ModifierKeys.Control) };
		SaveCommand = new(Save) { Gesture = new(Key.S, ModifierKeys.Control) };
		SaveConfirmation = ConfirmSave;
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

	public static IReadOnlyList<FileEncoding> SupportedEncodings => FileEncoding.All;
	public FileEncoding CurrentEncoding => _model.CurrentEncoding;

	[ObservableProperty]
	public partial TextLocation CaretLocation { get; set; }
	[ObservableProperty]
	public partial SyllableLocation CurrentSyllable { get; set; }
	[ObservableProperty]
	public partial ParserError? SelectedError { get; set; }
	[ObservableProperty]
	public partial Selection? Selection { get; set; }

	public HotKeyCommand NewCommand { get; }
	public HotKeyCommand OpenCommand { get; }
	public HotKeyCommand SaveCommand { get; }
	public Func<bool> SaveConfirmation { get; }

	public void New()
	{
		if (!ConfirmSave())
			return;
		_model.New();
		HighlightFirst();
	}
	public void Open()
	{
		if (!ConfirmSave())
			return;
		var filePath = GetOpeningFile();
		if (filePath == null)
			return;
		_model.Open(filePath);
		HighlightFirst();
	}
	[RelayCommand]
	public void SaveAs() => SaveAsInternal();
	public void Save() => SaveInternal();
	bool SaveAsInternal()
	{
		var filePath = GetSavingFile();
		if (filePath == null)
			return false;
		_model.Save(filePath);
		return true;
	}
	bool SaveInternal()
	{
		if (_model.SavedFileNameWithoutExtension == null)
			return SaveAsInternal();
		_model.Save();
		return true;
	}
	bool ConfirmSave()
	{
		if (_model.IsModified)
		{
			var action = WarnUnsavedChange();
			if (action == null || action == true && !SaveInternal())
				return false;
		}
		return true;
	}
	[RelayCommand]
	public void AutoSetRubyInSelection()
	{
		if (Selection != null)
			_model.AutoSetRuby(Selection.SurroundingSegment);
	}
	[RelayCommand]
	public void HighlightFirst() => HighlightLyrics(LyricsHighlightRequest.First);
	[RelayCommand]
	public void HighlightNext() => HighlightLyrics(LyricsHighlightRequest.Next);
	[RelayCommand]
	public void HighlightPrevious() => HighlightLyrics(LyricsHighlightRequest.Previous);
	[RelayCommand]
	public void HighlightNextLine() => HighlightLyrics(LyricsHighlightRequest.NextLine);
	[RelayCommand]
	public void HighlightPreviousLine() => HighlightLyrics(LyricsHighlightRequest.PreviousLine);
	[RelayCommand]
	public void MoveCaretToSelectedError()
	{
		if (SelectedError == null)
			return;
		CaretLocation = new(SelectedError.Location.Line, SelectedError.Location.Column);
		ScrollEditorIntoCaret(true);
	}
	[RelayCommand]
	public void SelectEncoding(SelectionChangedEventArgs selection)
	{
		var newEncoding = (FileEncoding)selection.AddedItems[0]!;
		if (CurrentEncoding == newEncoding) return;
		if (_model.SavedFileNameWithoutExtension != null)
		{
			var result = QueryEncodingChange(newEncoding.Name);
			if (result == null)
			{
				OnPropertyChanged(nameof(CurrentEncoding));
				return;
			}
			if (result.Value)
			{
				if (_model.IsModified && !WarnReopenWithEncoding(newEncoding.Name))
				{
					OnPropertyChanged(nameof(CurrentEncoding));
					return;
				}
				_model.Reopen(newEncoding);
				HighlightFirst();
				return;
			}
		}
		_model.CurrentEncoding = newEncoding;
	}

	bool? QueryEncodingChange(string newEncodingName) => Messenger.Send(new QueryEncodingChangeMessage(DocumentName, newEncodingName)).Response;
	bool WarnReopenWithEncoding(string newEncodingName) => Messenger.Send(new WarnReopenWithEncodingMessage(DocumentName, newEncodingName)).Response;
	bool? WarnUnsavedChange() => Messenger.Send(new WarnUnsavedChangeMessage(DocumentName)).Response;
	string? GetOpeningFile() => Messenger.Send(new GetOpeningFileMessage()).Response;
	string? GetSavingFile() => Messenger.Send(new GetSavingFileMessage()).Response;
	void ScrollViewerIntoCurrentSyllable() => Messenger.Send(new ScrollViewerIntoCurrentSyllableMessage());
	void ScrollEditorIntoCaret(bool focus) => Messenger.Send(new ScrollEditorIntoCaretMessage(focus));
	void HighlightLyrics(LyricsHighlightRequest request) => Messenger.Send(new HighlightLyricsMessage(request));
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
