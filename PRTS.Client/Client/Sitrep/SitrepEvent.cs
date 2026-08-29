namespace PRTS.Client.Sitrep;

/// <summary>
/// Represents the different types of situational reports (SITREPs) or events
/// that can occur within the game runtime environment.
/// </summary>
public enum SitrepEvent
{
    /// <summary>
    /// Alpha Warhead detonation sequence started.
    /// </summary>
    WarheadStarted,
    
    /// <summary>
    /// Alpha Warhead detonation sequence stopped.
    /// </summary>
    WarheadStopped,
    
    /// <summary>
    /// Alpha Warhead detonation sequence completed.
    /// </summary>
    WarheadDetonated,
    
    /// <summary>
    /// Player left the game.
    /// </summary>
    PlayerLeft,
    
    /// <summary>
    /// Player died by suicide.
    /// </summary>
    PlayerSuicide,
    
    /// <summary>
    /// Player joined the game.
    /// </summary>
    PlayerJoined,
    
    /// <summary>
    /// Player spawned.
    /// </summary>
    PlayerSpawned,
    
    /// <summary>
    /// Player killed a teammate.
    /// </summary>
    PlayerTeamKill,
    
    /// <summary>
    /// Player damaged a teammate.
    /// </summary>
    PlayerTeamDamage,
    
    /// <summary>
    /// Player killed an enemy.
    /// </summary>
    PlayerEnemyKill,
    
    /// <summary>
    /// Player damaged an enemy.
    /// </summary>
    PlayerEnemyDamage,
    
    /// <summary>
    /// Round started.
    /// </summary>
    RoundEnded,
    
    /// <summary>
    /// Round ended.
    /// </summary>
    RoundStarted,
    
    /// <summary>
    /// Round is waiting for players to join.
    /// </summary>
    RoundWaiting,
    
    /// <summary>
    /// Round is restarting.
    /// </summary>
    RoundRestarting,
}