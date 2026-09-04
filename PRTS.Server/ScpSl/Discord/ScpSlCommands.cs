using Discord;
using Discord.Interactions;

using NiveraAPI.Console;
using NiveraAPI.IO.Configs;

using PRTS.Discord;
using PRTS.ScpSl.Modules.Plugins;

namespace PRTS.ScpSl.Discord;

public class ScpSlCommands : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    /// List of commands that are prohibited from being used by administrators.
    /// </summary>
    [Config("scp-sl", "prohibited-commands", "List of commands that are prohibited from being used by administrators.")]
    public static string[] ProhibitedCommands { get; set; } = [];

    /// <summary>
    /// List of commands that are whitelisted for administrators.
    /// </summary>
    [Config("scp-sl", "whitelisted-commands", "List of commands that are whitelisted for administrators.")]
    public static string[] WhitelistedCommands { get; set; } = [];
    
    /// <summary>
    /// Lists all plugins on the server using a slash command interaction.
    /// </summary>
    [SlashCommand("listplugins", "Zobrazí seznam pluginů na serveru.")]
    public async Task ListAsync()
    {
        try
        {
            if (!Context.TryGetServerEntity<PluginManagerModule>(out var pmm))
            {
                await RespondAsync(":x: Bot nemá propojený plugin manager.", ephemeral: true);
                return;
            }

            if (pmm.Plugins.Length < 1)
            {
                await RespondAsync("Na serveru nejsou žádné pluginy.", ephemeral: true);
                return;
            }

            var modal = new ModalBuilder();
            var menu = new SelectMenuBuilder();

            menu.WithCustomId("plugin_list_menu")
                .WithPlaceholder("Vyber si plugin")
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithRequired(false);

            for (var x = 0; x < pmm.Plugins.Length; x++)
            {
                var plugin = pmm.Plugins[x];

                var option = new SelectMenuOptionBuilder()
                    .WithValue(plugin.Name)
                    .WithLabel(plugin.Name);

                if (x == 0)
                    option.WithDefault(true);

                menu.AddOption(option);
            }

            modal.WithCustomId("plugin_list_modal");
            modal.WithTitle("Plugin List");
            modal.AddSelectMenu("Plugin List", menu);
            
            var hasPerms = Context.HasPermission("ManagePlugins");

            await Context.RespondMenuAsync(modal, "plugin_list_menu", async (modal, menu) =>
            {
                if (menu.Values.Count < 1)
                {
                    await modal.RespondAsync(":x: | Musíš vybrat plugin.", ephemeral: true);
                    return;
                }

                var option = menu.Values.First();
                var plugin = pmm.Plugins.FirstOrDefault(p => p.Name == option);

                if (plugin == null)
                {
                    await modal.RespondAsync(":x: | Plugin nebyl nalezen.", ephemeral: true);
                    return;
                }

                var embed = new EmbedBuilder();

                embed.WithTitle(plugin.Name);
                embed.WithColor(Color.Blue);
                embed.WithDescription(plugin.Description);

                embed.AddField("Autor", plugin.Author, true);
                embed.AddField("Verze", plugin.Version, true);
                
                if (hasPerms)
                    embed.AddField("Soubor", plugin.File, true);

                await modal.RespondAsync(ephemeral: true, embed: embed.Build());
            });
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"An error occured while running command:\n{ex}");
        }
    }
}