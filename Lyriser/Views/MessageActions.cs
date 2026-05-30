using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using Livet.Behaviors.Messaging;
using Livet.Messaging;
using Lyriser.ViewModels;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Lyriser.Views;

public class QueryYesNoCancelAction : InteractionMessageAction<Window>
{
	public static readonly DependencyProperty MessageFormatProperty = DependencyProperty.Register(nameof(MessageFormat), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty YesButtonTextProperty = DependencyProperty.Register(nameof(YesButtonText), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty NoButtonTextProperty = DependencyProperty.Register(nameof(NoButtonText), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(QueryYesNoCancelAction));

	public string MessageFormat
	{
		get => (string)GetValue(MessageFormatProperty);
		set => SetValue(MessageFormatProperty, value);
	}
	public string YesButtonText
	{
		get => (string)GetValue(YesButtonTextProperty);
		set => SetValue(YesButtonTextProperty, value);
	}
	public string NoButtonText
	{
		get => (string)GetValue(NoButtonTextProperty);
		set => SetValue(NoButtonTextProperty, value);
	}
	public string CancelButtonText
	{
		get => (string)GetValue(CancelButtonTextProperty);
		set => SetValue(CancelButtonTextProperty, value);
	}
	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not QueryYesNoCancelMessage actualMessage)
			return;
		using var dialog = new TaskDialog();
		dialog.Cancelable = true;
		dialog.Caption = Title;
		dialog.InstructionText = string.Format(CultureInfo.CurrentCulture, MessageFormat, actualMessage.MessageArguments);
		TaskDialogButton YesButton = new(nameof(YesButton), YesButtonText);
		YesButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Yes);
		dialog.Controls.Add(YesButton);
		TaskDialogButton NoButton = new(nameof(NoButton), NoButtonText);
		NoButton.Click += (s, ev) => dialog.Close(TaskDialogResult.No);
		dialog.Controls.Add(NoButton);
		TaskDialogButton CancelButton = new(nameof(CancelButton), CancelButtonText);
		CancelButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Cancel);
		dialog.Controls.Add(CancelButton);
		dialog.StartupLocation = TaskDialogStartupLocation.CenterOwner;
		dialog.OwnerWindowHandle = ((HwndSource)PresentationSource.FromVisual(AssociatedObject)).Handle;
		var result = dialog.Show();
		actualMessage.Response = result == TaskDialogResult.Yes ? true : result == TaskDialogResult.No ? false : null;
	}
}

public class QueryDoCancelAction : InteractionMessageAction<Window>
{
	public static readonly DependencyProperty MessageFormatProperty = DependencyProperty.Register(nameof(MessageFormat), typeof(string), typeof(QueryDoCancelAction));
	public static readonly DependencyProperty DoButtonTextProperty = DependencyProperty.Register(nameof(DoButtonText), typeof(string), typeof(QueryDoCancelAction));
	public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(QueryDoCancelAction));
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(QueryDoCancelAction));

	public string MessageFormat
	{
		get => (string)GetValue(MessageFormatProperty);
		set => SetValue(MessageFormatProperty, value);
	}
	public string DoButtonText
	{
		get => (string)GetValue(DoButtonTextProperty);
		set => SetValue(DoButtonTextProperty, value);
	}
	public string CancelButtonText
	{
		get => (string)GetValue(CancelButtonTextProperty);
		set => SetValue(CancelButtonTextProperty, value);
	}
	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not QueryDoCancelMessage actualMessage)
			return;
		using var dialog = new TaskDialog();
		dialog.Cancelable = true;
		dialog.Caption = Title;
		dialog.InstructionText = string.Format(CultureInfo.CurrentCulture, MessageFormat, actualMessage.MessageArguments);
		TaskDialogButton YesButton = new(nameof(YesButton), DoButtonText);
		YesButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Yes);
		dialog.Controls.Add(YesButton);
		TaskDialogButton CancelButton = new(nameof(CancelButton), CancelButtonText);
		CancelButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Cancel);
		dialog.Controls.Add(CancelButton);
		dialog.StartupLocation = TaskDialogStartupLocation.CenterOwner;
		dialog.OwnerWindowHandle = ((HwndSource)PresentationSource.FromVisual(AssociatedObject)).Handle;
		var result = dialog.Show();
		actualMessage.Response = result == TaskDialogResult.Yes;
	}
}

public abstract class EncodedFileMessageAction : InteractionMessageAction<DependencyObject>
{
	public static readonly DependencyProperty FilterDisplayNameProperty = DependencyProperty.Register(nameof(FilterDisplayName), typeof(string), typeof(EncodedFileMessageAction));
	public static readonly DependencyProperty FilterExtensionListProperty = DependencyProperty.Register(nameof(FilterExtensionList), typeof(string), typeof(EncodedFileMessageAction));

	public string FilterDisplayName
	{
		get => (string)GetValue(FilterDisplayNameProperty);
		set => SetValue(FilterDisplayNameProperty, value);
	}
	public string FilterExtensionList
	{
		get => (string)GetValue(FilterExtensionListProperty);
		set => SetValue(FilterExtensionListProperty, value);
	}

	protected abstract FileDialog CreateFileDialog();

	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not ResponsiveInteractionMessage<string?> actualMessage)
			return;
		var dialog = CreateFileDialog();
		var filterExtensionsList = FilterExtensionList;
		dialog.Filter = FilterDisplayName + "|" + filterExtensionsList;
		dialog.DefaultExt = filterExtensionsList.Split(";")[0][2..];
		actualMessage.Response = dialog.ShowDialog() == true ? dialog.FileName : null;
	}
}

public class OpenEncodedFileMessageAction : EncodedFileMessageAction
{
	protected override FileDialog CreateFileDialog() => new OpenFileDialog { CheckFileExists = true };
}

public class SaveEncodedFileMessageAction : EncodedFileMessageAction
{
	protected override FileDialog CreateFileDialog() => new SaveFileDialog
	{
		AddExtension = true,
		CreatePrompt = true,
		ValidateNames = true,
		OverwritePrompt = true
	};
}

public class ScrollIntoCurrentSyllableAction : InteractionMessageAction<LyricsViewer>
{
	protected override void InvokeAction(InteractionMessage message) => AssociatedObject.ScrollIntoCurrentSyllable();
}

public class ScrollIntoCaretAction : InteractionMessageAction<TextEditor>
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not GenericInteractionMessage<bool> actualMessage)
			return;
		if (actualMessage.Value)
			_ = AssociatedObject.Focus();
		var caretLocation = AssociatedObject.TextArea.Caret.Location;
		AssociatedObject.ScrollTo(caretLocation.Line, caretLocation.Column);
	}
}

public class HighlightLyricsAction : InteractionMessageAction<LyricsViewer>
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not GenericInteractionMessage<LyricsHighlightRequest> actualMessage)
			return;
		switch (actualMessage.Value)
		{
			case LyricsHighlightRequest.Next:
				AssociatedObject.HighlightNext(true);
				break;
			case LyricsHighlightRequest.Previous:
				AssociatedObject.HighlightNext(false);
				break;
			case LyricsHighlightRequest.NextLine:
				AssociatedObject.HighlightNextLine(true);
				break;
			case LyricsHighlightRequest.PreviousLine:
				AssociatedObject.HighlightNextLine(false);
				break;
			case LyricsHighlightRequest.First:
				AssociatedObject.HighlightFirst();
				break;
			case LyricsHighlightRequest.Last:
				AssociatedObject.HighlightLast();
				break;
		}
	}
}
