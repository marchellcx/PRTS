using LabExtended.Core;

using LiteNetLib;

namespace PRTS.Monitoring.Patches;

/// <summary>
/// Patches the connection request processing to handle monitoring client connections and validate their keys.
/// </summary>
public static class ConnectionRequestPatch
{
    /// <summary>
    /// Intercepts connection requests to check for monitoring client connections and validate their keys.
    /// </summary>
    /// <param name="request">The connection request to be processed.</param>
    /// <returns>True if the connection request should be processed further; otherwise, false.</returns>
    [HarmonyLib.HarmonyPatch(typeof(CustomLiteNetLib4MirrorTransport), nameof(CustomLiteNetLib4MirrorTransport.ProcessConnectionRequest))]
    public static bool Prefix(ConnectionRequest request)
    {
        if (request.Data.AvailableBytes > 0)
        {
            var id = request.Data.PeekByte();

            if (id > 2 && id == MonitoringClient.ClientId)
            {
                ApiLog.Info("Received connection request from monitoring client.");

                request.Data.SkipBytes(1);

                var key = request.Data.GetString();

                if (!string.Equals(key, MonitoringClient.ClientKey))
                {
                    ApiLog.Warn("Monitoring client key mismatch. Rejecting connection request.");

                    request.Reject();
                }
                else
                {
                    MonitoringClient.AcceptClient(request);
                }

                return false;
            }
        }

        return true;
    }
}
