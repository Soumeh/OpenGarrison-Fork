using System.Reflection;
using System.Runtime.Loader;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal static class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> LoadFromDirectory(
        string pluginsDirectory,
        Func<IOpenGarrisonServerPlugin, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        Directory.CreateDirectory(pluginsDirectory);
        var assemblies = new List<Assembly>();
        foreach (var pluginPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(pluginPath)));
            }
            catch (Exception ex)
            {
                log($"[plugin] failed to load assembly \"{pluginPath}\": {ex.Message}");
            }
        }

        return LoadFromAssemblies(assemblies, contextFactory, log);
    }

    public static IReadOnlyList<LoadedPlugin> LoadFromAssemblies(
        IEnumerable<Assembly> assemblies,
        Func<IOpenGarrisonServerPlugin, IOpenGarrisonServerPluginContext> contextFactory,
        Action<string> log)
    {
        var loadedPlugins = new List<LoadedPlugin>();
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types
                         .Where(type => typeof(IOpenGarrisonServerPlugin).IsAssignableFrom(type)
                             && type is { IsAbstract: false, IsInterface: false }))
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IOpenGarrisonServerPlugin plugin)
                    {
                        continue;
                    }

                    var context = contextFactory(plugin);
                    plugin.Initialize(context);
                    loadedPlugins.Add(new LoadedPlugin(plugin, context));
                }
                catch (Exception ex)
                {
                    log($"[plugin] failed to initialize \"{type.FullName}\": {ex.Message}");
                }
            }
        }

        return loadedPlugins;
    }

    internal sealed record LoadedPlugin(IOpenGarrisonServerPlugin Plugin, IOpenGarrisonServerPluginContext Context);
}
