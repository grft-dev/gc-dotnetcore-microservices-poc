namespace Insurance.Graftcode;

internal static class GraftAsync
{
    // Graft methods are synchronous for now, so this waits for the task at the boundary.
    // The method body can stay ordinary async/await. It is a temporary bridge, and it
    // is not the shape we want to keep. Native Task and async support is in progress.
    // Graft methods will then be async the same way as the rest of a .NET codebase,
    // and this helper will go away.
    internal static T Run<T>(Func<Task<T>> operation) =>
        Task.Run(operation).ConfigureAwait(false).GetAwaiter().GetResult();
}
