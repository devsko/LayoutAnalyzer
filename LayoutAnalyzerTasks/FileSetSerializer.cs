// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace LayoutAnalyzerTasks
{
    public class FileSetSerializer : Task
    {
        public ITaskItem[] WatchFiles { get; set; }
        public ITaskItem[] TargetFrameworks { get; set; }
        public ITaskItem OutputPath { get; set; }

        public override bool Execute()
        {
            int tfwCount = TargetFrameworks.Length;
            MSBuildFileSetResult fileSetResult = new()
            {
                Projects = WatchFiles
                    .GroupBy(item => item.GetMetadata("ProjectFullPath"))
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .GroupBy(item => item.GetMetadata("FullPath"))
                            .Select(group => new ProjectFiles
                            {
                                FilePath = group.Key,
                                TargetFrameworks =
                                    /*group.Count() == tfwCount
                                    ? ""
                                    :*/ string.Join(";", group.Select(group => group.GetMetadata("TFW")))
                            })
                            .ToList()),
                TargetFrameworks = TargetFrameworks
                    .Select(item => new TargetFrameworkItem()
                    {
                        Name = item.ItemSpec,
                        Identifier = item.GetMetadata("Identifier"),
                        Version = item.GetMetadata("Version").Substring(1),
                        AssemblyPath = item.GetMetadata("Path"),
                    })
                    .ToList(),
            };

            using FileStream fileStream = File.Create(OutputPath.ItemSpec);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(fileSetResult);
            fileStream.Write(json, 0, json.Length);

            return !Log.HasLoggedErrors;
        }
    }
}
