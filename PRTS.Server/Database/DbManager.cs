using System.Collections.Concurrent;
using System.Reflection;

using NiveraAPI;
using NiveraAPI.Logs;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Serialization;

using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

using PRTS.Discord.MessageCache;

using PRTS.Profiles.Objects;
using PRTS.Punishments.Objects;

using PRTS.ScpSl.Modules.Plugins;
using PRTS.ScpSl.Modules.Reports;

using PRTS.Staff;

namespace PRTS.Database;

/// <summary>
/// Provides functionality for managing the database.
/// </summary>
public static class DbManager
{
    private static volatile LogSink log;
    private static volatile StorageManager storage;
    
    private static volatile ConcurrentDictionary<Type, StorageSerializer> serializers = new();
    private static volatile ConcurrentDictionary<FieldInfo, DbStorageAttribute> storages = new();

    /// <summary>
    /// The database file.
    /// </summary>
    public static StorageManager Storage => storage;

    /// <summary>
    /// Generates a new unique identifier.
    /// </summary>
    public static string NewId => Guid
        .NewGuid()
        .ToString();

    /// <summary>
    /// Starts the database manager.
    /// </summary>
    public static void Start()
    {
        log = LogManager.GetSource("Database", "Manager");
        
        RegisterSerializers();
        
        try
        {
            storages.Clear();
            serializers.Clear();
            
            var path = Path.Combine(Directory.GetCurrentDirectory(), "internal-database");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            FindStorages();

            storage = new(path) { DefaultSerializer = new JsonSerializer() };

            if (LibraryLoader.HasArgument("DatabaseDebug"))
                Storage.DebugLogs = true;

            AssignSerializers();

            storage.Initialize();

            AssignStorages();

            LibraryUpdate.Register(Storage.SaveDirty);
        }
        catch (Exception ex)
        {
            log.Error(ex);
        }
    }

    private static void AssignSerializers()
    {
        foreach (var kvp in storages)
        {
            try
            {
                if (kvp.Value.Serializer == null)
                    continue;
                
                if (string.IsNullOrEmpty(kvp.Value.Name))
                    continue;
                
                if (kvp.Value.Serializer != null)
                {
                    if (!serializers.TryGetValue(kvp.Value.Serializer, out var serializer))
                    {
                        if ((serializer = Activator.CreateInstance(kvp.Value.Serializer) as StorageSerializer) == null)
                        {
                            log.Error($"Failed to create serializer for &1{kvp.Value.Serializer.Name}&r");
                            
                            storages.TryRemove(kvp.Key, out _);
                            continue;
                        }
                        
                        serializers.TryAdd(kvp.Value.Serializer, serializer);
                    }
                    
                    storage.DirectorySerializers.TryAdd(kvp.Value.Name, serializer);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                
                storages.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static void AssignStorages()
    {
        foreach (var kvp in storages)
        {
            try
            {
                if (string.IsNullOrEmpty(kvp.Value.Name))
                    continue;

                var dir = storage.Add(kvp.Value.Name);

                if (dir != null)
                {
                    kvp.Key.SetValue(null, dir);
                }
                else
                {
                    log.Error($"Failed to assign storage &1{kvp.Value.Name}&r to &1{kvp.Key.Name}&r");
                }
                
                if (kvp.Key.DeclaringType != null)
                {
                    var initName = string.Concat("StorageInit_", kvp.Key.Name);
                    var initMethod = kvp.Key.DeclaringType.FindMethod(m => 
                        m.Name == initName 
                        && m.IsStatic
                        && m.ReturnType == typeof(void)
                        && m.GetAllParameters().Length == 0);
                    
                    initMethod?.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
    }
    
    private static void FindStorages()
    {
        var asm = Assembly.GetExecutingAssembly();
        var types = asm.GetTypes();
        
        foreach (var type in types)
        {
            var fields = type.GetAllFields();

            foreach (var field in fields)
            {
                if (!field.HasAttribute<DbStorageAttribute>(out var dbStorageAttribute))
                    continue;

                if (field.IsInitOnly || field.FieldType != typeof(StorageDirectory))
                {
                    log.Warn($"Field &1{field.Name}&r is not a &1StorageDirectory&r!");
                    continue;
                }
                
                if (string.IsNullOrEmpty(dbStorageAttribute.Name))
                    dbStorageAttribute.Name = field.Name;
                
                storages.TryAdd(field, dbStorageAttribute);
            }
        }
    }

    private static void RegisterSerializers()
    {
        ByteSerializer<string[]>.Serialize = (writer, roles) => writer.WriteArray(roles);
        ByteSerializer<string[]>.Deserialize = reader => reader.ReadArray<string>();
        
        ByteSerializer<ReportInfo>.Serialize = ReportSerialization.SerializeReportInfo;
        ByteSerializer<ReportInfo>.Deserialize = ReportSerialization.DeserializeReportInfo;   
        
        ByteSerializer<ReportStatus>.Serialize = (writer, status) => writer.WriteByte((byte)status);
        ByteSerializer<ReportStatus>.Deserialize = reader => (ReportStatus)reader.ReadByte();
        
        ByteSerializer<ProfileInfo>.Serialize = (writer, profile) => ProfileSerialization.SerializeProfile(writer, profile, true); 
        ByteSerializer<ProfileInfo>.Deserialize = ProfileSerialization.DeserializeProfile;
        
        ByteSerializer<ProfileSession>.Serialize = ProfileSerialization.SerializeSession;
        ByteSerializer<ProfileSession>.Deserialize = ProfileSerialization.DeserializeSession;
        
        ByteSerializer<PunishmentInfo>.Serialize = PunishmentSerialization.WritePunishmentInfo;
        ByteSerializer<PunishmentInfo>.Deserialize = PunishmentSerialization.ReadPunishmentInfo;
        
        ByteSerializer<StaffRole>.Serialize = (writer, role) =>
        {
            writer.WriteString(role.Id);
            writer.WriteBool(role.IsAdministrator);
            writer.WriteArray(role.RoleIds);
            writer.WriteArray(role.Permissions);
        };

        ByteSerializer<StaffRole>.Deserialize = reader =>
        {
            var role = new StaffRole
            {
                Id = reader.ReadString(),
                IsAdministrator = reader.ReadBool(),
                RoleIds = reader.ReadArray<ulong>(),
                Permissions = reader.ReadArray<string>()
            };

            return role;
        };
        
        ByteSerializer<PluginInfo>.Serialize = (writer, info) =>
        {
            writer.WriteString(info.Name);
            writer.WriteString(info.File);
            writer.WriteString(info.Author);
            writer.WriteString(info.Version);
            writer.WriteString(info.Description);
        };

        ByteSerializer<PluginInfo>.Deserialize = reader =>
        {
            var info = new PluginInfo
            {
                Name = reader.ReadString(),
                File = reader.ReadString(),
                Author = reader.ReadString(),
                Version = reader.ReadString(),
                Description = reader.ReadString()
            };

            return info;
        };
        
        ByteSerializer<CachedDiscordMessage>.Serialize = (writer, msg) =>
        {
            writer.WriteUInt64(msg.GuildId);
            writer.WriteUInt64(msg.ChannelId);
            writer.WriteUInt64(msg.MessageId);
        };

        ByteSerializer<CachedDiscordMessage>.Deserialize = reader =>
        {
            return new(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64());
        };
    }
}