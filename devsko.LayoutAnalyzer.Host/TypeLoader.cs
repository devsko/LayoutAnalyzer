using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed partial class TypeLoader : IDisposable
    {
        private readonly TempDirectory _directory = new TempDirectory();

        public string AppDirectoryPath
            => _directory.Path;

        partial void DisposeInternal();

        public void Dispose()
        {
            DisposeInternal();
            _directory.Dispose();
        }

        private void CopyFiles(string sourceDirectory, string searchPattern, string destinationDirectory)
        {
            // Seems to be the only way to copy files that are locked (e.g. by VisualStudio)

            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = "cmd.exe",
                Arguments = $"/C copy \"{sourceDirectory}\\{searchPattern}\" \"{destinationDirectory}\"",
            };
            Process? process = Process.Start(startInfo);
            if (process is not null)
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
            }

            //foreach (string file in Directory.EnumerateFiles(sourceDirectory, searchPattern, SearchOption.TopDirectoryOnly))
            //{
            //    try
            //    {
            //        File.Copy(file, Path.Combine(destinationDirectory, file));
            //    }
            //    catch (IOException)
            //    { }
            //}
        }
    }
}
