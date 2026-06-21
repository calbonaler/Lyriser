using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using Livet.Behaviors.Messaging;
using Livet.Messaging;
using Lyriser.ViewModels;
using Microsoft.Win32;
using TaskDialogLite;

namespace Lyriser.Views;

public class QueryYesNoCancelAction : InteractionMessageAction<Window>
{
	public static readonly DependencyProperty MessageFormatProperty = DependencyProperty.Register(nameof(MessageFormat), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty YesButtonTextProperty = DependencyProperty.Register(nameof(YesButtonText), typeof(string), typeof(QueryYesNoCancelAction));
	public static readonly DependencyProperty NoButtonTextProperty = DependencyProperty.Register(nameof(NoButtonText), typeof(string), typeof(QueryYesNoCancelAction));
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
	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not QueryYesNoCancelMessage actualMessage)
			return;
		var yesButton = new TaskDialogButton(YesButtonText);
		var noButton = new TaskDialogButton(NoButtonText);
		var dialog = new TaskDialog
		{
			Title = Title,
			HeaderText = string.Format(CultureInfo.CurrentCulture, MessageFormat, actualMessage.MessageArguments),
			Buttons = [yesButton, noButton, TaskDialogButton.Cancel],
		};
		var result = dialog.Show(((HwndSource)PresentationSource.FromVisual(AssociatedObject)).Handle);
		actualMessage.Response = result == yesButton ? true : result == noButton ? false : null;
	}
}

public class QueryDoCancelAction : InteractionMessageAction<Window>
{
	public static readonly DependencyProperty MessageFormatProperty = DependencyProperty.Register(nameof(MessageFormat), typeof(string), typeof(QueryDoCancelAction));
	public static readonly DependencyProperty DoButtonTextProperty = DependencyProperty.Register(nameof(DoButtonText), typeof(string), typeof(QueryDoCancelAction));
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
	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not QueryDoCancelMessage actualMessage)
			return;
		var dialog = new TaskDialog
		{
			Title = Title,
			HeaderText = string.Format(CultureInfo.CurrentCulture, MessageFormat, actualMessage.MessageArguments),
			Buttons = [new(DoButtonText), TaskDialogButton.Cancel],
		};
		actualMessage.Response = dialog.Show(((HwndSource)PresentationSource.FromVisual(AssociatedObject)).Handle) != TaskDialogButton.Cancel;
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
	protected override FileDialog CreateFileDialog() => new OpenFileDialog();
}

public class SaveEncodedFileMessageAction : EncodedFileMessageAction
{
	protected override FileDialog CreateFileDialog() => new SaveFileDialog();
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
