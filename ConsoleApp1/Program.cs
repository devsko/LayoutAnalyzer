using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {

            string projectFile = @"C:\Users\stefa\source\repos\LayoutAnalyzer\ConsoleApp2\ConsoleApp2.csproj";
            Environment.CurrentDirectory = Path.GetDirectoryName(projectFile)!;
            IReporter reporter = new PrefixConsoleReporter("watch: ", PhysicalConsole.Singleton, true, false);
            MsBuildFileSetFactory fileSetFactory = new(reporter, DotNetWatchOptions.Default, projectFile, false, true);

            var fileSet = await fileSetFactory.CreateAsync(default).ConfigureAwait(false);
            fileSet.Project.ToString();

            ProcessSpec processSpec = new()
            {
                Executable = @"C:\Program Files\dotnet\dotnet.exe",
                WorkingDirectory = Path.GetDirectoryName(projectFile),
                Arguments = new[] { "run" },
            };

            DotNetWatchContext context = new()
            {
                ProcessSpec = processSpec,
                Reporter = reporter,
                DefaultLaunchSettingsProfile = new LaunchSettingsProfile
                {
                    
                },
            };

               await using HotReloadDotNetWatcher watcher = new(reporter, fileSetFactory, DotNetWatchOptions.Default, PhysicalConsole.Singleton);
            await watcher.WatchAsync(context, default).ConfigureAwait(false);
        }
    }
}
