using NiveraAPI.IO.Network.Entities;
using NiveraAPI.Utilities;

namespace PRTS.ScpSl;

/// <summary>
/// Provides extension methods for the ScpSlServer class to facilitate asynchronous communication with server entities.
/// </summary>
public static class ScpSlExtensions
{
    /// <summary>
    /// Awaits a response from a server entity by sending a request and waiting for the response asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The type of the server entity.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the server entity.</typeparam>
    /// <param name="server">The ScpSlServer instance.</param>
    /// <param name="sendRequest">An action that sends a request to the server entity and provides a callback for the response.</param>
    /// <param name="maxWait">The maximum time to wait for a response.</param>
    /// <returns>The response from the server entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the server entity is not found.</exception>
    /// <exception cref="TimeoutException">Thrown if the response times out.</exception>
    public static async Task<TResponse> AwaitServerEntityResponseAsync<TEntity, TResponse>(this ScpSlServer server, Action<TEntity, Action<TResponse>> sendRequest, TimeSpan? maxWait = null) where TEntity : Entity
    {
        if (!server.Manager.TryGetFirstEntity<TEntity>(out var entity))
            throw new InvalidOperationException("Entity not found");

        var complete = false;
        var response = default(TResponse);

        var setResponse = new Action<TResponse>(x =>
        {
            response = x;
            complete = true;
        });

        await ThreadHelper.RunOnMainThread(() => { sendRequest(entity, setResponse); });

        var start = DateTime.Now;

        while (!complete)
        {
            await Task.Delay(100);

            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Server entity response timed out");
        }

        return response;
    }
}
