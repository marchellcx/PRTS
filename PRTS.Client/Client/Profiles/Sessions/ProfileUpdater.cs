using LabExtended.API;
using LabExtended.Core;

using MEC;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Profiles.Sessions;

/// <summary>
/// Represents a session associated with a user profile in the system.
/// This class manages the lifecycle of a user's profile session, including initialization,
/// periodic updates, and interaction with profile modules for session synchronization.
/// </summary>
public class ProfileUpdater
{
    /// <summary>
    /// The interval, in seconds, at which the profile session updates occur.
    /// </summary>
    [Config("profiles", "session-update-interval", "Interval between profile session updates (seconds")]
    public static float SessionUpdateInterval { get; set; } = 2f;
    
    private CoroutineHandle updateSessionHandle;
    
    /// <summary>
    /// The ID of the session.
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// The user ID tied to this profile.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// The player object.
    /// </summary>
    public ExPlayer Player { get; }
    
    /// <summary>
    /// The profile module.
    /// </summary>
    public ProfileModule Module { get; }

    /// <summary>
    /// Creates a new profile session.
    /// </summary>
    public ProfileUpdater(string id, string userId, ExPlayer player, ProfileModule module)
    {
        Id = id;
        UserId = userId;
        Player = player;
        Module = module;
    }

    /// <summary>
    /// Starts the profile session by initializing the session update process.
    /// This method activates a coroutine that periodically updates the session
    /// using the associated profile module and user information.
    /// </summary>
    public void Start()
    {
        Timing.KillCoroutines(updateSessionHandle);
        
        updateSessionHandle = Timing.RunCoroutine(UpdateSession(), Segment.LateUpdate);       
    }

    /// <summary>
    /// Stops the profile session by terminating the session update process.
    /// This method halts the coroutine responsible for periodic session updates,
    /// effectively ending the session lifecycle management activity.
    /// </summary>
    public void Stop()
    {
        Timing.KillCoroutines(updateSessionHandle);      
    }

    private IEnumerator<float> UpdateSession()
    {
        while (Player?.ReferenceHub != null
               && Module != null
               && !Module.IsDestroyed)
        {
            yield return Timing.WaitForSeconds(SessionUpdateInterval);

            try
            {
                Module.CallCmdUpdateSession(UserId, Id);
            }
            catch (Exception ex)
            {
                ApiLog.Error(ex);                
            }
        }
    }
}