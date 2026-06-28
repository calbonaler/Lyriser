using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Lyriser.ViewModels;
using Microsoft.Win32;
using TaskDialogLite;

namespace Lyriser.Views;

/// <summary>
/// MainWindow.xaml の相互作用ロジック
/// </summary>
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		RegisterMessageRecipients(WeakReferenceMessenger.Default);
	}

	void RegisterMessageRecipients(IMessenger messenger)
	{
		messenger.Register(this, static (MainWindow r, WarnUnsavedChangeMessage m) =>
		{
			var yesButton = new TaskDialogButton("保存する(&S)");
			var noButton = new TaskDialogButton("保存しない(&N)");
			var result = new TaskDialog
			{
				Title = ProductName,
				HeaderText = $"{m.DocumentName} への変更を保存しますか?",
				Buttons = [yesButton, noButton, TaskDialogButton.Cancel],
			}.Show(r.Handle);
			m.Reply(result == yesButton ? true : result == noButton ? false : null);
		});
		messenger.Register(this, static (MainWindow r, GetOpeningFileMessage m) => m.Reply(r.ShowFileDialog(new OpenFileDialog
		{
			Filter = "テキストファイル|*.txt",
			DefaultExt = "txt"
		})));
		messenger.Register(this, static (MainWindow r, GetSavingFileMessage m) => m.Reply(r.ShowFileDialog(new SaveFileDialog
		{
			Filter = "テキストファイル|*.txt",
			DefaultExt = "txt"
		})));
		messenger.Register(this, static (MainWindow r, QueryEncodingChangeMessage m) =>
		{
			var yesButton = new TaskDialogButton("開き直す(&R)");
			var noButton = new TaskDialogButton("保存時の文字コードのみ変更する(&S)");
			var result = new TaskDialog
			{
				Title = ProductName,
				HeaderText = $"{m.DocumentName} を {m.NewEncodingName} で開き直しますか?",
				Buttons = [yesButton, noButton, TaskDialogButton.Cancel],
			}.Show(r.Handle);
			m.Reply(result == yesButton ? true : result == noButton ? false : null);
		});
		messenger.Register(this, static (MainWindow r, WarnReopenWithEncodingMessage m) => m.Reply(new TaskDialog
		{
			Title = ProductName,
			HeaderText = $"{m.DocumentName} への変更を破棄して {m.NewEncodingName} で開き直しますか?",
			Buttons = [new("変更を破棄して開き直す(&R)"), TaskDialogButton.Cancel],
		}.Show(r.Handle) != TaskDialogButton.Cancel));
		messenger.Register(this, static (MainWindow r, ScrollViewerIntoCurrentSyllableMessage m) => r.LyricsViewer.ScrollIntoCurrentSyllable());
		messenger.Register(this, static (MainWindow r, HighlightLyricsMessage m) =>
		{
			switch (m.Request)
			{
				case LyricsHighlightRequest.Next:
					r.LyricsViewer.HighlightNext(true);
					break;
				case LyricsHighlightRequest.Previous:
					r.LyricsViewer.HighlightNext(false);
					break;
				case LyricsHighlightRequest.NextLine:
					r.LyricsViewer.HighlightNextLine(true);
					break;
				case LyricsHighlightRequest.PreviousLine:
					r.LyricsViewer.HighlightNextLine(false);
					break;
				case LyricsHighlightRequest.First:
					r.LyricsViewer.HighlightFirst();
					break;
				case LyricsHighlightRequest.Last:
					r.LyricsViewer.HighlightLast();
					break;
			}
		});
		messenger.Register(this, static (MainWindow r, ScrollEditorIntoCaretMessage m) =>
		{
			if (m.Focus) _ = r.LyricsEditor.Focus();
			var caretLocation = r.LyricsEditor.TextArea.Caret.Location;
			r.LyricsEditor.ScrollTo(caretLocation.Line, caretLocation.Column);
		});
	}

	void OnExitMenuItemClick(object sender, RoutedEventArgs e) => Close();

	string? ShowFileDialog(FileDialog dialog) => dialog.ShowDialog(this) == true ? dialog.FileName : null;
	static string? ProductName => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product;
	nint Handle => ((HwndSource)PresentationSource.FromVisual(this)).Handle;
}
