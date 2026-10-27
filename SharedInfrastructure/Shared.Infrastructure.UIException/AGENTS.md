# Shared.Infrastructure.UIException

`UiException`: an exception whose message is intended for the end user (validation-like failures in use cases, errors from external services explained to the user). `MessageForUser` is shown in the UI, and the optional internal message goes to logs.

```csharp
throw new UiException("Objednávku už nelze upravit, protože byla odeslána.");
throw new UiException(messageForUser: "Platba se nezdařila.", internalMessage: $"Stripe error {code}");
```

## Suggested use

How use cases usually report results and errors:

- Signal failures by throwing, not by returning `Success = false` flags: `UiException` when the user should see the message, an app-specific exception (e.g. `EntityNotFoundException`) or `BusinessRuleException` (project `DDD.Core`) otherwise.
- An empty response record is fine when there is nothing to return.
- A boolean result is fine only when answering the question is the purpose of the use case (e.g. "does this code already exist?").

## Rules

- The UI layer catches `UiException` and shows `MessageForUser`; every other exception is shown as a generic error. Don't show messages of other exception types to users.
- Messages are user-facing: localize them and never include technical details or sensitive data.
- Derive specific exceptions from it when callers need to distinguish cases (e.g. `AresUiException`).
