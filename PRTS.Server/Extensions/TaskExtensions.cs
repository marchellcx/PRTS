using NiveraAPI.Utilities;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods for working with tasks by enabling additional functionality,
/// such as executing continuations on the main thread upon task completion.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Extends a task to invoke a continuation on the main thread once it completes.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="task">The task to monitor for completion.</param>
    /// <param name="continuation">The action to execute on the main thread after the task completes successfully, using the task result.</param>
    /// <exception cref="ArgumentNullException">Thrown if the task or continuation is null.</exception>
    public static void ContinueOnMainThread<TResult>(this Task<TResult> task, Action<TResult> continuation)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        if (continuation == null)
            throw new ArgumentNullException(nameof(continuation));
        
        task.ContinueWithOnMain(t =>
        {
            if (t.IsFaulted)
            {
                Utils.Error("Utils/ContinueOnMainThread",$"Cannot continue on main thread: {t.Exception?.ToString() ?? "Unknown error"}");
                return;
            }
            
            continuation(t.Result);
        });
    }
    
    /// <summary>
    /// Extends a task to invoke a continuation on the main thread once it completes.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <typeparam name="TArg">The type of the additional argument to be supplied to the continuation function.</typeparam>
    /// <param name="task">The task to monitor for completion.</param>
    /// <param name="arg">An additional argument to pass to the continuation function upon task completion.</param>
    /// <param name="continuation">The action to execute on the main thread after the task completes successfully, using the task result and the additional argument.</param>
    /// <exception cref="ArgumentNullException">Thrown if the task or continuation is null.</exception>
    public static void ContinueOnMainThread<TResult, TArg>(this Task<TResult> task, TArg arg,
        Action<TResult, TArg> continuation)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        if (continuation == null)
            throw new ArgumentNullException(nameof(continuation));
        
        task.ContinueWithOnMain(t =>
        {
            if (t.IsFaulted)
            {
                Utils.Error("Utils/ContinueOnMainThread",$"Cannot continue on main thread: {t.Exception?.ToString() ?? "Unknown error"}");
                return;
            }
            
            continuation(t.Result, arg);
        });
    }
}