using System.Windows;
using Livet.Messaging;

namespace Lyriser.ViewModels;

public class WarnUnsavedChangeMessage(string messageKey, string documentName) : ResponsiveInteractionMessage<bool?>(messageKey)
{
	public string DocumentName { get; } = documentName;

	protected override Freezable CreateInstanceCore() => new WarnUnsavedChangeMessage(MessageKey, DocumentName);
}
