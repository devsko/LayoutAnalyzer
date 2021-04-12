using System.Collections.Generic;

namespace LayoutAnalyzerTasks
{
    public class MSBuildFileSetResult
    {
        public Dictionary<string, List<ProjectFiles>>? Projects { get; set; }
        public List<TargetFrameworkItem>? TargetFrameworks { get; set; }
    }

    public class ProjectFiles
    {
        public string? FilePath { get; set; }
        public string? TargetFrameworks { get; set; }
    }

    public class TargetFrameworkItem
    {
        public string? Name { get; set; }
        public string? Identifier { get; set; }
        public string? Version { get; set; }
        public string? AssemblyPath { get; set; }
    }
}
