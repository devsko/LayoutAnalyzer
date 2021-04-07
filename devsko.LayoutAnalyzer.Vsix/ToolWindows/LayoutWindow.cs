using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace devsko.LayoutAnalyzer
{
    [Guid("ccb3a83c-8506-4de0-a783-10982481ca50")]
    public class LayoutWindow : ToolWindowPane
    {
        public const string Title = "Layout";

        public LayoutWindow(LayoutAnalyzerPackage package)
        {
            Caption = Title;
            LayoutControl content = new(package);
            Content = content;

            string frameworkDirectory = package.HostRunner.TargetFramework switch
            {
                TargetFramework.NetFramework => "net472",
                TargetFramework.NetCore => "netcoreapp3.1",
                TargetFramework.Net => "net5.0",
                _ => throw new ArgumentException("")
            };
            string projectAssemblyPath = $@"C:\Users\stefa\source\repos\LayoutAnalyzer\devsko.LayoutAnalyzer.TestProject\bin\{(package.HostRunner.IsDebug ? "Debug" : "Release")}\{frameworkDirectory}\devsko.LayoutAnalyzer.TestProject.dll";

            content.Loaded += (sender, args) =>
            {
                package.JoinableTaskFactory.RunAsync(async () =>
                {
                    content.DataContext = null;
                    try
                    {
                        await AnalyzeAsync("System.IO.Pipelines.Pipe, System.IO.Pipelines", TimeSpan.FromSeconds(5));

                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.Zero);
                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.Zero);
                        await AnalyzeAsync(typeof(int).AssemblyQualifiedName, TimeSpan.FromSeconds(65));

                        await AnalyzeAsync("devsko.LayoutAnalyzer.TestProject.Explicit, devsko.LayoutAnalyzer.TestProject", TimeSpan.FromSeconds(5));

                        await AnalyzeAsync("x,y", TimeSpan.Zero);

                        //await AnalyzeAsync("devsko.LayoutAnalyzer.TestProject.S1, devsko.LayoutAnalyzer.TestProject");
                    }
                    catch (TaskCanceledException)
                    { }

                    async Task AnalyzeAsync(string typeName, TimeSpan delayAfter)
                    {
                        try
                        {
                            Layout layout = await package.HostRunner.AnalyzeAsync(projectAssemblyPath + '|' + typeName, VsShellUtilities.ShutdownToken);
                            if (layout is not null)
                            {
                                await (await package.GetOutAsync()).WriteLineAsync($"Layout from HOST ({package.HostRunner.Id}) took {layout.ElapsedTime}");
                            }
                            content.DataContext = layout;

                            await Task.Delay(delayAfter, VsShellUtilities.ShutdownToken);
                        }
                        catch (TaskCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            content.DataContext = null;
                            await (await package.GetOutAsync()).WriteLineAsync("Unexpected error: " + ex.ToStringDemystified());
                        }
                    }
                });
            };
        }
    }
}
