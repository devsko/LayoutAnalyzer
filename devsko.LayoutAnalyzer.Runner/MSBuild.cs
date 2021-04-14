using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using devsko.LayoutAnalyzer.Runner;
using LayoutAnalyzerTasks;

namespace devsko.LayoutAnalyzer.Runner
{
    public class MSBuild
    {
        public static async Task<ProjectItem> GenerateWatchListAsync(string projectFilePath, CancellationToken cancellationToken)
        {
            string targetsPath = Path.Combine(Path.GetDirectoryName(typeof(MSBuild).Assembly.Location)!, "LayoutAnalyzer.targets");
            string watchListPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                int exitCode;
                string output;
                using (ProcessState state = new(
                    Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles")!, "dotnet", "dotnet.exe"),
                    $"msbuild /nologo \"{projectFilePath}\" /p:_DotNetWatchListFile=\"{watchListPath}\" /v:n /t:GenerateWatchList /p:DotNetWatchBuild=true /p:DesignTimeBuild=true /p:CustomAfterMicrosoftCommonTargets=\"{targetsPath}\" /p:CustomAfterMicrosoftCommonCrossTargetingTargets=\"{targetsPath}\" /p:_DotNetWatchTraceOutput=false",
                    Path.GetDirectoryName(projectFilePath)!))
                {
                    (exitCode, output) = await state.StartAndWaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }

                if (exitCode == 0 && File.Exists(watchListPath))
                {
                    using var watchList = File.OpenRead(watchListPath);
                    MSBuildFileSetResult? result = await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(watchList, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result is not null && result.TargetFrameworks is not null && result.Projects is not null)
                    {
                        TFWItem[] tfwItems = result.TargetFrameworks
                            .Select(tfw => new TFWItem()
                            {
                                Name = tfw.Name!,
                                Target = tfw.Identifier switch
                                {
                                    ".NETFramework" => TargetFramework.NetFramework,
                                    ".NETCoreApp" when new Version(tfw.Version ?? "") >= new Version(5, 0) => TargetFramework.Net5,
                                    ".NETCoreApp" => TargetFramework.NetCore,
                                    ".NETStandard" => TargetFramework.NetStandard,
                                    _ => (TargetFramework)0
                                },
                                Version = new Version(tfw.Version ?? ""),
                                AssemblyPath = tfw.AssemblyPath!,
                            })
                            .ToArray();

                        BitVector32 allTfws = new((int)Math.Pow(2, tfwItems.Length) - 1);

                        FileItem[] fileItems = result.Projects
                            .SelectMany(pair =>
                                pair.Value
                                    .Select(file => new FileItem
                                    {
                                        FilePath = file.FilePath,
                                        ProjectPath = pair.Key,
                                        TargetFrameworkFlags = Convert(file.TargetFrameworks)
                                    }))
                            .ToArray();

                        BitVector32 Convert(string? targetFrameworks)
                        {
                            if (targetFrameworks is null || targetFrameworks.Length == 0)
                            {
                                return allTfws;
                            }

                            BitVector32 result = default;
                            int i = 1; // the indexer of BitVector32 is 1 based!
                            foreach (TFWItem item in tfwItems)
                            {
                                if (targetFrameworks.IndexOf(item.Name + ';', StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    result[i] = true;
                                }
                                i++;
                            }
                            return result;
                        }

                        return new ProjectItem
                        {
                            ProjectFilePath = projectFilePath,
                            FileItems = fileItems,
                            TfwItems = tfwItems,
                        };
                    }
                }

                return default;
            }
            finally
            {
                try
                {
                    File.Delete(watchListPath);
                }
                catch
                { }
            }
        }

        public
#if NETCOREAPP3_1_OR_GREATER
        readonly
#endif
        struct ProjectItem
        {
            public string? ProjectFilePath
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
            }
            public FileItem[] FileItems
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
            }
            public TFWItem[] TfwItems
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
            }
        }

        public
#if NETCOREAPP3_1_OR_GREATER
        readonly
#endif
        struct FileItem
        {
            public string? FilePath
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
            public string ProjectPath
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
            public BitVector32 TargetFrameworkFlags
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
        }

        public
#if NETCOREAPP3_1_OR_GREATER
        readonly
#endif
        struct TFWItem
        {
            public string Name
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
            public TargetFramework Target 
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
            public Version Version
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
            public string AssemblyPath
            {
                get;
#if NETCOREAPP3_1_OR_GREATER
            init;
#else
                set;
#endif
            }
        }

    }
}
