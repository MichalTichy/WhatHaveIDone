using System.ComponentModel;

namespace Shared.Infrastructure.UIException;

public class UiException : Exception
{
    [Localizable(true)]
    public string MessageForUser { get; }

    public UiException(string messageForUser, string internalMessage) : base(internalMessage)
    {
        MessageForUser = messageForUser;
    }

    public UiException(string message) : base(message)
    {
        MessageForUser = message;
    }
}
