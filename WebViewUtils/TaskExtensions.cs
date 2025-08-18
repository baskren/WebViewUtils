using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace WebViewUtils;

public static class TaskExtensions
{
    public static async Task AsCancellable(this Task task, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static async Task AsCancellable(this IAsyncAction action, CancellationToken cancellationToken)
    {
        try
        {
            await action.AsTask(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }


    public static async Task<T> AsCancellable<T>(this Task<T> task,T cancelledValue, CancellationToken cancellationToken)
    {
        try
        {
            return await task.WaitAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return cancelledValue;
        }
    }


}
