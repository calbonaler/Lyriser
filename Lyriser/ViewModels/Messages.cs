using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lyriser.ViewModels;

public class QueryEncodingChangeMessage(string documentName, string newEncodingName) : RequestMessage<bool?>
{
	public string DocumentName { get; } = documentName;
	public string NewEncodingName { get; } = newEncodingName;
}

public class WarnUnsavedChangeMessage(string documentName) : RequestMessage<bool?>
{
	public string DocumentName { get; } = documentName;
}

public class WarnReopenWithEncodingMessage(string documentName, string newEncodingName) : RequestMessage<bool>
{
	public string DocumentName { get; } = documentName;
	public string NewEncodingName { get; } = newEncodingName;
}

public class GetOpeningFileMessage : RequestMessage<string?>;

public class GetSavingFileMessage : RequestMessage<string?>;

public class ScrollViewerIntoCurrentSyllableMessage;

public class ScrollEditorIntoCaretMessage(bool focus)
{
	public bool Focus { get; } = focus;
}

public class HighlightLyricsMessage(LyricsHighlightRequest request)
{
	public LyricsHighlightRequest Request { get; } = request;
}
