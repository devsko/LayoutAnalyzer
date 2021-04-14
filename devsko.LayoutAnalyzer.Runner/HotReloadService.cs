using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace devsko.LayoutAnalyzer.Runner
{
    public readonly struct Update
    {
        public readonly Guid ModuleId;
        public readonly ImmutableArray<byte> ILDelta;
        public readonly ImmutableArray<byte> MetadataDelta;
        public readonly ImmutableArray<byte> PdbDelta;
    }

    public class HotReloadService
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

        public HotReloadService(HostWorkspaceServices services)
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

}
