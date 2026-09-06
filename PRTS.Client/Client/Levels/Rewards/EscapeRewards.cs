using LabExtended.API;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Levels.Rewards;

/// <summary>
/// Contains the rewards for escaping and helping others escape.
/// </summary>
public static class EscapeRewards
{
    /// <summary>
    /// The amount of points a player receives for helping another player escape.
    /// </summary>
    [Config("level-rewards", "helped-escape-reward", "The amount of points a player receives for helping another player escape.")]
    public static int HelpedEscapeReward { get; set; } = 2;

    /// <summary>
    /// The amount of points a player receives for escaping.
    /// </summary>
    [Config("level-rewards", "escaped-reward", "The amount of points a player receives for escaping.")]
    public static int EscapedReward { get; set; } = 2;

    /// <summary>
    /// The amount of points a player receives for escaping with an SCP item (stacks for each item).
    /// </summary>
    [Config("level-rewards", "escaped-with-scp-item-reward", "The amount of points a player receives for escaping with an SCP item (stacks for each item).")]
    public static int EscapedWithScpItemReward { get; set; } = 2;

    /// <summary>
    /// The list of SCP items that will give a player points for escaping with them.
    /// </summary>
    [Config("level-rewards", "escaped-with-scp-item-list", "The list of SCP items that will give a player points for escaping with them.")]
    public static ItemType[] EscapedWithScpItemList { get; set; } = EnumUtils<ItemType>.Values.Where(it => it.ToString().StartsWith("scp", StringComparison.OrdinalIgnoreCase)).ToArray();

    private static void OnEscaped(ReferenceHub hub)
    {
        if (!ExPlayer.TryGet(hub, out var player))
            return;

        if (player.DisarmedBy is ExPlayer disarmer)
            disarmer.AddXp(HelpedEscapeReward);

        var scpItems = player.Inventory.ItemTypes.Count(EscapedWithScpItemList.Contains);

        if (scpItems > 0)
            player.AddXp(EscapedWithScpItemReward * scpItems, $"Útěk s SCP předměty ({scpItems})");
        else
            player.AddXp(EscapedReward, "Útěk");
    }

    internal static void Initialize()
    {
        Escape.OnServerPlayerEscape += OnEscaped;
    }
}
