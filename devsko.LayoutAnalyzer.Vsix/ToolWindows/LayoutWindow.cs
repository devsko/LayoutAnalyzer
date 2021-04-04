using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

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
                    try
                    {
                        Layout layout = await package.HostRunner.AnalyzeAsync(projectAssemblyPath + '|' + typeof(int).AssemblyQualifiedName);

                        content.DataContext = layout;
                        //content.DataContext = await package.HostRunner.AnalyzeAsync(projectAssemblyPath + '|' + "devsko.LayoutAnalyzer.TestProject.S1, devsko.LayoutAnalyzer.TestProject");
                    }
                    catch (Exception ex)
                    {
                        content.DataContext = null;
                        ex.ToStringDemystified();
                    }
                });
            };
        }
    }
}
