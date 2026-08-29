using NiveraAPI.IO.Serialization;

namespace PRTS.ScpSl.Modules.Reports;

/// <summary>
/// Provides serialization and deserialization methods for <see cref="ReportInfo"/> objects.
/// </summary>
public static class ReportSerialization
{
    /// <summary>
    /// Serializes the provided <see cref="ReportInfo"/> object into the given <see cref="ByteWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="ByteWriter"/> instance to write the serialized data to.</param>
    /// <param name="info">The <see cref="ReportInfo"/> object containing the data to serialize.</param>
    public static void SerializeReportInfo(ByteWriter writer, ReportInfo info)
    {
        writer.WriteString(info.Id);
        writer.WriteString(info.StaffId);
        writer.WriteString(info.ReporterId);
        writer.WriteString(info.ReportedId);
        writer.WriteString(info.ServerId);
        
        writer.WriteDate(info.SubmittedAt);
        writer.WriteDate(info.ResolvedAt);
        
        writer.WriteByte((byte)info.Status);
        
        writer.WriteString(info.ReporterRole);
        writer.WriteString(info.ReportedRole);
        
        writer.WriteString(info.Reason);
        
        writer.WriteString(info.StaffResponse);
        writer.WriteString(info.CachedMessageId);
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