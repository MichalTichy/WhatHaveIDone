using System.Reflection;

namespace Shared.Infrastructure.IoC;

public record FullAssemblyLoader
{
    private readonly Action<string?> forceLoadAction;

    public static FullAssemblyLoader Default { get; } = new(startsWith =>
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var loadedPaths = loadedAssemblies.Select(a => a.Location).ToArray();

        string[] referencedPaths;
        if (string.IsNullOrWhiteSpace(startsWith))
        {
            referencedPaths = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");
        }
        else
        {
            referencedPaths = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"{startsWith}*.dll");
        }

        var toLoad = referencedPaths.Where(r => !loadedPaths.Contains(r, StringComparer.InvariantCultureIgnoreCase)).ToList();

        toLoad.ForEach(path => loadedAssemblies.Add(AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(path))));
    });

    public static FullAssemblyLoader ReferencedAssemblyLoader { get; } = new(startsWith =>
    {
        var loadedAssemblies = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Where(t => t.FullName != null).Select(assembly => assembly.FullName!));
        var assembliesToCheck = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(t => t.FullName != null).Where(assembly => ShouldInclude(assembly.FullName!)));

        while (assembliesToCheck.Any())
        {
            var assemblyToCheck = assembliesToCheck.Dequeue();

            foreach (var reference in assemblyToCheck.GetReferencedAssemblies())
            {
                if (!loadedAssemblies.Contains(reference.FullName) && ShouldInclude(reference.FullName))
                {
                    var assembly = AppDomain.CurrentDomain.Load(reference);
                    assembliesToCheck.Enqueue(assembly);
                    loadedAssemblies.Add(reference.FullName);
                }
            }
        }

        bool ShouldInclude(string assemblyFullName)
        {
            return startsWith is null || assemblyFullName.StartsWith(startsWith);
        }
    });

    private FullAssemblyLoader(Action<string?> forceLoadAction)
    {
        this.forceLoadAction = forceLoadAction;
    }

    public void Load(string? startsWith)
    {
        forceLoadAction(startsWith);
    }
}
