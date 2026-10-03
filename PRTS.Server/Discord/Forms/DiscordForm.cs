using Discord;
using Discord.WebSocket;

using NiveraAPI.Extensions;

using PRTS.Discord.Forms.Attributes;

namespace PRTS.Discord.Forms;

public class DiscordForm
{
    public async Task UpdateAsync(SocketModal modal)
    {
        var map = DiscordFormMap.GetOrAdd(GetType());

        foreach (var kvp in map)
        {
            switch (kvp.Value)
            {
                case TextInputAttribute textInputAttribute:
                    {
                        if (!modal.Data.Components.TryGetFirst(x => x.CustomId == kvp.Key.Name, out var textInputData))
                            throw new Exception($"Text input with CustomId '{kvp.Key.Name}' not found in modal data.");

                        kvp.Key.SetValue(this, textInputData.Value);
                        break;
                    }

                case CheckBoxAttribute checkBoxAttribute:
                    {
                        if (!modal.Data.Components.TryGetFirst(x => x.CustomId == kvp.Key.Name, out var checkBoxData))
                            throw new Exception($"Checkbox with CustomId '{kvp.Key.Name}' not found in modal data.");

                        kvp.Key.SetValue(this, checkBoxData.BoolValue);
                        break;
                    }
            }
        }
    }

    public ModalBuilder ToModal()
    {
        var map = DiscordFormMap.GetOrAdd(GetType());
        var builder = new ModalBuilder();

        foreach (var kvp in map)
        {
            switch (kvp.Value)
            {
                case TextInputAttribute textInputAttribute:
                    builder.AddTextInput(textInputAttribute.Label, kvp.Key.Name, textInputAttribute.Style, textInputAttribute.Placeholder, textInputAttribute.MinLength, textInputAttribute.MaxLength, textInputAttribute.IsRequired, null, null, textInputAttribute.Description);
                    break;

                case CheckBoxAttribute checkBoxAttribute:
                    builder.AddCheckBox(checkBoxAttribute.Label, new CheckboxBuilder()
                        .WithCustomId(kvp.Key.Name)
                        .WithDefaultState(checkBoxAttribute.IsChecked), checkBoxAttribute.Description);
                    break;
            }
        }

        return builder;
    }
}