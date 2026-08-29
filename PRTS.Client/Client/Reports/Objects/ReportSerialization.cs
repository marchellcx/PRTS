using NiveraAPI.IO.Serialization;
using PRTS.Client.Reports.Enums;

namespace PRTS.Client.Reports.Objects;

/// <summary>
/// Provides serialization and deserialization methods for <see cref="ReportInfo"/> objects.
/// </summary>
public static class ReportSerialization
{
    static ReportSerialization()
    {
        ByteSerializer<ReportStatus>.Deserialize = reader => (ReportStatus)reader.ReadByte();
    }

    /// <summary>
    /// Deserializes a <see cref="ReportInfo"/> object from the provided <see cref="ByteReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="ByteReader"/> to read serialized data from.</param>
    /// <returns>A new instance of <see cref="ReportInfo"/> with the deserialized data.</returns>
    public static ReportInfo DeserializeReportInfo(ByteReader reader)
    {
        var info = new ReportInfo();
        
        info.Id = reader.ReadString();
        info.StaffId = reader.ReadString();
        info.ReporterId = reader.ReadString();
        info.ReportedId = reader.ReadString();
        info.ServerId = reader.ReadString();

        info.SubmittedAt = reader.ReadDate();
        info.ResolvedAt = reader.ReadDate();

        info.Status = (ReportStatus)reader.ReadByte();

        info.ReporterRole = reader.ReadString();
        info.ReportedRole = reader.ReadString();
        
        info.Reason = reader.ReadString();
        info.StaffResponse = reader.ReadString();
        
        info.CachedMessageId = reader.ReadString();
        return info;
    }
}