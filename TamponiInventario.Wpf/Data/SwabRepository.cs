using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TamponiInventario.Wpf.Data;

public sealed class SwabRepository
{
    private readonly string _connectionString;

    public SwabRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IReadOnlyList<SwabOverview> FetchSwabs(string? query)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        var warnDays = GetGlobalWarnDays(connection);
        var alarmDays = GetGlobalAlarmDays(connection);
        var likeQuery = $"%{query ?? string.Empty}%";

        var sql = @"
SELECT s.id, s.sku, s.name,
       COALESCE(st.in_stock, 1) AS in_stock,
       COALESCE(st.updated_at, s.created_at) AS updated_at,
       st.machine_id AS machine_id,
       mc.name AS machine_name,
       (SELECT mv.ts FROM movements mv WHERE mv.swab_id=s.id AND mv.action='TAKE' ORDER BY mv.ts DESC LIMIT 1) AS last_take_ts,
       (SELECT mv.ts FROM movements mv WHERE mv.swab_id=s.id AND mv.action='RETURN' ORDER BY mv.ts DESC LIMIT 1) AS last_return_ts
FROM swabs s
LEFT JOIN swab_state st ON st.swab_id = s.id
LEFT JOIN machines mc ON mc.id = st.machine_id
";

        var withFilter = !string.IsNullOrWhiteSpace(query);
        if (withFilter)
        {
            sql += @"
WHERE s.name LIKE $query COLLATE NOCASE
   OR mc.name LIKE $query COLLATE NOCASE
";
        }

        sql += "ORDER BY s.name COLLATE NOCASE";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (withFilter)
        {
            command.Parameters.AddWithValue("$query", likeQuery);
        }

        using var reader = command.ExecuteReader();
        var items = new List<SwabOverview>();
        while (reader.Read())
        {
            var swabId = reader.GetInt32(reader.GetOrdinal("id"));
            var openTakenTs = OpenTakenTs(connection, swabId);
            var currentDays = openTakenTs is null ? 0 : CurrentCalendarDays(openTakenTs);
            var totalDays = TotalUniqueDays(connection, swabId);
            var isWarning = currentDays > warnDays || totalDays > warnDays;
            var isAlarm = currentDays > alarmDays || totalDays > alarmDays;

            items.Add(new SwabOverview(
                Id: swabId,
                Sku: reader.GetString(reader.GetOrdinal("sku")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                InStock: reader.GetInt32(reader.GetOrdinal("in_stock")) == 1,
                UpdatedAt: reader.GetString(reader.GetOrdinal("updated_at")),
                OpenTakenTs: openTakenTs,
                CurrentDays: currentDays,
                TotalDays: totalDays,
                Warning: isWarning,
                Alarm: isAlarm,
                LastTakeTs: reader.IsDBNull(reader.GetOrdinal("last_take_ts")) ? null : reader.GetString(reader.GetOrdinal("last_take_ts")),
                LastReturnTs: reader.IsDBNull(reader.GetOrdinal("last_return_ts")) ? null : reader.GetString(reader.GetOrdinal("last_return_ts")),
                MachineName: reader.IsDBNull(reader.GetOrdinal("machine_name")) ? null : reader.GetString(reader.GetOrdinal("machine_name"))
            ));
        }

        return items;
    }

    public SwabState GetState(int swabId)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT in_stock, machine_id, updated_at FROM swab_state WHERE swab_id=$swabId";
        command.Parameters.AddWithValue("$swabId", swabId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new SwabState(
                InStock: reader.GetInt32(reader.GetOrdinal("in_stock")) == 1,
                MachineId: reader.IsDBNull(reader.GetOrdinal("machine_id")) ? null : reader.GetInt32(reader.GetOrdinal("machine_id")),
                UpdatedAt: reader.GetString(reader.GetOrdinal("updated_at"))
            );
        }

        return new SwabState(true, null, null);
    }

    public void SetState(int swabId, bool inStock, int? machineId)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        if (inStock)
        {
            machineId = null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR REPLACE INTO swab_state (swab_id, in_stock, machine_id, updated_at)
VALUES ($swabId, $inStock, $machineId, $updatedAt)
";
        command.Parameters.AddWithValue("$swabId", swabId);
        command.Parameters.AddWithValue("$inStock", inStock ? 1 : 0);
        command.Parameters.AddWithValue("$machineId", (object?)machineId ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<MachineItem> ListMachines()
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM machines ORDER BY name COLLATE NOCASE";

        using var reader = command.ExecuteReader();
        var items = new List<MachineItem>();
        while (reader.Read())
        {
            items.Add(new MachineItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Name: reader.GetString(reader.GetOrdinal("name"))
            ));
        }

        return items;
    }

    public int AddUsageDaysForRange(int swabId, string startIso, string endIso)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        var start = DateTime.Parse(startIso, CultureInfo.InvariantCulture).Date;
        var end = DateTime.Parse(endIso, CultureInfo.InvariantCulture).Date;
        if (end < start)
        {
            return 0;
        }

        var inserted = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO usage_days (swab_id, day) VALUES ($swabId, $day)";
            command.Parameters.AddWithValue("$swabId", swabId);
            command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }

    public string? OpenTakenTs(int swabId)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT taken_ts FROM usage_sessions
WHERE swab_id=$swabId AND returned_ts IS NULL
ORDER BY taken_ts DESC LIMIT 1
";
        command.Parameters.AddWithValue("$swabId", swabId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return reader.GetString(reader.GetOrdinal("taken_ts"));
        }

        return null;
    }

    public int TotalUniqueDays(int swabId)
    {
        using var connection = OpenConnection();
        DatabaseInitializer.Initialize(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) AS c FROM usage_days WHERE swab_id=$swabId";
        command.Parameters.AddWithValue("$swabId", swabId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return reader.GetInt32(reader.GetOrdinal("c"));
        }

        return 0;
    }

    private static int CurrentCalendarDays(string startIso)
    {
        return CalendarDaysBetween(startIso, DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
    }

    private static int CalendarDaysBetween(string startIso, string endIso)
    {
        var start = DateTime.Parse(startIso, CultureInfo.InvariantCulture).Date;
        var end = DateTime.Parse(endIso, CultureInfo.InvariantCulture).Date;
        return (end - start).Days + 1;
    }

    private static string? GetSetting(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key";
        command.Parameters.AddWithValue("$key", key);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return reader.GetString(reader.GetOrdinal("value"));
        }

        return null;
    }

    private static int GetPositiveSetting(SqliteConnection connection, string key, int defaultValue)
    {
        var raw = GetSetting(connection, key);
        if (!int.TryParse(raw, out var value))
        {
            value = defaultValue;
        }

        if (value <= 0)
        {
            value = defaultValue;
        }

        return value;
    }

    private static int GetGlobalWarnDays(SqliteConnection connection)
    {
        return GetPositiveSetting(connection, DatabaseInitializer.SettingsKeyWarnDays, DatabaseInitializer.DefaultGlobalWarnDays);
    }

    private static int GetGlobalAlarmDays(SqliteConnection connection)
    {
        return GetPositiveSetting(connection, DatabaseInitializer.SettingsKeyAlarmDays, DatabaseInitializer.DefaultGlobalAlarmDays);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}

public sealed record SwabOverview(
    int Id,
    string Sku,
    string Name,
    bool InStock,
    string UpdatedAt,
    string? OpenTakenTs,
    int CurrentDays,
    int TotalDays,
    bool Warning,
    bool Alarm,
    string? LastTakeTs,
    string? LastReturnTs,
    string? MachineName
);

public sealed record SwabState(bool InStock, int? MachineId, string? UpdatedAt);

public sealed record MachineItem(int Id, string Name);
