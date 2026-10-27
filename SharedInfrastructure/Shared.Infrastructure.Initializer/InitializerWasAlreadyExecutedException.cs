using Shared.Infrastructure.Initializer.Texts;

namespace Shared.Infrastructure.Initializer;

public class InitializerWasAlreadyExecutedException : InitializerException
{
    public InitializerWasAlreadyExecutedException() : base(ExceptionTexts.InitializerAlreadyExecuted)
    {
    }
}
