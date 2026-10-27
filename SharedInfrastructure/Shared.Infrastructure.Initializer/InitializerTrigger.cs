namespace Shared.Infrastructure.Initializer;

//The number of the enum values is used for sorting.
//The lower the number, the earlier the initializer is executed.
//This sort precess sort by Priority.
public enum InitializerTrigger
{
    OnStartup = 0,
    OnApplicationReady = 1
}
