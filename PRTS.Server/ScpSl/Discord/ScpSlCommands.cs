using Discord;
using Discord.Interactions;
using NiveraAPI.Console;
using NiveraAPI.Extensions;
using NiveraAPI.IO.Configs;
using NiveraAPI.Utilities;

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
    /// Executes a remote server command asynchronously.
    /// </summary>
    /// <param name="command">The command to be executed on the SCP-SL server.</param>
    /// <returns>A task representing the asynchronous command execution operation.</returns>
    [SlashCommand("command", "Invokes a command on the server.")]
    public async Task CommandAsync([Summary("Command", "Příkaz co chcete spustit na serveru.")] string command)
    {
        if (!ScpSlManager.Bots.TryGetFirst(kvp => kvp.Value.Client == Context.Client, out var bot))
        {
            await RespondAsync(":x: | Bot nenalezen.", ephemeral: true);
            return;
        }

        if (bot.Value.Server == null)
        {
            await RespondAsync(":x: | Bot nemá propojený server.", ephemeral: true);
            return;
        }
        
        if (!Context.HasPermission("SendRemoteCommands"))
        {
            await RespondAsync(":x: | Nemáš povolení na tento příkaz.", ephemeral: true);
            return;
        }
        
        if (ProhibitedCommands.Any(c => command.ToLowerInvariant().StartsWith(c.ToLowerInvariant())))
        {
            await RespondAsync(":x: | Tento příkaz je zakázán.");
            return;
        }

        if (WhitelistedCommands.Length > 0 &&
            !WhitelistedCommands.Any(c => command.ToLowerInvariant().StartsWith(c.ToLowerInvariant())))
        {
            await RespondAsync(":x: | Tento příkaz je zakázán.");
            return;
        }

        await RespondAsync("Odesílám příkaz serveru ..", ephemeral: true);

        var complete = false;
        var response = string.Empty;
        
        await ThreadHelper.RunOnMainThread(() =>
        {
            var task = bot.Value.Server.CallRpcInvokeCommandAsync(command);
            
            while (!task.IsCompleted && !task.IsFaulted)
                continue;
            
            complete = true;
            response = task.IsFaulted ? task.Exception?.ToString() ?? "Unknown error" : task.Result;
        });
        
        while (!complete)
            await Task.Delay(100);
        
        if (response.Length > 2000)
            response = response.Substring(0, 1996) + " ...";

        await ModifyOriginalResponseAsync(msg => msg.Content = $"```{response}```");
    }
    
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

    /// <summary>
    /// Handles the upload of plugins to the server using a slash command interaction.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [SlashCommand("uploadplugin", "Slouží k nahrávání pluginů na server.")]
    public async Task UploadAsync()
    {
        try
        {
            if (!Context.HasPermission("ManagePlugins"))
            {
                await RespondAsync(":x: | Nemáš povolení na tento příkaz.", ephemeral: true);
                return;
            }

            var file = new FileUploadComponentBuilder();

            file.WithCustomId("plugin_upload")
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithRequired(true);

            var modal = new ModalBuilder()
                .WithCustomId("plugin_upload_modal")
                .WithTitle("Plugin Upload")
                .AddFileUpload("Plugin Upload", file);

            var response = await Context.AwaitModalResponseAsync(modal, TimeSpan.FromMinutes(2));
            
            if (response.Data.Attachments.Count == 0)
            {
                await response.RespondAsync(":x: | Modal nemá žádné attachmenty.");
                return;
            }

            var attachment = response.Data.Attachments.First();
            
            var uri = new Uri(attachment.Url);
            var name = Path.GetFileName(uri.AbsolutePath);

            await response.DeferAsync(true);

            var result = await Context.AwaitServerEntityResponseAsync<PluginManagerModule, KeyValuePair<bool, string>>((pmm, setResponse) =>
            {
                pmm.CallRpcDownloadPlugin(attachment.Url, setResponse);
            });

            if (!result.Key)
            {
                await response.ModifyOriginalResponseAsync(msg =>
                    msg.Content = $":x: Chyba: `{result.Value ?? "Neznámá chyba!"}`");
            }
            else
            {
                await response.ModifyOriginalResponseAsync(msg =>
                    msg.Content = $":white_check_mark: Plugin `{name}` úspěšně přidán na server.");
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"An error occured while running command:\n{ex}");
        }
    }

    /// <summary>
    /// Removes a specified plugin from the server.
    /// </summary>
    /// <param name="name">The name of the plugin to be removed.</param>
    /// <returns>A task that represents the asynchronous operation of removing the plugin.</returns>
    [SlashCommand("removeplugin", "Odebere plugin ze serveru.")]
    public async Task RemoveAsync([Summary("Název", "Název pluginu co se má odstranit.")] string name)
    {
        try
        {
            if (!Context.HasPermission("ManagePlugins"))
            {
                await RespondAsync(":x: | Nemáš povolení na tento příkaz.", ephemeral: true);
                return;
            }

            await DeferAsync(true);
            
            var result = await Context.AwaitServerEntityResponseAsync<PluginManagerModule, bool>((pmm, setResponse) =>
            {
                pmm.CallRpcRemovePlugin(name, setResponse);
            });

            if (!result)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = ":x: | Plugin nebyl nalezen.");
            }
            else
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $":white_check_mark: | Plugin `{name}` odstraněn!");
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"An error occured while running command:\n{ex}");
        }
    }
}