using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace devsko.LayoutAnalyzer.Test
{
    public class HotReload : IDisposable
    {
        public readonly struct Update
        {
            public readonly Guid ModuleId;
            public readonly ImmutableArray<byte> ILDelta;
            public readonly ImmutableArray<byte> MetadataDelta;
            public readonly ImmutableArray<byte> PdbDelta;
        }

        private class HotReloadServiceAccessor
        {
            private delegate ref (ImmutableArray<Update>, ImmutableArray<Diagnostic>) ConvertTaskDelegate(Task task);

            private static readonly Type _hotReloadServiceType = Type.GetType("Microsoft.CodeAnalysis.ExternalAccess.Watch.Api.WatchHotReloadService, Microsoft.CodeAnalysis.Features, Version=3.10.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")!;
            private static readonly Type _enCWorkspaceServiceType = Type.GetType("Microsoft.CodeAnalysis.EditAndContinue.EditAndContinueWorkspaceService, Microsoft.CodeAnalysis.Features, Version=3.10.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")!;
            private static readonly ValueTuple<
                Func<object, Solution, CancellationToken, Task>,
                Action<object>,
                Func<object, Solution, CancellationToken, Task>,
                ConvertTaskDelegate,
                Func<object>>
                _delegates = CreateDelegates();

            private static ValueTuple<
                Func<object, Solution, CancellationToken, Task>,
                Action<object>,
                Func<object, Solution, CancellationToken, Task>,
                ConvertTaskDelegate,
                Func<object>>
            CreateDelegates()
            {
                ParameterExpression thisParameter = Expression.Parameter(typeof(object), "this");
                Expression @this = Expression.Convert(thisParameter, _hotReloadServiceType);

                ParameterExpression solution = Expression.Parameter(typeof(Solution), "solution");
                ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

                MethodInfo startSessionAsyncMethod = _hotReloadServiceType.GetMethod("StartSessionAsync", BindingFlags.Instance | BindingFlags.Public)!;
                var startSessionAsync = Expression.Lambda<Func<object, Solution, CancellationToken, Task>>(
                    Expression.Call(@this, startSessionAsyncMethod, solution, cancellationToken),
                    thisParameter, solution, cancellationToken
                ).Compile();

                MethodInfo endSessionMethod = _hotReloadServiceType.GetMethod("EndSession", BindingFlags.Instance | BindingFlags.Public)!;
                var endSession = Expression.Lambda<Action<object>>(
                    Expression.Call(@this, endSessionMethod),
                    thisParameter
                ).Compile();

                MethodInfo emitSolutionUpdateAsyncMethod = _hotReloadServiceType.GetMethod("EmitSolutionUpdateAsync", BindingFlags.Instance | BindingFlags.Public)!;
                var emitSolutionUpdateAsync = Expression.Lambda<Func<object, Solution, CancellationToken, Task>>(
                    Expression.Call(@this, emitSolutionUpdateAsyncMethod, solution, cancellationToken),
                    thisParameter, solution, cancellationToken
                ).Compile();

                Type updateType = Type.GetType("Microsoft.CodeAnalysis.ExternalAccess.Watch.Api.WatchHotReloadService+Update, Microsoft.CodeAnalysis.Features, Version=3.10.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")!;
                Type immutableArrayOfUpdateType = typeof(ImmutableArray<>).MakeGenericType(updateType);
                Type taskResultType = typeof(ValueTuple<,>).MakeGenericType(immutableArrayOfUpdateType, typeof(ImmutableArray<Diagnostic>));
                MethodInfo unsafeAsMethod = typeof(Unsafe).GetMethod(nameof(Unsafe.As), 2, BindingFlags.Static | BindingFlags.Public, null, new[] { Type.MakeGenericMethodParameter(0).MakeByRefType() }, null)!;
                unsafeAsMethod = unsafeAsMethod.MakeGenericMethod(taskResultType, typeof(ValueTuple<ImmutableArray<Update>, ImmutableArray<Diagnostic>>));

                ParameterExpression task = Expression.Parameter(typeof(Task), "task");
                ParameterExpression result = Expression.Variable(taskResultType, "result");

                var convertTaskResult = Expression.Lambda<ConvertTaskDelegate>(
                Expression.Block(
                    new[] { result },
                    Expression.Assign(
                        result,
                        Expression.Property
                            (Expression.Convert(task, typeof(Task<>).MakeGenericType(taskResultType)),
                            "Result")),
                        Expression.Call(
                            unsafeAsMethod,
                            result)),
                    task
                ).Compile();

                FieldInfo log = _enCWorkspaceServiceType.GetField("Log", BindingFlags.Static | BindingFlags.NonPublic)!;
                var getLog = Expression.Lambda<Func<object>>(
                    Expression.Field(null, log)
                ).Compile();

                return ValueTuple.Create(startSessionAsync, endSession, emitSolutionUpdateAsync, convertTaskResult, getLog);
            }

            private readonly object _hotReloadService;

            public HotReloadServiceAccessor(HostWorkspaceServices services)
            {
                _hotReloadService = Activator.CreateInstance(_hotReloadServiceType, services)!;
            }

            public Task StartSessionAsync(Solution solution, CancellationToken cancellationToken)
                => _delegates.Item1(_hotReloadService, solution, cancellationToken);

            public void EndSession()
                => _delegates.Item2(_hotReloadService);

            public async Task<(ImmutableArray<Update>, ImmutableArray<Diagnostic>)> EmitSolutionUpdateAsync(Solution solution, CancellationToken cancellationToken)
            {
                Task task = _delegates.Item3(_hotReloadService, solution, cancellationToken);
                await task.ConfigureAwait(false);
                var log = _delegates.Item5();

                return _delegates.Item4(task);
            }
        }
        private class Progress : IProgress<ProjectLoadProgress>
        {
            public void Report(ProjectLoadProgress value)
            { }
        }

        private readonly string _projectFilePath;
        private readonly Dictionary<string, Project> _projects;
        private readonly HotReloadServiceAccessor _hotReloadServiceAccessor;

        public MSBuildWorkspace Workspace { get; private init; }
        public string ProjectName { get; private init; }
        public Solution Solution { get; private set; }

        public static async Task<HotReload> InitializeAsync(string projectFilePath, CancellationToken cancellationToken)
        {
            MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            Project project = await workspace.OpenProjectAsync(projectFilePath, new Progress(), cancellationToken).ConfigureAwait(false);

            HotReloadServiceAccessor accessor = new HotReloadServiceAccessor(workspace.Services);
            await accessor.StartSessionAsync(workspace.CurrentSolution, cancellationToken).ConfigureAwait(false);

            return new HotReload(workspace, project, accessor);
        }

        private HotReload(MSBuildWorkspace workspace, Project root, HotReloadServiceAccessor hotRelaodServiceAccessor)
        {
            Workspace = workspace;
            Solution = workspace.CurrentSolution;
            _projectFilePath = root.FilePath!;
            _hotReloadServiceAccessor = hotRelaodServiceAccessor;

            _projects = new Dictionary<string, Project>();
            foreach (Project project in root.Solution.Projects)
            {
                if (project == root || project.FilePath == _projectFilePath)
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

        public ICollection<string> Flavors
        => _projects.Keys;

        public Project GetProject(string flavor = "")
        => _projects[flavor];

        public async ValueTask<bool> HandleFileChangeAsync(string path, CancellationToken cancellationToken)
        {
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

            (ImmutableArray<Update> updates, ImmutableArray<Diagnostic> diagnostics) = await _hotReloadServiceAccessor.EmitSolutionUpdateAsync(updatedSolution, cancellationToken).ConfigureAwait(false);

            Solution = updatedSolution;
            //if (hotReloadDiagnostics.IsDefaultOrEmpty && updates.IsDefaultOrEmpty)
            //{
            //    // It's possible that there are compilation errors which prevented the solution update
            //    // from being updated. Let's look to see if there are compilation errors.
            //    var diagnostics = GetDiagnostics(updatedSolution, cancellationToken);
            //    if (diagnostics.IsDefaultOrEmpty)
            //    {
            //        await _deltaApplier.Apply(context, file.FilePath, updates, cancellationToken);
            //    }
            //    else
            //    {
            //        await _deltaApplier.ReportDiagnosticsAsync(context, diagnostics, cancellationToken);
            //    }

            //    HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
            //    // Even if there were diagnostics, continue treating this as a success
            //    return true;
            //}

            //if (!hotReloadDiagnostics.IsDefaultOrEmpty)
            //{
            //    // Rude edit.
            //    _reporter.Output("Unable to apply hot reload because of a rude edit. Rebuilding the app...");
            //    foreach (var diagnostic in hotReloadDiagnostics)
            //    {
            //        _reporter.Verbose(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
            //    }

            //    HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
            //    return false;
            //}

            //_currentSolution = updatedSolution;

            //var applyState = await _deltaApplier.Apply(context, file.FilePath, updates, cancellationToken);
            //HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
            //return applyState;

            return default;

            static async ValueTask<SourceText> GetSourceTextAsync(string path, CancellationToken cancellationToken)
            {
                for (var attemptIndex = 0; attemptIndex < 6; attemptIndex++)
                {
                    try
                    {
                        using var stream = File.OpenRead(path);
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

        public IEnumerable<string> GetAllSourceFilePaths(string flavor)
        {
            return GetSourceFilePaths(GetProject(flavor));

            IEnumerable<string> GetSourceFilePaths(Project project)
            {
                foreach (Document document in project.Documents)
                {
                    if (document.SourceCodeKind == SourceCodeKind.Regular && document.FilePath is string path)
                    {
                        yield return path;
                    }
                }
                foreach (ProjectReference reference in project.ProjectReferences)
                {
                    Project? refProject = Solution.GetProject(reference.ProjectId);
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
            Workspace.Dispose();
        }
    }
}
