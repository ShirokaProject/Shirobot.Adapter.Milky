using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using ShiroBot.SDK.Core;
using ShiroBot.Model.QQ;

if (args.Length != 1) throw new ArgumentException("Provide the release ZIP path.");
var directory = Path.Combine(Path.GetTempPath(), "ShiroBot.MilkyPackageProbe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    ZipFile.ExtractToDirectory(Path.GetFullPath(args[0]), directory);
    var context = new AssemblyLoadContext("packaged-milky", isCollectible: true);
    context.Resolving += (_, name) =>
    {
        if (name.Name == typeof(IBotAdapter).Assembly.GetName().Name) return typeof(IBotAdapter).Assembly;
        if (name.Name == typeof(IQGroupApi).Assembly.GetName().Name) return typeof(IQGroupApi).Assembly;
        var path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    };
    try
    {
        var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "ShiroBot.Adapter.Milky.dll"));
        var types = assembly.GetTypes(); // Fails when protocol DTO dependencies are missing.
        var type = types.Single(type => typeof(IBotAdapter).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface);
        var adapter = (IBotAdapter)Activator.CreateInstance(type)!;
        if (adapter.Platform != "qq") throw new InvalidOperationException("Unexpected adapter platform.");
        if (context.LoadFromAssemblyName(new AssemblyName("ShiroBot.Adapter.Milky.Model")).GetTypes().Length == 0)
            throw new InvalidOperationException("Empty private protocol model.");
        using var archive = ZipFile.OpenRead(Path.GetFullPath(args[0]));
        var names = archive.Entries.Select(entry => entry.FullName).Order().ToArray();
        if (!names.SequenceEqual(new[] { "ShiroBot.Adapter.Milky.Model.dll", "ShiroBot.Adapter.Milky.dll" }.Order()))
            throw new InvalidOperationException("ZIP must contain exactly the adapter and private model DLLs, without symbols or host contracts.");
        Console.WriteLine("Packaged Milky adapter and private model loaded successfully in isolation.");
    }
    finally { context.Unload(); }
}
finally { Directory.Delete(directory, recursive: true); }
