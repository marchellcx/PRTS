using LabExtended.API;
using LabExtended.Core;

namespace PRTS.Client.Sitrep;

/// <summary>
/// Provides utility methods for processing and manipulating strings in the context of Situation Reports (SitReps).
/// </summary>
public static class SitrepStrings
{
    /// <summary>
    /// Adds player-specific variables to the provided dictionary with keys prefixed by the specified string.
    /// </summary>
    /// <param name="vars">The dictionary to which player variables will be added.</param>
    /// <param name="key">The prefix used for keys when adding player variables to the dictionary.</param>
    /// <param name="player">The player object containing the information to be added as variables.</param>
    public static void AddPlayerVariables(this Dictionary<string, object> vars, string key, ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            player = ExPlayer.Host;

        vars.Add($"{key}.Nick", player.Nickname);
        vars.Add($"{key}.UserId", player.UserId);
        vars.Add($"{key}.Ip", player.IpAddress);
        vars.Add($"{key}.Country", player.CountryCode);
        vars.Add($"{key}.Health", player.Health.ToString("000.00"));
        vars.Add($"{key}.MaxHealth", player.MaxHealth.ToString("000.00"));
        vars.Add($"{key}.ArtHealth", player.ArtificialHealth.ToString("000.00"));
        vars.Add($"{key}.MaxArtHealth", player.MaxArtificialHealth.ToString("000.00"));
        vars.Add($"{key}.Group", player.PermissionsGroupName ?? "None");
        vars.Add($"{key}.Rank", player.ReferenceHub.serverRoles.Network_myText);
        vars.Add($"{key}.Role", player.Role.Name);
        vars.Add($"{key}.Item", player.Inventory.CurrentItemType);
    }
    
    /// <summary>
    /// Replaces variables within a given string based on provided mappings and optional additional variable resolvers.
    /// Variables are defined with a leading '$', followed by the variable name and, optionally, a property or identifier separated by a colon.
    /// </summary>
    /// <param name="str">The input string potentially containing variable placeholders to be replaced.</param>
    /// <param name="variables">A dictionary mapping variable names to <see cref="ExPlayer"/> objects, from which specific properties can be retrieved.</param>
    /// <returns>
    /// A string with variables replaced by their corresponding values as determined by the dictionary or additional variable resolver.
    /// </returns>
    public static string ReplaceVariables(string str, Dictionary<string, object> variables)
    {
        try
        {
            foreach (var variable in variables)
            {
                str = str.Replace($"${variable.Key}", variable.Value?.ToString() ?? "");
            }
        }
        catch (Exception ex)
        {
            ApiLog.Error(ex);
        }

        return str;
    }
}