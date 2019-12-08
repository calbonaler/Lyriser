using System.Windows;
using Livet.Messaging;

namespace Lyriser.ViewModels
{
	public class WarnUnsavedChangeMessage : ResponsiveInteractionMessage<bool?>
	{
		public WarnUnsavedChangeMessage(string messageKey, string documentName) : base(messageKey) => DocumentName = documentName;

		public string DocumentName { get; }

		protected override Freezable CreateInstanceCore() => new WarnUnsavedChangeMessage(MessageKey, DocumentName);
	}
}
