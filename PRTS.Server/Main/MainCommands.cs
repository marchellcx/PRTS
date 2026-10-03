using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using Fergun.Interactive;
using Fergun.Interactive.Pagination;

using NiveraAPI.Steam;
using NiveraAPI.Console;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;

using PRTS.Staff;
using PRTS.Levels;
using PRTS.Discord;
using PRTS.Database;
using PRTS.Profiles;
using PRTS.RoleSync;
using PRTS.Extensions;
using PRTS.Levels.Properties;

namespace PRTS.Main;

/// <summary>
/// Represents the main commands for the Discord bot, providing functionalities such as profile management, staff role management, role synchronization, and account linking with Steam.
/// </summary>
public class MainCommands : InteractionModuleBase<SocketInteractionContext>
{
    #region Profiles
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

        Func<PageBuilder> factory;

        if (!string.IsNullOrEmpty(profile.Value.UserId)
            && profile.Value.UserId.TrySplit('@', true, 2, out var segments)
            && segments[1] == "steam")
        {
            var info = await SteamClient.GetProfileInfoAsync(segments[0]);

            if (!string.IsNullOrEmpty(info?.AvatarFullUrl))
                page.WithAuthor(profile.Value.GetNickname(), info.AvatarFullUrl,
                    $"https://steamcommunity.com/profiles/{segments[0]}");
            else
                page.WithAuthor(profile.Value.GetNickname(), null,
                    $"https://steamcommunity.com/profiles/{segments[0]}");

            factory = new Func<PageBuilder>(() =>
            {
                var builder = new PageBuilder();

                builder.WithTitle(":globe_with_meridians: | Profil");
                builder.WithColor(Color.Blue);
                builder.WithCurrentTimestamp();

                if (!string.IsNullOrEmpty(info?.AvatarFullUrl))
                    builder.WithAuthor(profile.Value.GetNickname(), info.AvatarFullUrl,
                        $"https://steamcommunity.com/profiles/{segments[0]}");
                else
                    builder.WithAuthor(profile.Value.GetNickname(), null,
                        $"https://steamcommunity.com/profiles/{segments[0]}");

                return builder;
            });
        }
        else
        {
            page.WithAuthor(user.GlobalName ?? user.Username, user.GetDisplayAvatarUrl() ?? (user.GetDisplayAvatarUrl() ?? user.GetDefaultAvatarUrl()));

            factory = new Func<PageBuilder>(() =>
            {
                var builder = new PageBuilder();

                builder.WithTitle(":globe_with_meridians: | Profil");
                builder.WithColor(Color.Blue);
                builder.WithCurrentTimestamp();
                builder.WithAuthor(user.GlobalName ?? user.Username, user.GetDisplayAvatarUrl() ?? (user.GetDisplayAvatarUrl() ?? user.GetDefaultAvatarUrl()));

                return builder;
            });
        }

        var totalPlaytime = profile.Value.GetTotalPlaytime();

        var dayPlaytime = profile.Value.GetTotalPlaytime(DateTimeExtensions.DayStart, DateTimeExtensions.DayEnd);
        var weekPlaytime = profile.Value.GetTotalPlaytime(DateTimeExtensions.WeekStart, DateTimeExtensions.WeekEnd);
        var monthPlaytime = profile.Value.GetTotalPlaytime(DateTimeExtensions.MonthStart, DateTimeExtensions.MonthEnd);

        page.WithTitle(":globe_with_meridians: | Profil");
        page.WithColor(Color.Blue);
        page.WithCurrentTimestamp();

        page.AddField(":man_detective: První připojení", profile.Value.CreatedAt.ToLocalTime().ToVeCzechString());
        page.AddField(":knot: Poslední připojení", profile.Value.LastLogin.ToLocalTime().ToVeCzechString());

        page.AddField(":clock1: Čas na serveru (celkem)", totalPlaytime.ToFullCzechString());
        page.AddField(":calendar: Čas na serveru (dnes)", dayPlaytime.ToFullCzechString());
        page.AddField(":calendar: Čas na serveru (tento týden)", weekPlaytime.ToFullCzechString());
        page.AddField(":calendar: Čas na serveru (tento měsíc)", monthPlaytime.ToFullCzechString());

        builders.Add(page);

        ProfileManager.InvokeEmbedBuilder(profile.Value, factory, builders);

        builder.WithUsers(Context.User);
        builder.WithPages(builders);

        await bot.Fergun.SendPaginatorAsync(builder.Build(), Context.Interaction, null, InteractionResponseType.ChannelMessageWithSource, true);
    }
    #endregion

    #region Staff Roles
    /// <summary>
    /// Adds permissions to a staff role.
    /// </summary>
    /// <param name="role">The role associated with the staff role.</param>
    /// <param name="permissions">A comma-separated list of permissions to be added.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("addstaffperms", "Přidá permise staff roli.")]
    public async Task AddStaffPermissionsAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role,
        [Summary("Permise", "Seznam permisí které přidat (oddělené čárkou).")] string permissions)
    {
        if (!Context.HasPermission(Permissions.ManageStaff))
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
    [SlashCommand("delstaffperms", "Odebere permise staff roli.")]
    public async Task DeleteStaffPermissionsAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role,
        [Summary("Permise", "Seznam permisí které odebrat (oddělené čárkou).")] string permissions)
    {
        if (!Context.HasPermission(Permissions.ManageStaff))
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
    [SlashCommand("removestaffrole", "Odstraní staff roli.")]
    public async Task RemoveStaffRoleAsync(
        [Summary("Role", "Role propojená se staff rolí.")] SocketRole role)
    {
        if (!Context.HasPermission(Permissions.ManageStaff))
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
    [SlashCommand("createstaffrole", "Vytvoří staff roli.")]
    public async Task CreateStaffRoleAsync(
        [Summary("Role", "Role propojená se staff rolí.")]
        SocketRole role,
        [Summary("Administrátor", "Zda jsou lidi s touto rolí administrátoři.")] bool isAdmin,
        [Summary("Permise", "Seznam permisí (oddělené pomocí čárky)")] string perms = "")
    {
        if (!Context.HasPermission(Permissions.ManageStaff))
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
                IsAdministrator = isAdmin,
                RoleIds = [role.Id]
            };

            StaffRole.Roles.AddStorageValue(staffRole.Id, () => staffRole);

            await RespondAsync($":white_check_mark: | Role `{staffRole.Id}` byla vytvořena.", ephemeral: true);
        }
    }
    #endregion

    #region Role Sync
    /// <summary>
    /// Adds a remote role and associates it with a list of server roles.
    /// This method synchronizes a specified role with one or more roles on the Discord server,
    /// enabling users with the specified roles to access the remote role.
    /// </summary>
    /// <param name="remoteRole">The name of the remote role to be added and synchronized.</param>
    /// <param name="roleId">A list of server roles that will be associated with the remote role.</param>
    /// <returns>A task that represents the asynchronous operation of adding the synchronized role.</returns>
    [SlashCommand("addsyncrole", "Přidá roli na serveru.")]
    public async Task SyncRoleAsync(
        [Summary("Group", "Název skupiny která bude přidělena na serveru.")] string remoteRole, 
        [Summary("Role", "Role která bude v synchronizaci.")] SocketRole roleId)
    {
        if (!Context.HasPermission(Permissions.ManageSyncRoles))
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
    [SlashCommand("removesyncrole", "Odebere roli na serveru.")]
    public async Task UnsyncRoleAsync(
        [Summary("Group", "Název skupiny která bude přidělena na serveru.")] string remoteRole, 
        [Summary("Role", "Role kterou odebrat ze synchronizace.")] SocketRole role)
    {
        if (!Context.HasPermission(Permissions.ManageSyncRoles))
        {
            await RespondAsync(":x: Nemáš povolení na tento příkaz.", ephemeral: true);
            return;
        }

        if (RoleSyncRoles.TryRemoveRole(role.Id, remoteRole))
            await RespondAsync($":white_check_mark: Role `{remoteRole}` úspěšně odebrána!", ephemeral: true);
        else
            await RespondAsync(":x: Nastala chyba při odebírání role.", ephemeral: true);
    }
    #endregion

    #region Account Linking
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

            if (!ProfileManager.TryGetProfile(x => (!string.IsNullOrEmpty(x.UserId) && x.UserId == id) || x.DiscordId == Context.User.Id, out profile))
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
    #endregion

    #region Levels
    /// <summary>
    /// Resets the experience points (XP) of a specified player.
    /// This command requires the "ModifyLevels" permission.
    /// </summary>
    /// <param name="user">The player whose experience points are to be reset.</param>
    /// <returns>A task that represents the asynchronous operation of resetting the experience points.</returns>
    [SlashCommand("resetxp", "Resetuje XP hráče.")]
    public async Task ResetExperienceAsync(
        [Summary("Hráč", "Hráč, jehož XP chcete resetovat.")] SocketUser user)
    {
        if (!Context.HasPermission("ModifyLevels"))
        {
            await RespondAsync(":x: | Nemáš oprávnění k úpravě levelů.", ephemeral: true);
            return;
        }

        if (!ProfileManager.TryGetProfileByDiscordId(user.Id, out var profile))
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nebyl nalezen.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        if (!profile.Value.TryGetProperty<LevelDataProperty>(LevelManager.DataPropertyName, out var levelData))
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nemá načtená data o levelu.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        var currentExp = levelData.Experience;
        var currentLevel = LevelManager.GetLevelForXp(currentExp);

        var result = LevelManager.ModifyProfileXp(profile, "Reset XP", -currentExp);

        var newExp = levelData.Experience;
        var newLevel = LevelManager.GetLevelForXp(newExp);

        if (result is Levels.Enums.LevelModifyResult.ProfileNotFound)
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nebyl nalezen.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        await RespondAsync($":white_check_mark: | XP uživatele {user.Mention} byly úspěšně resetovány na {newExp} (level {currentLevel.Level} → {newLevel.Level})!", ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    /// Sets the experience points for a specified player.
    /// </summary>
    /// <param name="user">The player whose experience points are to be set.</param>
    /// <param name="experience">The number of experience points to set for the player.</param>
    /// <returns>A task that represents the asynchronous operation of setting the experience points.</returns>
    [SlashCommand("modxp", "Upraví XP hráče.")]
    public async Task SetExperienceAsync(
        [Summary("Hráč", "Hráč, jehož XP chcete změnit.")] SocketUser user,
        [Summary("Důvod", "Důvod, proč se XP hráče mění.")] string reason,
        [Summary("XP", "Počet XP, které chcete přidat nebo odebrat.")] int experience)
    {
        if (!Context.HasPermission(Permissions.EditLevels))
        {
            await RespondAsync(":x: | Nemáš oprávnění k úpravě levelů.", ephemeral: true);
            return;
        }

        if (!ProfileManager.TryGetProfileByDiscordId(user.Id, out var profile))
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nebyl nalezen.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        if (!profile.Value.TryGetProperty<LevelDataProperty>(LevelManager.DataPropertyName, out var levelData))
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nemá načtená data o levelu.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        var currentExp = levelData.Experience;
        var currentLevel = LevelManager.GetLevelForXp(currentExp);

        var result = LevelManager.ModifyProfileXp(profile, reason, experience);

        var newExp = levelData.Experience;
        var newLevel = LevelManager.GetLevelForXp(newExp);

        if (result is Levels.Enums.LevelModifyResult.ProfileNotFound)
        {
            await RespondAsync($":x: | Profil uživatele {user.Mention} nebyl nalezen.", ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        if (newExp > currentExp)
        {
            await RespondAsync($":white_check_mark: | Zkušenosti uživatele {user.Mention} byly úspěšně nastaveny na {experience} - +{newExp - currentExp} (level {currentLevel.Level} → {newLevel.Level})!", ephemeral: true, allowedMentions: AllowedMentions.None);
        }
        else if (newExp < currentExp)
        {
            await RespondAsync($":white_check_mark: | Zkušenosti uživatele {user.Mention} byly úspěšně nastaveny na {experience} - -{currentExp - newExp} (level {currentLevel.Level} → {newLevel.Level})!", ephemeral: true, allowedMentions: AllowedMentions.None);
        }
        else
        {
            await RespondAsync($":white_check_mark: | Zkušenosti uživatele {user.Mention} zůstaly beze změny na {experience} (level {currentLevel.Level}).", ephemeral: true, allowedMentions: AllowedMentions.None);
        }
    }

    /// <summary>
    /// Displays the leaderboard of players based on their levels.
    /// </summary>
    /// <param name="size">The number of players to display in the leaderboard.</param>
    /// <returns>A task that represents the asynchronous operation of displaying the leaderboard.</returns>
    [SlashCommand("leaderboard", "Zobrazí leaderboard hráčů podle levelů.")]
    public async Task LeaderboardAsync(
        [Summary("Velikost", "Počet hráčů zobrazených v leaderboardu.")] [MaxValue(25)] int size)
    {
        var embed = new EmbedBuilder();

        if (!await LevelLeaderboard.EditEmbedAsync(embed, size))
        {
            await RespondAsync(":x: | Nastala chyba při generování leaderboardu (možná nejsou žádní hráči s levely).", ephemeral: true);
            return;
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    /// Sets the channel for the leaderboard message, allowing users to view the leaderboard in a specified text channel.
    /// </summary>
    /// <param name="channel">The text channel where the leaderboard message will be posted.</param>
    /// <returns>A task that represents the asynchronous operation of setting the leaderboard channel.</returns>
    [SlashCommand("leaderboardchannel", "Nastaví kanál pro leaderboard zprávu.")]
    public async Task SetLeaderboardAsync(
        [Summary("Kanál", "Kanál ve kterém bude aktualizována leaderboard zpráva.")] SocketTextChannel channel)
    {
        try
        {
            if (!Context.HasPermission(Permissions.ManageLeaderboard))
            {
                await RespondAsync(":x: | Nemáš práva na správu leaderboardu.", ephemeral: true);
                return;
            }

            var msg = await LevelLeaderboard.PostLeaderboardAsync(channel, true);

            if (msg != null)
                await RespondAsync($":white_check_mark: | Leaderboard zpráva byla úspěšně vytvořena v kanálu {channel.Mention}: {msg.GetJumpUrl()}.", ephemeral: true);
            else
                await RespondAsync(":x: | Nastala chyba při vytváření leaderboard zprávy (možná nejsou žádní hráči s levely).", ephemeral: true);
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write($"Error while setting leaderboard channel (User={Context.User.GlobalName}@{Context.User.Id}, Guild={Context.Guild.Name}@{Context.Guild.Id}, Channel={Context.Channel.Name}@{Context.Channel.Id}):\n{ex}");

            if (Context.Interaction.HasResponded)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = ":x: | Nastala chyba při nastavování kanálu pro leaderboard zprávu.");
            }
            else
            {
                await RespondAsync(":x: | Nastala chyba při nastavování kanálu pro leaderboard zprávu.", ephemeral: true);
            }
        }
    }
    #endregion
}