using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using NiveraAPI.Logs;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Network.Entities;

using PRTS.ScpSl;
using PRTS.ScpSl.Discord;
using PRTS.Staff;
using System.Net.NetworkInformation;

namespace PRTS.Discord;

public static class DiscordExtensions
{
    /// <summary>
    /// Sends a request to a server-side entity and waits for a response within an optional timeout period.
    /// </summary>
    /// <param name="ctx">The interaction context used to access the associated server entity.</param>
    /// <param name="sendRequest">An action that sends the request to a specific <typeparamref name="TEntity"/> and
    /// accepts a callback for the response of type <typeparamref name="TResponse"/>.</param>
    /// <param name="maxWait">An optional maximum timespan to wait for a response before timing out. Defaults to no timeout if not specified.</param>
    /// <typeparam name="TEntity">The type of the server entity the request is directed to.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected from the server entity.</typeparam>
    /// <returns>The response of type <typeparamref name="TResponse"/> from the server entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified server entity is not found.</exception>
    /// <exception cref="TimeoutException">Thrown if the response is not received within the specified timeout period.</exception>
    public static async Task<TResponse> AwaitServerEntityResponseAsync<TEntity, TResponse>(
        this SocketInteractionContext ctx,
        Action<TEntity, Action<TResponse>> sendRequest, TimeSpan? maxWait = null) where TEntity : Entity
    {
        if (!ctx.TryGetServerEntity<TEntity>(out var entity))
            throw new InvalidOperationException("Entity not found");

        var complete = false;
        var response = default(TResponse);
        
        var setResponse = new Action<TResponse>(x =>
        {
            response = x;
            complete = true;
        });

        await ThreadHelper.RunOnMainThread(() => { sendRequest(entity, setResponse); });
        
        var start = DateTime.Now;

        while (!complete)
        {
            await Task.Delay(100);
            
            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Server entity response timed out");
        }

        return response;   
    }

    /// <summary>
    /// Sends a request to a specified server-side entity and waits for its response within an optional timeout period.
    /// </summary>
    /// <param name="ctx">The interaction context that provides access to the associated Discord server and entities.</param>
    /// <param name="sendRequest">An action used to send the request to a specific server entity of type <typeparamref name="TEntity"/>.
    /// This action also provides a callback to signal when the response has been received.</param>
    /// <param name="maxWait">An optional timespan that specifies the maximum duration to wait for a response. Defaults to no timeout if not provided.</param>
    /// <typeparam name="TEntity">The type of the server entity the request is being sent to.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified server entity could not be found.</exception>
    /// <exception cref="TimeoutException">Thrown if the response was not received within the specified timeout duration.</exception>
    public static async Task AwaitServerEntityResponseAsync<TEntity>(this SocketInteractionContext ctx,
        Action<TEntity, Action> sendRequest, TimeSpan? maxWait = null) where TEntity : Entity
    {
        if (!ctx.TryGetServerEntity<TEntity>(out var entity))
            throw new InvalidOperationException("Entity not found");

        var complete = false;
        var setResponse = new Action(() => { complete = true; });

        await ThreadHelper.RunOnMainThread(() => { sendRequest(entity, setResponse); });
        
        var start = DateTime.Now;

        while (!complete)
        {
            await Task.Delay(100);
            
            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Server entity response timed out");
        }
    }
    
    /// <summary>
    /// Attempts to retrieve a DiscordBot instance associated with the client from the provided interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client to match with a bot.</param>
    /// <param name="bot">When this method returns, contains the associated DiscordBot instance if a match is found, or null if no match is found.</param>
    /// <returns>True if a matching bot is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client in the interaction context is null.</exception>
    public static bool TryGetBot(this SocketInteractionContext ctx, out DiscordBot bot)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (ctx.Client == null)
            throw new InvalidOperationException("Client is null!");
        
        bot = null!;

        foreach (var kvp in DiscordBot.Bots)
        {
            if (kvp.Value.Client == null)
                continue;

            if (kvp.Value.Client == ctx.Client)
            {
                bot = kvp.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve a DiscordBot instance of the specified type associated with the client from the provided interaction context.
    /// </summary>
    /// <typeparam name="T">The type of DiscordBot to retrieve.</typeparam>
    /// <param name="ctx">The interaction context containing the Discord client to match with a bot.</param>
    /// <param name="bot">When this method returns, contains the associated DiscordBot instance if a match is found, or null if no match is found.</param>
    /// <returns>True if a matching bot of the specified type is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client in the interaction context is null.</exception>
    public static bool TryGetBot<T>(this SocketInteractionContext ctx, out T bot) where T : DiscordBot
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (ctx.Client == null)
            throw new InvalidOperationException("Client is null!");

        bot = null!;

        foreach (var kvp in DiscordBot.Bots)
        {
            if (kvp.Value.Client == null)
                continue;

            if (kvp.Value.Client == ctx.Client
                && kvp.Value is T castBot)
            {
                bot = castBot;
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Attempts to retrieve an SCP:SL server based on the client from the provided interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client to be matched with a server.</param>
    /// <param name="server">When this method returns, contains the matching SCP:SL server if the operation succeeds, or null if no server is found.</param>
    /// <returns>True if a matching server is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client in the interaction context is null.</exception>
    public static bool TryGetServer(this SocketInteractionContext ctx, out ScpSlServer server)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (ctx.Client == null)
            throw new InvalidOperationException("Client is null!");
        
        server = null!;

        if (!ctx.TryGetBot<ScpSlBot>(out var bot))
            return false;

        if (bot.Server == null)
            return false;

        server = bot.Server;
        return true;
    }

    /// <summary>
    /// Attempts to retrieve an entity of type <typeparamref name="T"/> from the SCP:SL server associated with the provided interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client associated with the server.</param>
    /// <param name="entity">When this method returns, contains the first matching entity of type <typeparamref name="T"/> if found, or null if no such entity exists.</param>
    /// <typeparam name="T">The type of the entity to retrieve, which must derive from <see cref="Entity"/>.</typeparam>
    /// <returns>True if an entity of the specified type is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context is null.</exception>
    public static bool TryGetServerEntity<T>(this SocketInteractionContext ctx, out T entity) where T : Entity
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        entity = null!;

        if (!ctx.TryGetServer(out var server))
            return false;
        
        return server.Manager.TryGetFirstEntity(out entity);
    }

    /// <summary>
    /// Attempts to retrieve a server entity of the specified type based on the provided interaction context and entity ID.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client to retrieve the server entity from.</param>
    /// <param name="entityId">The unique identifier of the entity to retrieve.</param>
    /// <param name="entity">When this method returns, contains the server entity of the specified type if found; otherwise, null.</param>
    /// <typeparam name="T">The type of the server entity to retrieve, which must derive from Entity.</typeparam>
    /// <returns>True if the specified server entity is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context is null.</exception>
    public static bool TryGetServerEntity<T>(this SocketInteractionContext ctx, ushort entityId, out T entity)
        where T : Entity
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        entity = null!;

        if (!ctx.TryGetServer(out var server))
            return false;
        
        return server.Manager.TryGetEntity(entityId, out entity);
    }

    /// <summary>
    /// Sends a modal response to the interaction context and registers a menu handler for processing subsequent component interactions.
    /// </summary>
    /// <param name="ctx">The interaction context associated with the Discord event, used to respond to the interaction.</param>
    /// <param name="builder">The modal builder used to create and configure the modal dialog to be sent to the user.</param>
    /// <param name="menuHandler">The action that handles interactions with components in the menu, receiving the initiating message component and an optional updated state component.</param>
    /// <returns>A task representing the asynchronous operation of sending the modal response and registering the menu handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ctx"/>, <paramref name="builder"/>, or <paramref name="menuHandler"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="builder"/> does not have a custom ID defined.</exception>
    public static async Task RespondMenuAsync(this SocketInteractionContext ctx, ModalBuilder builder, string menuId,
        Func<SocketModal, SocketMessageComponentData, Task?> menuHandler)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        
        if (menuHandler == null)
            throw new ArgumentNullException(nameof(menuHandler));
        
        if (string.IsNullOrEmpty(menuId))
            throw new ArgumentNullException(nameof(menuId));
        
        if (string.IsNullOrEmpty(builder.CustomId))
            throw new ArgumentException("Modal builder must have a custom ID", nameof(builder));
        
        var id = string.Concat(builder.CustomId, "_", DateTime.Now.Ticks);

        builder.WithCustomId(id);

        DiscordBot.menus.TryAdd(id, modal =>
        {
            if (!modal.Data.Components.TryGetFirst(x => x.CustomId == menuId, out var menu))
            {
                Log.Warn($"Menu &1{menuId}&r not found in modal &1{builder.CustomId}&r!");
                return null;
            }

            return menuHandler(modal, menu);
        });

        await ctx.Interaction.RespondWithModalAsync(builder.Build());
    }

    /// <summary>
    /// Sends a modal response to the interaction context and registers a menu handler for processing subsequent component interactions.
    /// </summary>
    /// <param name="ctx">The message component context containing the Discord client and interaction data.</param>
    /// <param name="builder">The modal builder used to define the modal to be sent to the user.</param>
    /// <param name="menuId">The ID of the menu component within the modal.</param>
    /// <param name="menuHandler">The handler function to process menu interactions.</param>
    /// <returns>A task representing the asynchronous operation of sending the modal response and registering the menu handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context, modal builder, or menu handler is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the modal builder does not have a custom ID.</exception>
    public static async Task RespondMenuAsync(this SocketMessageComponent ctx, ModalBuilder builder, string menuId,
        Func<SocketModal, SocketMessageComponentData, Task?> menuHandler)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (menuHandler == null)
            throw new ArgumentNullException(nameof(menuHandler));

        if (string.IsNullOrEmpty(menuId))
            throw new ArgumentNullException(nameof(menuId));

        if (string.IsNullOrEmpty(builder.CustomId))
            throw new ArgumentException("Modal builder must have a custom ID", nameof(builder));

        var id = string.Concat(builder.CustomId, "_", DateTime.Now.Ticks);

        builder.WithCustomId(id);

        DiscordBot.menus.TryAdd(id, modal =>
        {
            if (!modal.Data.Components.TryGetFirst(x => x.CustomId == menuId, out var menu))
            {
                Log.Warn($"Menu &1{menuId}&r not found in modal &1{builder.CustomId}&r!");
                return null;
            }

            return menuHandler(modal, menu);
        });

        await ctx.RespondWithModalAsync(builder.Build());
    }

    /// <summary>
    /// Sends a modal response to the interaction context and registers a menu handler for processing subsequent component interactions.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="builder">The modal builder used to define the modal to be sent to the user.</param>
    /// <param name="menuHandler">The handler function to process the modal interaction.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context, modal builder, or menu handler is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the modal builder does not have a custom ID.</exception>
    public static async Task RespondMenuAsync(this SocketMessageComponent ctx, ModalBuilder builder, Func<SocketModal, Task?> menuHandler)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (menuHandler == null)
            throw new ArgumentNullException(nameof(menuHandler));

        if (string.IsNullOrEmpty(builder.CustomId))
            throw new ArgumentException("Modal builder must have a custom ID", nameof(builder));

        var id = string.Concat(builder.CustomId, "_", DateTime.Now.Ticks);

        builder.WithCustomId(id);

        DiscordBot.menus.TryAdd(id, menuHandler);
        await ctx.RespondWithModalAsync(builder.Build());
    }

    /// <summary>
    /// Waits for a modal interaction to be completed and retrieves the resulting modal instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="builder">The modal builder used to define the modal to be sent to the user.</param>
    /// <returns>The modal instance containing the interaction response data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context or modal builder is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the modal builder does not have a custom ID.</exception>
    /// <exception cref="Exception">Thrown when no Discord bot is associated with the target client.</exception>
    public static async Task<SocketModal> AwaitModalResponseAsync(this SocketMessageComponent ctx, ModalBuilder builder, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (string.IsNullOrEmpty(builder.CustomId))
            throw new ArgumentException("Modal builder must have a custom ID", nameof(builder));

        var modal = default(SocketModal);
        var id = string.Concat(builder.CustomId, "_", DateTime.Now.Ticks);
        
        builder.WithCustomId(id);

        DiscordBot.modals.TryAdd(id, x =>
        {
            modal = x;
            return Task.CompletedTask;
        });
        
        await ctx.RespondWithModalAsync(builder.Build());
        
        var start = DateTime.Now;

        while (modal == null)
        {
            await Task.Delay(100);
            
            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Modal response timed out");
        }

        return modal;
    }

    /// <summary>
    /// Waits for a button interaction to be completed and retrieves the resulting button component instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="embedBuilder">The embed builder used to define the embed to be sent to the user.</param>
    /// <param name="buttons">The collection of button builders used to define the buttons to be sent to the user.</param>
    /// <param name="ephemeral">Whether the message should be ephemeral (only visible to the user).</param>
    /// <param name="maxWait">The maximum amount of time to wait for a button interaction.</param>
    /// <returns>The button component instance containing the interaction response data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of the required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the button builders do not have a custom ID.</exception>
    /// <exception cref="TimeoutException">Thrown when the button response times out.</exception>
    public static async Task<SocketMessageComponent> AwaitButtonsAsync(this SocketMessageComponent ctx, EmbedBuilder embedBuilder, IEnumerable<ButtonBuilder> buttons, bool ephemeral, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (embedBuilder == null)
            throw new ArgumentNullException(nameof(embedBuilder));

        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        var comp = default(SocketMessageComponent);
        var builder = new ComponentBuilderV2();

        foreach (var button in buttons)
        {
            if (string.IsNullOrEmpty(button.CustomId))
                throw new ArgumentException("Button builder must have a custom ID", nameof(button));

            button.WithCustomId(string.Concat(button.CustomId, DateTime.UtcNow.Ticks));

            DiscordBot.buttons.TryAdd(button.CustomId, x =>
            {
                comp = x;
                return Task.CompletedTask;
            });
        }

        await ctx.FollowupAsync(embed: embedBuilder.Build(), components: builder.Build(), ephemeral: ephemeral);

        var start = DateTime.Now;

        while (comp == null)
        {
            await Task.Delay(100);

            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Button response timed out");
        }

        return comp;
    }

    /// <summary>
    /// Waits for a button interaction to be completed and retrieves the resulting button component instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="msg">The message to be sent with the buttons.</param>
    /// <param name="buttons">The collection of button builders to be included in the message.</param>
    /// <param name="ephemeral">Whether the message should be ephemeral (only visible to the user).</param>
    /// <param name="maxWait">The maximum amount of time to wait for a button interaction.</param>
    /// <returns>The button component instance resulting from the interaction.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the context or buttons collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a button builder does not have a custom ID.</exception>
    /// <exception cref="TimeoutException">Thrown when the button response times out.</exception>
    public static async Task<SocketMessageComponent> AwaitButtonsAsync(this SocketMessageComponent ctx, string msg, IEnumerable<ButtonBuilder> buttons, bool ephemeral, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        var comp = default(SocketMessageComponent);
        var builder = new ComponentBuilderV2();

        foreach (var button in buttons)
        {
            if (string.IsNullOrEmpty(button.CustomId))
                throw new ArgumentException("Button builder must have a custom ID", nameof(button));

            button.WithCustomId(string.Concat(button.CustomId, DateTime.UtcNow.Ticks));

            DiscordBot.buttons.TryAdd(button.CustomId, x =>
            {
                comp = x;
                return Task.CompletedTask;
            });
        }

        await ctx.FollowupAsync(text: msg, components: builder.Build(), ephemeral: ephemeral);

        var start = DateTime.Now;

        while (comp == null)
        {
            await Task.Delay(100);

            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Button response timed out");
        }

        return comp;
    }

    /// <summary>
    /// Waits for a button interaction to be completed and retrieves the resulting button component instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="embedBuilder">The embed builder used to define the embed to be sent to the user.</param>
    /// <param name="buttons">The collection of button builders used to define the buttons to be sent to the user.</param>
    /// <param name="ephemeral">Whether the message should be ephemeral (only visible to the user).</param>
    /// <param name="maxWait">The maximum amount of time to wait for a button interaction.</param>
    /// <returns>The button component instance containing the interaction response data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of the required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the button builders do not have a custom ID.</exception>
    /// <exception cref="TimeoutException">Thrown when the button response times out.</exception>
    public static async Task<SocketMessageComponent> AwaitButtonsAsync(this SocketModal ctx, EmbedBuilder embedBuilder, IEnumerable<ButtonBuilder> buttons, bool ephemeral, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (embedBuilder == null)
            throw new ArgumentNullException(nameof(embedBuilder));

        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        var comp = default(SocketMessageComponent);
        var builder = new ComponentBuilderV2();

        foreach (var button in buttons)
        {
            if (string.IsNullOrEmpty(button.CustomId))
                throw new ArgumentException("Button builder must have a custom ID", nameof(button));

            button.WithCustomId(string.Concat(button.CustomId, DateTime.UtcNow.Ticks));

            DiscordBot.buttons.TryAdd(button.CustomId, x =>
            {
                comp = x;
                return Task.CompletedTask;
            });
        }

        await ctx.FollowupAsync(embed: embedBuilder.Build(), components: builder.Build(), ephemeral: ephemeral);

        var start = DateTime.Now;

        while (comp == null)
        {
            await Task.Delay(100);

            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Button response timed out");
        }

        return comp;
    }

    /// <summary>
    /// Waits for a button interaction to be completed and retrieves the resulting button component instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="msg">The message to be sent with the buttons.</param>
    /// <param name="buttons">The collection of button builders to be included in the message.</param>
    /// <param name="ephemeral">Whether the message should be ephemeral (only visible to the user).</param>
    /// <param name="maxWait">The maximum amount of time to wait for a button interaction.</param>
    /// <returns>The button component instance resulting from the interaction.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the context or buttons collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a button builder does not have a custom ID.</exception>
    /// <exception cref="TimeoutException">Thrown when the button response times out.</exception>
    public static async Task<SocketMessageComponent> AwaitButtonsAsync(this SocketModal ctx, string msg, IEnumerable<ButtonBuilder> buttons, bool ephemeral, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        var comp = default(SocketMessageComponent);
        var builder = new ComponentBuilderV2();

        foreach (var button in buttons)
        {
            if (string.IsNullOrEmpty(button.CustomId))
                throw new ArgumentException("Button builder must have a custom ID", nameof(button));

            button.WithCustomId(string.Concat(button.CustomId, DateTime.UtcNow.Ticks));

            DiscordBot.buttons.TryAdd(button.CustomId, x =>
            {
                comp = x;
                return Task.CompletedTask;
            });
        }

        await ctx.FollowupAsync(text: msg, components: builder.Build(), ephemeral: ephemeral);

        var start = DateTime.Now;

        while (comp == null)
        {
            await Task.Delay(100);

            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Button response timed out");
        }

        return comp;
    }

    /// <summary>
    /// Waits for a modal interaction to be completed and retrieves the resulting modal instance.
    /// </summary>
    /// <param name="ctx">The interaction context containing the Discord client and interaction data.</param>
    /// <param name="builder">The modal builder used to define the modal to be sent to the user.</param>
    /// <returns>The modal instance containing the interaction response data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the interaction context or modal builder is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the modal builder does not have a custom ID.</exception>
    /// <exception cref="Exception">Thrown when no Discord bot is associated with the target client.</exception>
    public static async Task<SocketModal> AwaitModalResponseAsync(this SocketInteractionContext ctx, ModalBuilder builder, TimeSpan? maxWait = null)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (string.IsNullOrEmpty(builder.CustomId))
            throw new ArgumentException("Modal builder must have a custom ID", nameof(builder));

        var modal = default(SocketModal);
        var id = string.Concat(builder.CustomId, "_", DateTime.Now.Ticks);
        
        builder.WithCustomId(id);

        DiscordBot.modals.TryAdd(id, x =>
        {
            modal = x;
            return Task.CompletedTask;
        });
        
        await ctx.Interaction.RespondWithModalAsync(builder.Build());
        
        var start = DateTime.Now;

        while (modal == null)
        {
            await Task.Delay(100);
            
            if (maxWait.HasValue && DateTime.Now - start > maxWait.Value)
                throw new TimeoutException("Modal response timed out");
        }

        return modal;
    }

    /// <summary>
    /// Attempts to retrieve a specific component from the modal's data using its unique identifier.
    /// </summary>
    /// <param name="modal">The modal containing the components to search through.</param>
    /// <param name="componentId">The unique identifier of the component to locate.</param>
    /// <param name="component">The output parameter that will contain the retrieved component if found.</param>
    /// <returns>
    /// True if the component with the specified identifier is found; otherwise, false.
    /// </returns>
    public static bool TryGetComponent(this SocketModal modal, string componentId,
        out SocketMessageComponentData component)
    {
        component = default!;

        if (modal == null)
            return false;

        foreach (var comp in modal.Data.Components.ToList())
        {
            if (string.IsNullOrEmpty(comp.CustomId) || comp.CustomId != componentId)
                continue;
            
            component = comp;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the user associated with the specified interaction context has the given permission.
    /// </summary>
    /// <param name="ctx">The interaction context containing information about the user and interaction.</param>
    /// <param name="perm">The permission string to check for.</param>
    /// <returns>True if the user has the specified permission; otherwise, false.</returns>
    public static bool HasPermission(this SocketInteractionContext ctx, string perm)
    {
        if (ctx?.User == null)
            return false;

        return StaffRole.HasPermissionAll(0, ctx.User.Id, perm);
    }

    /// <summary>
    /// Determines whether the user associated with the specified message component context has the given permission.
    /// </summary>
    /// <param name="ctx">The message component context containing information about the user and interaction.</param>
    /// <param name="perm">The permission string to check for.</param>
    /// <returns>True if the user has the specified permission; otherwise, false.</returns>
    public static bool HasPermission(this SocketMessageComponent ctx, string perm)
    {
        if (ctx?.User == null)
            return false;

        return StaffRole.HasPermissionAll(0, ctx.User.Id, perm);
    }

    /// <summary>
    /// Determines whether the user associated with the specified modal interaction context has the given permission.
    /// </summary>
    /// <param name="ctx">The modal interaction context containing information about the user and interaction.</param>
    /// <param name="perm">The permission string to check for.</param>
    /// <returns>True if the user has the specified permission; otherwise, false.</returns>
    public static bool HasPermission(this SocketModal ctx, string perm)
    {
        if (ctx?.User == null)
            return false;

        return StaffRole.HasPermissionAll(0, ctx.User.Id, perm);
    }
}