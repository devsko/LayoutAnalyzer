using System.Diagnostics;
using System.IO;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class AppDirectory : TempDirectory
    {
        public void CopyFiles(string directory, string searchPattern = "*.*")
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }
            // Seems to be the only way to copy files that are locked (e.g. by VisualStudio)
            ExecuteCmd($"copy \"{directory}\\{searchPattern}\" \"{Path}\"");
        }

        protected override void DeleteDirectory()
        {
            ExecuteCmd($"rd \"{Path}\" /S/Q");
        }

        private static void ExecuteCmd(string arguments)
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                FileName = "cmd.exe",
                Arguments = "/C " + arguments,
            };
            Process? process = Process.Start(startInfo);
            if (process is not null)
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
            }
        }
    }
}
