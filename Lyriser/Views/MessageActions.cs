using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using Livet.Behaviors.Messaging;
using Livet.Messaging;
using Lyriser.Models;
using Lyriser.ViewModels;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs.Controls;

namespace Lyriser.Views;

public class WarnUnsavedChangeAction : InteractionMessageAction<Window>
{
	public static readonly DependencyProperty MessageFormatProperty = DependencyProperty.Register(nameof(MessageFormat), typeof(string), typeof(WarnUnsavedChangeAction));
	public static readonly DependencyProperty SaveButtonTextProperty = DependencyProperty.Register(nameof(SaveButtonText), typeof(string), typeof(WarnUnsavedChangeAction));
	public static readonly DependencyProperty DoNotSaveButtonTextProperty = DependencyProperty.Register(nameof(DoNotSaveButtonText), typeof(string), typeof(WarnUnsavedChangeAction));
	public static readonly DependencyProperty CancelButtonTextProperty = DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(WarnUnsavedChangeAction));
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(WarnUnsavedChangeAction));

	public string MessageFormat
	{
		get => (string)GetValue(MessageFormatProperty);
		set => SetValue(MessageFormatProperty, value);
	}
	public string SaveButtonText
	{
		get => (string)GetValue(SaveButtonTextProperty);
		set => SetValue(SaveButtonTextProperty, value);
	}
	public string DoNotSaveButtonText
	{
		get => (string)GetValue(DoNotSaveButtonTextProperty);
		set => SetValue(DoNotSaveButtonTextProperty, value);
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
		if (message is not WarnUnsavedChangeMessage actualMessage)
			return;
		using var dialog = new TaskDialog();
		dialog.Cancelable = true;
		dialog.Caption = Title;
		dialog.InstructionText = string.Format(MessageFormat, actualMessage.DocumentName);
		TaskDialogButton SaveButton = new TaskDialogButton(nameof(SaveButton), SaveButtonText);
		SaveButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Yes);
		dialog.Controls.Add(SaveButton);
		TaskDialogButton DontSaveButton = new TaskDialogButton(nameof(DontSaveButton), DoNotSaveButtonText);
		DontSaveButton.Click += (s, ev) => dialog.Close(TaskDialogResult.No);
		dialog.Controls.Add(DontSaveButton);
		TaskDialogButton CancelButton = new TaskDialogButton(nameof(CancelButton), CancelButtonText);
		CancelButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Cancel);
		dialog.Controls.Add(CancelButton);
		dialog.StartupLocation = TaskDialogStartupLocation.CenterOwner;
		dialog.OwnerWindowHandle = ((HwndSource)PresentationSource.FromVisual(AssociatedObject)).Handle;
		var result = dialog.Show();
		actualMessage.Response = result == TaskDialogResult.Yes ? true : result == TaskDialogResult.No ? false : null;
	}
}

public abstract class EncodedFileMessageAction : InteractionMessageAction<DependencyObject>
{
	public static readonly DependencyProperty FilterDisplayNameProperty = DependencyProperty.Register(nameof(FilterDisplayName), typeof(string), typeof(EncodedFileMessageAction));
	public static readonly DependencyProperty FilterExtensionListProperty = DependencyProperty.Register(nameof(FilterExtensionList), typeof(string), typeof(EncodedFileMessageAction));
	public static readonly DependencyProperty EncodingLabelTextProperty = DependencyProperty.Register(nameof(EncodingLabelText), typeof(string), typeof(EncodedFileMessageAction));

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
	public string EncodingLabelText
	{
		get => (string)GetValue(EncodingLabelTextProperty);
		set => SetValue(EncodingLabelTextProperty, value);
	}

	protected void SetFilterToDialog(CommonFileDialog dialog)
	{
		dialog.Filters.Add(new CommonFileDialogFilter(FilterDisplayName, FilterExtensionList));
		dialog.DefaultExtension = dialog.Filters[0].Extensions[0];
	}

	protected Func<FileEncoding> AddEncodingSelectorToDialog(CommonFileDialog dialog, bool addAutoDetect)
	{
		var encodingComboBox = new CommonFileDialogComboBox();
		var encodings = new List<(string DisplayName, FileEncoding Encoding)>()
		{
			("UTF-8",       FileEncoding.UTF8),
			("UTF-8 (BOM)", FileEncoding.UTF8WithBom),
			("ANSI",        FileEncoding.Ansi),
		};
		if (addAutoDetect)
			encodings.Insert(0, ("自動検出", FileEncoding.AutoDetect));
		foreach (var (displayName, _) in encodings)
			encodingComboBox.Items.Add(new CommonFileDialogComboBoxItem(displayName));
		encodingComboBox.SelectedIndex = 0;
		var group = new CommonFileDialogGroupBox(EncodingLabelText);
		group.Items.Add(encodingComboBox);
		group.IsProminent = true;
		dialog.Controls.Add(group);
		return () => encodings[encodingComboBox.SelectedIndex].Encoding;
	}
}

public class OpenEncodedFileMessageAction : EncodedFileMessageAction
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not ResponsiveInteractionMessage<EncodedFileInfo?> actualMessage)
			return;
		using var dialog = new CommonOpenFileDialog();
		dialog.EnsureFileExists = true;
		dialog.EnsurePathExists = true;
		SetFilterToDialog(dialog);
		var selector = AddEncodingSelectorToDialog(dialog, true);
		actualMessage.Response = dialog.ShowDialog() == CommonFileDialogResult.Ok ? new EncodedFileInfo(dialog.FileName, selector()) : null;
	}
}

public class SaveEncodedFileMessageAction : EncodedFileMessageAction
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not ResponsiveInteractionMessage<EncodedFileInfo?> actualMessage)
			return;
		using var dialog = new CommonSaveFileDialog();
		dialog.AlwaysAppendDefaultExtension = true;
		dialog.CreatePrompt = true;
		dialog.EnsurePathExists = true;
		dialog.EnsureValidNames = true;
		dialog.OverwritePrompt = true;
		SetFilterToDialog(dialog);
		var selector = AddEncodingSelectorToDialog(dialog, false);
		actualMessage.Response = dialog.ShowDialog() == CommonFileDialogResult.Ok ? new EncodedFileInfo(dialog.FileName, selector()) : null;
	}
}

public class FocusSyllableAction : InteractionMessageAction<LyricsViewer>
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not GenericInteractionMessage<SyncFocusArgument> actualMessage)
			return;
		var arg = actualMessage.Value;
		if (arg.Force)
			AssociatedObject.Focus();
		else if (AssociatedObject.IsFocused || AssociatedObject.IsKeyboardFocused || AssociatedObject.IsKeyboardFocusWithin)
			return;
		AssociatedObject.CurrentSyllable = new SyllableLocation(arg.Line, arg.Column);
		AssociatedObject.ScrollIntoCurrentSyllable();
	}
}

public class FocusCharacterAction : InteractionMessageAction<TextEditor>
{
	protected override void InvokeAction(InteractionMessage message)
	{
		if (message is not GenericInteractionMessage<SyncFocusArgument> actualMessage)
			return;
		var arg = actualMessage.Value;
		if (arg.Force)
			AssociatedObject.Focus();
		else if (AssociatedObject.IsFocused || AssociatedObject.IsKeyboardFocused || AssociatedObject.IsKeyboardFocusWithin)
			return;
		AssociatedObject.TextArea.Caret.Location = new TextLocation(arg.Line, arg.Column >= 1 ? arg.Column : 1);
		AssociatedObject.ScrollTo(arg.Line, arg.Column);
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
