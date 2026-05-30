using System.Windows;
using Livet.Messaging;

namespace Lyriser.ViewModels;

public class QueryYesNoCancelMessage(string messageKey, object?[] messageArguments) : ResponsiveInteractionMessage<bool?>(messageKey)
{
	public object?[] MessageArguments { get; } = messageArguments;

	protected override Freezable CreateInstanceCore() => new QueryYesNoCancelMessage(MessageKey, MessageArguments);
}

public class QueryDoCancelMessage(string messageKey, object?[] messageArguments) : ResponsiveInteractionMessage<bool>(messageKey)
{
	public object?[] MessageArguments { get; } = messageArguments;

	protected override Freezable CreateInstanceCore() => new QueryDoCancelMessage(MessageKey, MessageArguments);
}
