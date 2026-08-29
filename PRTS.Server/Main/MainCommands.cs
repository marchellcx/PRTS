using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using Fergun.Interactive;
using Fergun.Interactive.Pagination;

using Newtonsoft.Json;

using NiveraAPI.Steam;
using NiveraAPI.Console;
using NiveraAPI.Utilities;

using PRTS.Discord;
using PRTS.Profiles;
using PRTS.Database;
using PRTS.Extensions;
using PRTS.RoleSync;
using PRTS.Staff;

namespace PRTS.Main;

public class MainCommands : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    /// Displays the profile of the user.
    /// </summary>
    [SlashCommand("profile", "Informace o profilu.")]
    public async Task ProfileAsync(
        [Summary("Hráč", "Hráč kterého chceš vidět profil.")] SocketUser? user = null)
    {
        user ??= Context.User;
        
        if (!Context.TryGetBot(out var bot))
        {
            await RespondAsync(":x: | Bot nenalezen!", ephemeral: true);
            return;
        }

        if (!ProfileManager.TryGetProfile(x => x.DiscordId == user.Id, out var profile))
        {
            await RespondAsync($":x: | Uživatel {user.Mention} nemá propojený profil!", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        var builders = new List<IPageBuilder>();
        var builder = new StaticPaginatorBuilder();
        var page = new PageBuilder();
        var info = await SteamClient.GetProfileInfoAsync(profile.Value.UserId.Split('@')[0]);

        var totalPlaytime = profile.Value.GetTotalPlaytime();
        var monthPlaytime = profile.Value.GetTotalPlaytime(new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);

        page.WithTitle(":globe_with_meridians: | Profil");
        page.WithColor(Color.Blue);
        page.WithCurrentTimestamp();
        page.WithFooter($"ID: {profile.Value.Id}");

        if (!string.IsNullOrEmpty(info?.AvatarFullUrl))
            page.WithAuthor(profile.Value.GetNickname(), info.AvatarFullUrl,
                $"https://steamcommunity.com/profiles/{profile.Value.UserId.Split('@')[0]}");
        else
            page.WithAuthor(profile.Value.GetNickname(), null,
                $"https://steamcommunity.com/profiles/{profile.Value.UserId.Split('@')[0]}");

        page.AddField(":clock1: Čas na serveru (celkem)", totalPlaytime.ToFullCzechString());
        page.AddField(":calendar: Čas na serveru (tento měsíc)", monthPlaytime.ToFullCzechString());

        builders.Add(page);

        var factory = new Func<PageBuilder>(() =>
        {
            var builder = new PageBuilder();

            builder.WithTitle(":globe_with_meridians: | Profil");
            builder.WithColor(Color.Blue);
            builder.WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(info?.AvatarFullUrl))
                builder.WithAuthor(profile.Value.GetNickname(), info.AvatarFullUrl,
                    $"https://steamcommunity.com/profiles/{profile.Value.UserId.Split('@')[0]}");
            else
                builder.WithAuthor(profile.Value.GetNickname(), null,
                    $"https://steamcommunity.com/profiles/{profile.Value.UserId.Split('@')[0]}");

            return builder;
        });

        ProfileManager.InvokeEmbedBuilder(profile.Value, factory, builders);

        builder.WithUsers(Context.User);
        builder.WithPages(builders);

        await bot.Fergun.SendPaginatorAsync(builder.Build(), Context.Channel);
    }

    /// <summary>
    /// Adds permissions to a staff role.
    /// </summary>
    /// <param name="role">The role associated with the staff role.</param>
    /// <param name="permissions">A comma-separated list of permissions to be added.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("addperms", "Přidá permise staff roli.")]
    public async Task AddPermissionsAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role,
        [Summary("Permise", "Seznam permisí které přidat (oddělené čárkou).")] string permissions)
    {
        if (!Context.HasPermission("ManageStaff"))
        {
            await RespondAsync(":x: | Nemáš práva na správu rolí.", ephemeral: true);
            return;
        }

        if (!StaffRole.TryGetBoundRole(role.Id, out var boundRole))
        {
            await RespondAsync($":x: | Žádná role není propojená s rolí `{role.Name}`", ephemeral: true);
            return;
        }
        
        boundRole.Value.Permissions = boundRole.Value.Permissions.Concat(permissions.Split(',')).ToArray();
        boundRole.IsDirty = true;

        await RespondAsync($":white_check_mark: | Roli `{boundRole.Value.Id}` byly přidány permise.");
    }
    
    /// <summary>
    /// Adds permissions to a staff role.
    /// </summary>
    /// <param name="role">The role associated with the staff role.</param>
    /// <param name="permissions">A comma-separated list of permissions to be added.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("delperms", "Odebere permise staff roli.")]
    public async Task DeletePermissionsAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role,
        [Summary("Permise", "Seznam permisí které odebrat (oddělené čárkou).")] string permissions)
    {
        if (!Context.HasPermission("ManageStaff"))
        {
            await RespondAsync(":x: | Nemáš práva na správu rolí.", ephemeral: true);
            return;
        }

        if (!StaffRole.TryGetBoundRole(role.Id, out var boundRole))
        {
            await RespondAsync($":x: | Žádná role není propojená s rolí `{role.Name}`", ephemeral: true);
            return;
        }
        
        boundRole.Value.Permissions = boundRole.Value.Permissions.Except(permissions.Split(',')).ToArray();
        boundRole.IsDirty = true;

        await RespondAsync($":white_check_mark: | Roli `{boundRole.Value.Id}` byly odebrány permise.");
    }
    
    /// <summary>
    /// Removes a staff role that is bound to the specified Discord role.
    /// </summary>
    /// <param name="role">The Discord role that is associated with the staff role to be removed.</param>
    /// <returns>A task representing the asynchronous operation of removing the staff role.</returns>
    [SlashCommand("remove", "Odstraní staff roli.")]
    public async Task RemoveAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role)
    {
        if (!Context.HasPermission("ManageStaff"))
        {
            await RespondAsync(":x: | Nemáš práva na správu rolí.", ephemeral: true);
            return;
        }

        if (!StaffRole.TryGetBoundRole(role.Id, out var boundRole))
        {
            await RespondAsync($":x: | Žádná role není propojená s rolí `{role.Name}`", ephemeral: true);
            return;
        }

        if (StaffRole.Roles.RemoveStorageValue(boundRole.Value.Id, true))
        {
            await RespondAsync($":white_check_mark: | Role `{boundRole.Value.Id}` odstraněna!", ephemeral: true);
            return;
        }

        await RespondAsync($":x: | Roli `{boundRole.Value.Id}` se nepodařilo odstranit.", ephemeral: true);
    }

    /// <summary>
    /// Creates or updates a staff role with the specified properties, including binding it to a Discord role,
    /// setting administrative privileges, and defining custom permissions.
    /// </summary>
    /// <param name="role">The Discord role to bind with the staff role.</param>
    /// <param name="isAdmin">Indicates whether members of the staff role have administrative privileges.</param>
    /// <param name="perms">A comma-separated string representing the permissions associated with the staff role.</param>
    /// <returns>A task representing the asynchronous operation of creating or updating the staff role.</returns>
    [SlashCommand("create", "Vytvoří staff roli.")]
    public async Task CreateAsync(
        [Summary("Role", "Role propojená se staff rolí.")]
        SocketRole role,
        [Summary("Administrátor", "Zda jsou lidi s touto rolí administrátoři.")] bool isAdmin,
        [Summary("Permise", "Seznam permisí (oddělené pomocí čárky)")] string perms = "")
    {
        if (!Context.HasPermission("ManageStaff"))
        {
            await RespondAsync($":x: | Nemáš práva na správu rolí.", ephemeral: true);
            return;
        }

        if (StaffRole.TryGetBoundRole(role.Id, out var boundRole))
        {
            boundRole.Value.Permissions = perms.Split(',');
            boundRole.Value.IsAdministrator = isAdmin;
            boundRole.IsDirty = true;

            await RespondAsync($":white_check_mark: | Role `{boundRole.Value.Id}` byla aktualizována.", ephemeral: true);
        }
        else
        {
            var staffRole = new StaffRole
            {
                Id = DbManager.NewId,
                Permissions = perms.Split(','),
                IsAdministrator = isAdmin
            };

            StaffRole.Roles.AddStorageValue(staffRole.Id, () => staffRole);

            await RespondAsync($":white_check_mark: | Role `{staffRole.Id}` byla vytvořena.", ephemeral: true);
        }
    }
    
        /// <summary>
    /// Adds a remote role and associates it with a list of server roles.
    /// This method synchronizes a specified role with one or more roles on the Discord server,
    /// enabling users with the specified roles to access the remote role.
    /// </summary>
    /// <param name="remoteRole">The name of the remote role to be added and synchronized.</param>
    /// <param name="roleId">A list of server roles that will be associated with the remote role.</param>
    /// <returns>A task that represents the asynchronous operation of adding the synchronized role.</returns>
    [SlashCommand("syncrole", "Přidá roli na serveru.")]
    public async Task SyncRoleAsync(
        [Summary("Group", "Název skupiny která bude přidělena na serveru.")] string remoteRole, 
        [Summary("Role", "Role která bude v synchronizaci.")] SocketRole roleId)
    {
        if (!Context.HasPermission("ManageSyncRoles"))
        {
            await RespondAsync(":x: Nemáš povolení na tento příkaz.", ephemeral: true);
            return;
        }

        if (RoleSyncRoles.TryAddRole(roleId.Id, remoteRole))
            await RespondAsync($":white_check_mark: Role `{remoteRole}` úspěšně vytvořena!", ephemeral: true);
        else
            await RespondAsync(":x: Nastala chyba při vytváření role.", ephemeral: true);
    }
    
    /// <summary>
    /// Adds a remote role and associates it with a list of server roles.
    /// This method synchronizes a specified role with one or more roles on the Discord server,
    /// enabling users with the specified roles to access the remote role.
    /// </summary>
    /// <param name="remoteRole">The name of the remote role to be added and synchronized.</param>
    /// <param name="role">A list of server roles that will be associated with the remote role.</param>
    /// <returns>A task that represents the asynchronous operation of adding the synchronized role.</returns>
    [SlashCommand("unsyncrole", "Odebere roli na serveru.")]
    public async Task UnsyncRoleAsync(
        [Summary("Group", "Název skupiny která bude přidělena na serveru.")] string remoteRole, 
        [Summary("Role", "Role kterou odebrat ze synchronizace.")] SocketRole role)
    {
        if (!Context.HasPermission("ManageSyncRoles"))
        {
            await RespondAsync(":x: Nemáš povolení na tento příkaz.", ephemeral: true);
            return;
        }

        if (RoleSyncRoles.TryAddRole(role.Id, remoteRole))
            await RespondAsync($":white_check_mark: Role `{remoteRole}` úspěšně vytvořena!", ephemeral: true);
        else
            await RespondAsync(":x: Nastala chyba při vytváření role.", ephemeral: true);
    }
    
    /// <summary>
    /// Handles the process of linking a Discord account to a Steam account.
    /// This method checks if the user has already linked a Steam account and responds accordingly.
    /// If no account is linked, it generates a Steam authentication link, sends it to the user,
    /// waits for the linking process to complete, and stores the connection in persistent storage.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of the linking process.</returns>
    [SlashCommand("link", "Slouží k propojení Discord účtu se Steam účtem.")]
    public async Task LinkAsync()
    {
        try
        {
            if (ProfileManager.TryGetProfile(x => x.DiscordId == Context.User.Id, out var profile))
            {
                await RespondAsync($"Již máš propojený účet (`{profile.Value.UserId}`)!", ephemeral: true);
                return;
            }

            var complete = false;
            var id = string.Empty;
            
            var comps = new ComponentBuilder();
            var button = new ButtonBuilder();

            var link = await ThreadHelper.RunOnMainThread(() =>
            {
                return Network.SteamAuth.Create(s =>
                {
                    id = s.SteamId;
                    complete = true;
                }, null, "Prihlasen pomoci SteamID %ID%! Nyni muzes toto okno zavrit.").Url;
            });

            button.WithUrl(link)
                .WithLabel("Přihlášení přes Steam")
                .WithStyle(ButtonStyle.Link);

            comps.WithButton(button);

            await RespondAsync("Přihlaš se přes Steam pomocí tlačítka na této zprávě.", null, false,
                true, AllowedMentions.None, null, comps.Build());

            while (!complete)
                await Task.Delay(100);

            id = string.Concat(id, "@steam");

            if (!ProfileManager.TryGetProfileByUserId(id, out profile))
                profile = ProfileManager.AddProfileWithUserId(id);

            profile.Value.DiscordId = Context.User.Id;
            profile.IsDirty = true;
            
            await ModifyOriginalResponseAsync(msg => msg.Content = $":white_check_mark: | Přihlášen pomocí SteamID `{id}`!");
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"Error while linking Discord account to Steam account (User={Context.User.GlobalName}@{Context.User.Id}, Guild={Context.Guild.Name}@{Context.Guild.Id}, Channel={Context.Channel.Name}@{Context.Channel.Id}):\n{ex}");
            
            if (Context.Interaction.HasResponded)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = ":x: | Nastala chyba při propojování účtu.");
            }
            else
            {
                await RespondAsync(":x: | Nastala chyba při propojování účtu.", ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Handles the process of unlinking a Discord account from a Steam account.
    /// This method checks if a Steam account is currently linked to the user's Discord account.
    /// If a link exists, it removes the connection and confirms the unlinking.
    /// If no link exists, it notifies the user that no Steam account is linked.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of the unlinking process.</returns>
    [SlashCommand("unlink", "Slouží ke zrušení propojení se Steam účtem.")]
    public async Task UnlinkAsync()
    {
        try
        {
            if (ProfileManager.TryGetProfile(x => x.DiscordId == Context.User.Id, out var profile))
            {
                profile.Value.DiscordId = 0;
                profile.IsDirty = true;
                
                await RespondAsync(":white_check_mark: | Propojení se Steam účtem úspěšně zrušeno!", ephemeral: true);
            }
            else
            {
                await RespondAsync(":x: | Nemáš propojený Steam účet!", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"Error while unlinking Discord account from Steam account (User={Context.User.GlobalName}@{Context.User.Id}, Guild={Context.Guild.Name}@{Context.Guild.Id}, Channel={Context.Channel.Name}@{Context.Channel.Id}):\n{ex}");
            
            if (Context.Interaction.HasResponded)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = ":x: | Nastala chyba při rušení propojení účtu.");
            }
            else
            {
                await RespondAsync(":x: | Nastala chyba při rušení propojení účtu.", ephemeral: true);
            }
        }
    }
}