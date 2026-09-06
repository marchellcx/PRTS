using InventorySystem.Items.Scp1509;

using LabApi.Events.Handlers;
using LabApi.Events.Arguments.PlayerEvents;

using LabExtended.API;
using LabExtended.Core;
using LabExtended.Events;

using NiveraAPI.IO.Configs;

using PlayerRoles;
using PlayerRoles.PlayableScps.Scp939;

using PlayerStatsSystem;

namespace PRTS.Client.Sitrep.Events;

/// <summary>
/// Represents a service that manages player-related Sitrep notifications
/// by subscribing to and handling player join and leave events.
/// </summary>
public static class SitrepPlayerEvents
{
    /// <summary>
    /// Represents the damage types that can be inflicted by players.
    /// </summary>
    public enum PlayerDamageType
    {
        /// <summary>
        /// Warhead damage type.
        /// </summary>
        Warhead,
        
        /// <summary>
        /// SCP-096 damage type.
        /// </summary>
        Scp096,
        
        /// <summary>
        /// SCP-049 damage type.
        /// </summary>
        Scp049,
        
        /// <summary>
        /// SCP-018 damage type.
        /// </summary>
        Scp018,
        
        /// <summary>
        /// Grenade damage type.
        /// </summary>
        /// 
        Grenade,
        
        /// <summary>
        /// SCP-079 recontainment sequence.
        /// </summary>
        Recontainment,
        
        /// <summary>
        /// Micro-HID damage type.
        /// </summary>
        MicroHID,
        
        /// <summary>
        /// Jailbird damage type.
        /// </summary>
        Jailbird,
        
        /// <summary>
        /// Firearm damage type.
        /// </summary>
        Firearm,
        
        /// <summary>
        /// Disruptor damage type.
        /// </summary>
        Disruptor,
        
        /// <summary>
        /// Asphyxiation damage type.
        /// </summary>
        Asphyxiated,
        
        /// <summary>
        /// Bleeding damage type.
        /// </summary>
        Bleeding,
        
        /// <summary>
        /// Died to bullet wounds.
        /// </summary>
        BulletWounds,
        
        /// <summary>
        /// Died to cardiac arrest.
        /// </summary>
        CardiacArrest,
        
        /// <summary>
        /// Crushed by doors.
        /// </summary>
        Crushed,
        
        /// <summary>
        /// Died to decontamination gases.
        /// </summary>
        Decontamination,
        
        /// <summary>
        /// Fell down.
        /// </summary>
        Falldown,
        
        /// <summary>
        /// Died to hypothermia.
        /// </summary>
        Hypothermia,
        
        /// <summary>
        /// Died in the Pocket Dimension.
        /// </summary>
        PocketDecay,
        
        /// <summary>
        /// Died to poison.
        /// </summary>
        Poisoned,
        
        /// <summary>
        /// Died to SCP-1344
        /// </summary>
        Scp1344,
        
        /// <summary>
        /// Died to SCP-1507
        /// </summary>
        Scp1507,
        
        /// <summary>
        /// Died to SCP-1509
        /// </summary>
        Scp1509,
        
        /// <summary>
        /// Died to SCP-173
        /// </summary>
        Scp173,
        
        /// <summary>
        /// Died to SCP-207
        /// </summary>
        Scp207,
        
        /// <summary>
        /// Died to SCP-3114
        /// </summary>
        Scp3114,
        
        /// <summary>
        /// Died to SCP-939
        /// </summary>
        Scp939,

        /// <summary>
        /// Died to SCP-106
        /// </summary>
        Scp106,
        
        /// <summary>
        /// Got their hands severed.
        /// </summary>
        SeveredHands,
        
        /// <summary>
        /// Died to a Tesla gate.
        /// </summary>
        Tesla,
        
        /// <summary>
        /// Died to a zombie.
        /// </summary>
        Zombie,
        
        /// <summary>
        /// Unknown damage type.
        /// </summary>
        Unknown,
        
        /// <summary>
        /// SCP damage type.
        /// </summary>
        Scp
    }

    /// <summary>
    /// A dictionary mapping player damage types to their corresponding translations.
    /// </summary>
    [Config("sitrep", "damage-translations", "A dictionary mapping player damage types to their corresponding translations.")]
    public static Dictionary<PlayerDamageType, string> DamageTranslations { get; set; } = new()
    {
        { PlayerDamageType.Warhead, "Warhead" },
        { PlayerDamageType.Scp096, "SCP-096" },
        { PlayerDamageType.Scp049, "SCP-049" },
        { PlayerDamageType.Scp018, "SCP-018" },
        { PlayerDamageType.Grenade, "Grenade" },
        { PlayerDamageType.Recontainment, "SCP-079 Recontainment" },
        { PlayerDamageType.MicroHID, "Micro-HID" },
        { PlayerDamageType.Jailbird, "Jailbird" },
        { PlayerDamageType.Firearm, "Firearm" },
        { PlayerDamageType.Disruptor, "Disruptor" },
        { PlayerDamageType.Asphyxiated, "Asphyxiation" },
        { PlayerDamageType.Bleeding, "Bleeding" },
        { PlayerDamageType.BulletWounds, "Bullet Wounds" },
        { PlayerDamageType.CardiacArrest, "Cardiac Arrest" },
        { PlayerDamageType.Crushed, "Crushed" },
        { PlayerDamageType.Decontamination, "Decontamination" },
        { PlayerDamageType.Falldown, "Falldown" },
        { PlayerDamageType.Hypothermia, "Hypothermia" },
        { PlayerDamageType.PocketDecay, "Pocket Decay" },
        { PlayerDamageType.Poisoned, "Poisoned" },
        { PlayerDamageType.Scp1344, "SCP-1344" },
        { PlayerDamageType.Scp1507, "SCP-1507" },
        { PlayerDamageType.Scp1509, "SCP-1509" },
        { PlayerDamageType.Scp173, "SCP-173" },
        { PlayerDamageType.Scp207, "SCP-207" },
        { PlayerDamageType.Scp3114, "SCP-3114" },
        { PlayerDamageType.Scp939, "SCP-939" },
        { PlayerDamageType.SeveredHands, "Severed Hands" },
        { PlayerDamageType.Tesla, "Tesla Gate" },
        { PlayerDamageType.Zombie, "Zombie" },
        { PlayerDamageType.Scp, "SCP" },
        { PlayerDamageType.Scp106, "SCP-106" },
        { PlayerDamageType.Unknown, "Unknown" }
    };

    private static bool init;

    /// <summary>
    /// The message template sent when a player receives damage from a teammate.
    /// Placeholders in the template can include:
    /// - $Player.Nick: Name of the player who was damaged.
    /// - $DamageAmount: The amount of damage received.
    /// - $Attacker.Nick: Name of the teammate who caused the damage.
    /// </summary>
    [Config("sitrep", "message-player-teamdamage", "Message to send when a player gets damages by a teammate.")]
    public static string PlayerTeamDamageMessage { get; set; } = "Player $Player.Nick took $DamageAmount damage from teammate $Attacker.Nick.";

    /// <summary>
    /// The message template used when a player takes damage from an enemy.
    /// This string can contain placeholders such as $Player.Nick (the player's name),
    /// $DamageAmount (the amount of damage inflicted), and $Attacker.Nick (the attacker's name),
    /// which are dynamically replaced at runtime.
    /// </summary>
    [Config("sitrep", "message-player-enemydamage", "Message to send when a player gets damages by an enemy.")]
    public static string PlayerEnemyDamageMessage { get; set; } = "Player $Player.Nick took $DamageAmount damage from enemy $Attacker.Nick.";

    /// <summary>
    /// The message template sent when a player kills an enemy.
    /// Variables in the template include:
    /// $Player.Nick - The nickname of the player.
    /// $DamageType - The type of damage used to kill the enemy.
    /// </summary>
    [Config("sitrep", "message-player-enemykill", "The message to send a player kills an enemy.")]
    public static string PlayerEnemyKillMessage { get; set; } = "Player $Player.Nick killed enemy with $DamageType.";

    /// <summary>
    /// The message template used when a player kills a teammate, often displayed with
    /// dynamic values such as the player's nickname and the type of damage used.
    /// </summary>
    [Config("sitrep", "message-player-teamkill", "The message to send a player kills a teammate.")]
    public static string PlayerTeamKillMessage { get; set; } = "Player $Player.Nick killed teammate with $DamageType.";

    /// <summary>
    /// The message displayed when a player kills themselves in the game.
    /// This message supports placeholders such as $Player.Nick for the player's nickname and $DamageType for the type of damage used.
    /// </summary>
    [Config("sitrep", "message-player-suicide", "The message to send a player kills themselves.")]
    public static string PlayerSuicideMessage { get; set; } = "Player $Player.Nick killed themselves with $DamageType.";
    
    /// <summary>
    /// Specifies the message template sent when a player leaves the game.
    /// The message supports variable substitution, allowing placeholders
    /// like "$Player.Nick" to be replaced with the player's nickname.
    /// </summary>
    [Config("sitrep", "message-player-left", "The message to send when a player leaves the game.")]
    public static string PlayerLeftMessage { get; set; } = "Player $Player.Nick left!";

    /// <summary>
    /// Specifies the message template sent when a player joins the game.
    /// The message supports variable substitution, such as replacing
    /// placeholders like "$Player.Nick" with the player's nickname.
    /// </summary>
    [Config("sitrep", "message-player-joined", "The message to send when a player joins the game.")]
    public static string PlayerJoinedMessage { get; set; } = "Player $Player.Nick joined!";

    /// <summary>
    /// Defines the message template sent when a player spawns in the game.
    /// The message allows variable substitution, enabling placeholders such
    /// as "$Player.Nick" for the player's nickname and "$Player.Role" for
    /// the role the player assumes upon spawning.
    /// </summary>
    [Config("sitrep", "message-player-spawned", "The message to send when a player spawns.")]
    public static string PlayerSpawnedMessage { get; set; } = "Player $Player.Nick spawned as $Player.Role!";

    /// <summary>
    /// Starts the SitrepPlayerEvents service, registering the necessary event listeners
    /// and initializing the associated SitrepService instance.
    /// This method subscribes to player join and leave events, enabling Sitrep notifications to the appropriate channels.
    /// </summary>
    public static void Start()
    {
        if (init)
            return;

        init = true;
        
        PlayerEvents.Hurt += OnPlayerDamage;
        PlayerEvents.Death += OnPlayerDeath;
        PlayerEvents.ChangedRole += OnPlayerSpawned;
        
        ExPlayerEvents.Left += OnPlayerLeft;
        ExPlayerEvents.Verified += OnPlayerJoined;
    }

    private static void OnPlayerLeft(ExPlayer player)
        => SitrepService.TrySendEvent(SitrepEvent.PlayerLeft, PlayerLeftMessage, dict => dict.AddPlayerVariables("Player", player));
    
    private static void OnPlayerJoined(ExPlayer player)
        => SitrepService.TrySendEvent(SitrepEvent.PlayerJoined, PlayerJoinedMessage, dict => dict.AddPlayerVariables("Player", player));

    private static void OnPlayerSpawned(PlayerChangedRoleEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        if (!args.NewRole.RoleTypeId.IsAlive())
            return;
        
        SitrepService.TrySendEvent(SitrepEvent.PlayerSpawned, PlayerSpawnedMessage, dict => dict.AddPlayerVariables("Player", player));
    }
    
    private static void OnPlayerDamage(PlayerHurtEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        if (args.Attacker is not ExPlayer attacker)
            return;

        if (args.DamageHandler == null)
            return;

        var damageAmount = (args.DamageHandler as StandardDamageHandler)!.TotalDamageDealt;
        var damageType = TranslateDamage(args.DamageHandler);
        var damageName = DamageTranslations.TryGetValue(damageType, out var translation) 
            ? translation 
            : damageType.ToString();
        
        if (!HitboxIdentity.IsEnemy(attacker.ReferenceHub, player.ReferenceHub))
        {
            SitrepService.TrySendEvent(SitrepEvent.PlayerTeamDamage, PlayerTeamKillMessage, dict =>
            {
                dict.AddPlayerVariables("Player", player);
                dict.AddPlayerVariables("Attacker", attacker);
                
                dict.Add("DamageName", damageName);
                dict.Add("DamageAmount", damageAmount.ToString("0.00"));
            });
        }
        else
        {
            SitrepService.TrySendEvent(SitrepEvent.PlayerEnemyDamage, PlayerEnemyKillMessage, dict =>
            {
                dict.AddPlayerVariables("Player", player);
                dict.AddPlayerVariables("Attacker", attacker);
                
                dict.Add("DamageName", damageName);
                dict.Add("DamageAmount", damageAmount.ToString("0.00"));
            });
        }
    }

    private static void OnPlayerDeath(PlayerDeathEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        if (args.Attacker is not ExPlayer attacker)
            return;

        if (player.IsNpc || attacker.IsNpc)
            return;

        if (player.IsServer || attacker.IsServer)
            return;

        if (args.DamageHandler == null)
            return;

        if (!args.OldRole.IsAlive())
            return;

        var damageAmount = (args.DamageHandler as StandardDamageHandler)!.TotalDamageDealt;
        var damageType = TranslateDamage(args.DamageHandler);
        var damageName = DamageTranslations.TryGetValue(damageType, out var translation) 
            ? translation 
            : damageType.ToString();

        var role = args.OldRole;

        if (player.UserId == attacker.UserId)
        {
            SitrepService.TrySendEvent(SitrepEvent.PlayerSuicide, PlayerSuicideMessage, dict =>
            {
                dict.AddPlayerVariables("Player", player);

                dict["Player.Role"] = role.ToString();

                dict.Add("DamageName", damageName);
                dict.Add("DamageAmount", damageAmount.ToString("0.00"));
            });
        }
        else if (!HitboxIdentity.IsEnemy(attacker.Role.Type, role)
            && attacker.Role.Type is not RoleTypeId.ClassD 
            && role is not RoleTypeId.ClassD)
       {
            SitrepService.TrySendEvent(SitrepEvent.PlayerTeamKill, PlayerTeamKillMessage, dict =>
            {
                dict.AddPlayerVariables("Player", player);
                dict.AddPlayerVariables("Attacker", attacker);
                
                dict["Player.Role"] = role.ToString();
                
                dict.Add("DamageName", damageName);
                dict.Add("DamageAmount", damageAmount.ToString("0.00"));
            });
        }
        else
        {
            SitrepService.TrySendEvent(SitrepEvent.PlayerEnemyKill, PlayerEnemyKillMessage, dict =>
            {
                dict.AddPlayerVariables("Player", player);
                dict.AddPlayerVariables("Attacker", attacker);
                
                dict["Player.Role"] = role.ToString();
                
                dict.Add("DamageName", damageName);
                dict.Add("DamageAmount", damageAmount.ToString("0.00"));
            });
        }
    }

    private static PlayerDamageType TranslateDamage(DamageHandlerBase damageHandler)
    {
        switch (damageHandler)
        {
            case WarheadDamageHandler: return PlayerDamageType.Warhead;

            case RecontainmentDamageHandler: return PlayerDamageType.Recontainment;
            case MicroHidDamageHandler: return PlayerDamageType.MicroHID;
            case JailbirdDamageHandler: return PlayerDamageType.Jailbird;
            case FirearmDamageHandler: return PlayerDamageType.Firearm;
            case DisruptorDamageHandler: return PlayerDamageType.Disruptor;

            case Scp018DamageHandler: return PlayerDamageType.Scp018;
            case Scp049DamageHandler: return PlayerDamageType.Scp049;
            case Scp096DamageHandler: return PlayerDamageType.Scp096;
            case Scp939DamageHandler: return PlayerDamageType.Scp939;
            case Scp1509DamageHandler: return PlayerDamageType.Scp1509;

            case ScpDamageHandler scp: return scp.Attacker.Role switch
            {
                RoleTypeId.Scp049 => PlayerDamageType.Scp049,
                RoleTypeId.Scp096 => PlayerDamageType.Scp096,
                RoleTypeId.Scp106 => PlayerDamageType.Scp106,
                RoleTypeId.Scp173 => PlayerDamageType.Scp173,
                RoleTypeId.Scp939 => PlayerDamageType.Scp939,

                _ => PlayerDamageType.Scp
            };

            case ExplosionDamageHandler explosion: return explosion.ExplosionType switch
            { 
                ExplosionType.Disruptor => PlayerDamageType.Disruptor,
                ExplosionType.Jailbird => PlayerDamageType.Jailbird,
                
                _ => PlayerDamageType.Grenade
            };

            case UniversalDamageHandler universalDamageHandler:
            {
                if (universalDamageHandler.TranslationId == DeathTranslations.Asphyxiated.Id)
                    return PlayerDamageType.Asphyxiated;

                if (universalDamageHandler.TranslationId == DeathTranslations.Bleeding.Id)
                    return PlayerDamageType.Bleeding;

                if (universalDamageHandler.TranslationId == DeathTranslations.BulletWounds.Id)
                    return PlayerDamageType.BulletWounds;

                if (universalDamageHandler.TranslationId == DeathTranslations.CardiacArrest.Id)
                    return PlayerDamageType.CardiacArrest;

                if (universalDamageHandler.TranslationId == DeathTranslations.Crushed.Id)
                    return PlayerDamageType.Crushed;

                if (universalDamageHandler.TranslationId == DeathTranslations.Decontamination.Id)
                    return PlayerDamageType.Decontamination;

                if (universalDamageHandler.TranslationId == DeathTranslations.Explosion.Id)
                    return PlayerDamageType.Grenade;

                if (universalDamageHandler.TranslationId == DeathTranslations.Falldown.Id)
                    return PlayerDamageType.Falldown;

                if (universalDamageHandler.TranslationId == DeathTranslations.Hypothermia.Id)
                    return PlayerDamageType.Hypothermia;

                if (universalDamageHandler.TranslationId == DeathTranslations.MicroHID.Id)
                    return PlayerDamageType.MicroHID;

                if (universalDamageHandler.TranslationId == DeathTranslations.PocketDecay.Id)
                    return PlayerDamageType.PocketDecay;

                if (universalDamageHandler.TranslationId == DeathTranslations.Poisoned.Id)
                    return PlayerDamageType.Poisoned;

                if (universalDamageHandler.TranslationId == DeathTranslations.Recontained.Id)
                    return PlayerDamageType.Recontainment;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp049.Id)
                    return PlayerDamageType.Scp049;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp096.Id)
                    return PlayerDamageType.Scp096;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp127Bullets.Id)
                    return PlayerDamageType.Firearm;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp1344.Id)
                    return PlayerDamageType.Scp1344;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp1507Peck.Id)
                    return PlayerDamageType.Scp1507;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp1509.Id)
                    return PlayerDamageType.Scp1509;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp173.Id)
                    return PlayerDamageType.Scp173;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp207.Id)
                    return PlayerDamageType.Scp207;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp3114Slap.Id)
                    return PlayerDamageType.Scp3114;

                if (universalDamageHandler.TranslationId == DeathTranslations.Scp939Lunge.Id
                    || universalDamageHandler.TranslationId == DeathTranslations.Scp939Other.Id)
                    return PlayerDamageType.Scp939;

                if (universalDamageHandler.TranslationId == DeathTranslations.SeveredHands.Id)
                    return PlayerDamageType.SeveredHands;

                if (universalDamageHandler.TranslationId == DeathTranslations.Tesla.Id)
                    return PlayerDamageType.Tesla;

                if (universalDamageHandler.TranslationId == DeathTranslations.Warhead.Id)
                    return PlayerDamageType.Warhead;

                if (universalDamageHandler.TranslationId == DeathTranslations.Zombie.Id)
                    return PlayerDamageType.Zombie;

                break;
            }
        }
        
        ApiLog.Warn("PRTS", $"Unknown damage handler: &3{damageHandler.GetType().Name}&r");
        return PlayerDamageType.Unknown;
    }
}