using NiveraAPI.IO.Serialization;

using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Objects;

/// <summary>
/// Provides static methods for serializing and deserializing instances of the <c>PunishmentInfo</c> class.
/// </summary>
public static class PunishmentSerialization
{
    /// <summary>
    /// Serializes the provided <c>PunishmentInfo</c> object into the specified byte writer.
    /// </summary>
    /// <param name="writer">The <c>ByteWriter</c> instance used to write the serialized punishment data.</param>
    /// <param name="info">The <c>PunishmentInfo</c> object containing the punishment details to be serialized.</param>
    public static void WritePunishmentInfo(ByteWriter writer, PunishmentInfo info)
    {
        writer.WriteString(info.Id);
        writer.WriteString(info.StaffId);
        writer.WriteString(info.TargetId);
        writer.WriteString(info.ReportId);
        writer.WriteString(info.ServerId);
        
        writer.WriteString(info.RevokedId);
        writer.WriteString(info.RevokedReason);
        
        writer.WriteString(info.Reason);
        writer.WriteString(info.CachedMessageId);
        
        writer.WriteArray(info.AppliedServers);
        
        writer.WriteDate(info.IssuedAt);
        writer.WriteDate(info.ExpiresAt);
        writer.WriteDate(info.RevokedAt);
        
        writer.WriteByte((byte)info.Type);
        writer.WriteByte((byte)info.Status);
    }

    /// <summary>
    /// Reads punishment information from the provided byte reader and returns a populated <c>PunishmentInfo</c> object.
    /// </summary>
    /// <param name="reader">The <c>ByteReader</c> instance used to deserialize the punishment data.</param>
    /// <returns>A <c>PunishmentInfo</c> object containing the deserialized punishment details.</returns>
    public static PunishmentInfo ReadPunishmentInfo(ByteReader reader)
    {
        var info = new PunishmentInfo();

        info.Id = reader.ReadString();
        info.StaffId = reader.ReadString();
        info.TargetId = reader.ReadString();
        info.ReportId = reader.ReadString();
        info.ServerId = reader.ReadString();

        info.RevokedId = reader.ReadString();
        info.RevokedReason = reader.ReadString();

        info.Reason = reader.ReadString();
        info.CachedMessageId = reader.ReadString();

        info.AppliedServers = reader.ReadArray<string>();

        info.IssuedAt = reader.ReadDate();
        info.ExpiresAt = reader.ReadDate();
        info.RevokedAt = reader.ReadDate();

        info.Type = (PunishmentType)reader.ReadByte();
        info.Status = (PunishmentStatus)reader.ReadByte();

        return info;
    }
}