using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace devsko.LayoutAnalyzer.Runner
{
    public class HotReload : IDisposable
    {
        private readonly Pipe _pipe;
        private readonly BinaryWriter? _writer;
        private readonly string _projectFilePath;
        private readonly CancellationTokenSource _cancelInitialization;
        private readonly Task _initializationTask;
        private Dictionary<string, Project>? _projects;
        private HotReloadService? _hotReloadService;
        private FileWatcher? _projectFilesWatcher;

        public MSBuildWorkspace? Workspace { get; private set; }
        public string? ProjectName { get; private set; }
        public Solution? Solution { get; private set; }

        public HotReload(Pipe pipe, string projectFilePath, string flavor)
        {
            _pipe = pipe;
            _writer = new BinaryWriter(_pipe.Stream, Encoding.UTF8, leaveOpen: true);
            _projectFilePath = projectFilePath;
            _cancelInitialization = new CancellationTokenSource();
            _initializationTask = InitializeAsync(_cancelInitialization.Token);

            async Task InitializeAsync(CancellationToken cancellationToken)
            {
                // It is crucial that the workspace is created with the CASE SENSITIVE path. Otherwise
                // the PDBs will not match the source files and hot reload wil not work.

                projectFilePath = Helper.GetCaseSensitivePath(projectFilePath);

                Log.WriteLine($"HOT RELOAD Starting initialization for project {projectFilePath}");

                Workspace = MSBuildWorkspace.Create();
                Project root = await Workspace.OpenProjectAsync(Path.GetFullPath(projectFilePath), cancellationToken: cancellationToken).ConfigureAwait(false);
                Solution = Workspace.CurrentSolution;

                _hotReloadService = new HotReloadService(Workspace.Services);
                await _hotReloadService.StartSessionAsync(Workspace.CurrentSolution, cancellationToken).ConfigureAwait(false);

                CreateProjectsMap();

                Project? project = GetProject(flavor);
                if (project is not null)
                {
                    _projectFilesWatcher = new FileWatcher(GetAllSourceFilePaths(project));
                }

                Log.WriteLine($"HOT RELOAD Completed initialization for project {projectFilePath}");

                void CreateProjectsMap()
                {
                    _projects = new Dictionary<string, Project>();
                    foreach (Project project in root.Solution.Projects)
                    {
                        if (project == root || project.FilePath == projectFilePath)
                        {
                            string projectName = project.Name;
                            if (projectName[^1] == ')')
                            {
                                int pos = projectName.LastIndexOf('(');
                                if (pos >= 0)
                                {
                                    if (_projects.Count == 0)
                                    {
                                        ProjectName = projectName[..pos];
                                    }
                                    _projects.Add(projectName[(pos + 1)..^1], project);
                                }
                            }
                        }
                    }
                    if (ProjectName is null)
                    {
                        ProjectName = root.Name;
                        _projects.Add(string.Empty, root);
                    }
                }
            }
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_initializationTask.IsCompleted)
            {
                await _initializationTask.ConfigureAwait(false);
            }
            else
            {
                using (cancellationToken.UnsafeRegister((state) => ((CancellationTokenSource)state!).Cancel(), _cancelInitialization))
                {
                    await _initializationTask.ConfigureAwait(false);
                }
            }
        }

        public ICollection<string>? Flavors
            => _projects?.Keys;

        public Project? GetProject(string flavor = "")
        {
            Project? project = null;
            _projects?.TryGetValue(flavor, out project);

            return project;
        }

        public async Task LoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.WriteLine($"HOT RELOAD Initialization failed {ex.ToStringDemystified()}");
            }

            try
            {
                Log.WriteLine("HOT RELOAD Entering loop");

                Debug.Assert(_projectFilesWatcher is not null);
                while (true)
                {
                    var changed = await _projectFilesWatcher.GetChangedFileAsync(cancellationToken).ConfigureAwait(false);
                    if (changed is null)
                    {
                        Log.WriteLine("HOT RELOAD Leaving loop");
                        break;
                    }

                    Log.WriteLine($"HOT RELOAD File changed {changed}");

                    bool handled = await HandleFileChangeAsync(changed, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine($"HOT RELOAD Loop failed {ex.ToStringDemystified()}");
            }
        }

        private async ValueTask<bool> HandleFileChangeAsync(string path, CancellationToken cancellationToken)
        {
            Debug.Assert(Workspace is not null);
            Debug.Assert(_hotReloadService is not null);

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Solution? updatedSolution = null;
            ProjectId updatedProjectId;
            if (Workspace.CurrentSolution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)) is Document documentToUpdate)
            {
                var sourceText = await GetSourceTextAsync(path, cancellationToken).ConfigureAwait(false);
                updatedSolution = documentToUpdate.WithText(sourceText).Project.Solution;
                updatedProjectId = documentToUpdate.Project.Id;
            }
            else if (Workspace.CurrentSolution.Projects.SelectMany(p => p.AdditionalDocuments).FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)) is AdditionalDocument additionalDocument)
            {
                var sourceText = await GetSourceTextAsync(path, cancellationToken).ConfigureAwait(false);
                updatedSolution = Workspace.CurrentSolution.WithAdditionalDocumentText(additionalDocument.Id, sourceText, PreservationMode.PreserveValue);
                updatedProjectId = additionalDocument.Project.Id;
            }
            else
            {
                return false;
            }

            (ImmutableArray<Update> updates, ImmutableArray<Diagnostic> diagnostics) = await _hotReloadService.EmitSolutionUpdateAsync(updatedSolution, cancellationToken).ConfigureAwait(false);

            if (updates.IsDefault && diagnostics.IsDefault)
            {
                // It's possible that there are compilation errors which prevented the solution update
                // from being updated. Let's look to see if there are compilation errors.
                var compileDiagnostics = GetDiagnostics(updatedSolution, cancellationToken);
                if (compileDiagnostics.IsDefaultOrEmpty)
                {
                    //await _deltaApplier.Apply(context, file.FilePath, updates, cancellationToken);
                }
                else
                {
                    foreach (string diagnostic in compileDiagnostics)
                    {
                        Log.WriteLine($"HOT RELOAD {diagnostic}");
                    }
                }

                // Even if there were diagnostics, continue treating this as a success
                return true;
            }

            if (!diagnostics.IsDefaultOrEmpty)
            {
                // Rude edit.
                foreach (var diagnostic in diagnostics)
                {
                    Log.WriteLine($"HOT RELOAD rude edit {CSharpDiagnosticFormatter.Instance.Format(diagnostic)}");
                }

                return false;
            }

            Solution = updatedSolution;

            return await ApplyChangesAsync(path, updates, cancellationToken).ConfigureAwait(false);

            static async ValueTask<SourceText> GetSourceTextAsync(string path, CancellationToken cancellationToken)
            {
                for (var attemptIndex = 0; attemptIndex < 6; attemptIndex++)
                {
                    try
                    {
                        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                        return SourceText.From(stream, Encoding.UTF8);
                    }
                    catch (IOException) when (attemptIndex < 5)
                    {
                        await Task.Delay(20 * (attemptIndex + 1), cancellationToken).ConfigureAwait(false);
                    }
                }

                Debug.Fail("This shouldn't happen.");
                return null;
            }
        }

        private async ValueTask<bool> ApplyChangesAsync(string path, ImmutableArray<Update> updates, CancellationToken cancellationToken)
        {
            Debug.Assert(_writer is not null);

            _writer.Write(_projectFilePath);
            _writer.Write(path);
            _writer.Write(updates.Length);
            for (int i = 0; i < updates.Length; i++)
            {
                Update update = updates[i];
                _writer.Write(update.ModuleId.ToString());
                await WriteArrayAsync(_writer, update.MetadataDelta.ToArray(), cancellationToken).ConfigureAwait(false);
                await WriteArrayAsync(_writer, update.ILDelta.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            await _pipe.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            byte[] response = new byte[1];
#if DEBUG
            TimeSpan timeout = Timeout.InfiniteTimeSpan;
#else
            TimeSpan timeout = TimeSpan.FromSeconds(5);
#endif
            CancellationTokenSource cts = new(timeout);
            int length = await _pipe.Stream.ReadAsync(response, cancellationToken).ConfigureAwait(false);
            if (length == 1)
            {
                return response[0] == 0;
            }

            return false;

            static ValueTask WriteArrayAsync(BinaryWriter writer, byte[] bytes, CancellationToken cancellationToken)
            {
                writer.Write(bytes.Length);
                writer.Flush();
                return writer.BaseStream.WriteAsync(bytes, cancellationToken);
            }
        }

        public static IEnumerable<string> GetAllSourceFilePaths(Project project)
        {
            return GetSourceFilePaths(project);

            static IEnumerable<string> GetSourceFilePaths(Project project)
            {
                if (project.FilePath is not null)
                {
                    yield return project.FilePath;
                }
                foreach (Document document in project.Documents)
                {
                    if (document.SourceCodeKind == SourceCodeKind.Regular && document.FilePath is string path)
                    {
                        yield return path;
                    }
                }
                foreach (ProjectReference reference in project.ProjectReferences)
                {
                    Project? refProject = project.Solution.GetProject(reference.ProjectId);
                    if (refProject is not null)
                    {
                        foreach (string path in GetSourceFilePaths(refProject))
                        {
                            yield return path;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _cancelInitialization.Cancel();
            Workspace?.Dispose();
            _writer?.Dispose();
        }

        private ImmutableArray<string> GetDiagnostics(Solution solution, CancellationToken cancellationToken)
        {
            // TODO only relevant projects

            var @lock = new object();
            var builder = ImmutableArray<string>.Empty;
            Parallel.ForEach(solution.Projects, project =>
            {
                if (!project.TryGetCompilation(out var compilation))
                {
                    return;
                }

                var compilationDiagnostics = compilation.GetDiagnostics(cancellationToken);
                if (compilationDiagnostics.IsDefaultOrEmpty)
                {
                    return;
                }

                var projectDiagnostics = ImmutableArray<string>.Empty;
                foreach (var item in compilationDiagnostics)
                {
                    if (item.Severity == DiagnosticSeverity.Error)
                    {
                        var diagnostic = CSharpDiagnosticFormatter.Instance.Format(item);
                        projectDiagnostics = projectDiagnostics.Add(diagnostic);
                    }
                }

                lock (@lock)
                {
                    builder = builder.AddRange(projectDiagnostics);
                }
            });

            return builder;
        }
    }
}
