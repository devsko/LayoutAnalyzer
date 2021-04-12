using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    [Guid("ccb3a83c-8506-4de0-a783-10982481ca50")]
    public class LayoutWindow : ToolWindowPane
    {
        public const string Title = "Layout";

        private CancellationTokenSource _cancel;

        public LayoutWindow(LayoutAnalyzerPackage package)
        {
            Caption = Title;
            LayoutControl content = new(package);
            Content = content;

            string frameworkDirectory = package.HostRunner.TargetFramework switch
            {
                TargetFramework.NetFramework => "net472",
                TargetFramework.NetCore => "netcoreapp3.1",
                TargetFramework.Net5Plus => "net5.0",
                _ => throw new ArgumentException("")
            };
            string projectAssemblyPath = $@"C:\Users\stefa\source\repos\LayoutAnalyzer\devsko.LayoutAnalyzer.TestProject\bin\{(package.HostRunner.IsDebug ? "Debug" : "Release")}\{frameworkDirectory}\devsko.LayoutAnalyzer.TestProject.dll";

            content.Unloaded += (sender, args) =>
                _cancel.Cancel();

            content.Loaded += (sender, args) =>
            {
                _cancel = CancellationTokenSource.CreateLinkedTokenSource(VsShellUtilities.ShutdownToken);
                content.DataContext = null;

                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    try
                    {
                        await AnalyzeAsync("System.IO.Pipelines.Pipe, System.IO.Pipelines", TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.Zero).ConfigureAwait(false);
                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.Zero).ConfigureAwait(false);
                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.FromSeconds(65)).ConfigureAwait(false);

                        await AnalyzeAsync("devsko.LayoutAnalyzer.TestProject.Explicit, devsko.LayoutAnalyzer.TestProject", TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                        await AnalyzeAsync("x,y", TimeSpan.Zero).ConfigureAwait(false);

                        //await AnalyzeAsync("devsko.LayoutAnalyzer.TestProject.S1, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    { }

                    async Task AnalyzeAsync(string typeName, TimeSpan delayAfter)
                    {
                        try
                        {
                            Layout layout = await package.HostRunner.AnalyzeAsync(projectAssemblyPath + '|' + typeName, _cancel.Token).ConfigureAwait(false);
                            if (layout is not null)
                            {
                                await package.OutWriter.WriteLineAsync($"Layout from HOST ({package.HostRunner.Id}) took {layout.ElapsedTime}").ConfigureAwait(false);
                            }

                            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
                            content.DataContext = layout;

                            await Task.Delay(delayAfter, _cancel.Token).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            await package.OutWriter.WriteLineAsync("Analysis canceled");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            content.DataContext = null;
                            await package.OutWriter.WriteLineAsync("Unexpected error: " + ex.ToStringDemystified()).ConfigureAwait(false);
                        }
                    }
                });
            };
        }
    }
}
