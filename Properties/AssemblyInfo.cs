using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ShiroBot.MilkyAdapter.Tests")]

// ShiroBot.SDK 0.7.0's packaging probe only recognizes the legacy plugin marker string.
[assembly: AssemblyMetadata("ShiroBot.PluginPackagingProbe", "BotPluginAttribute")]
