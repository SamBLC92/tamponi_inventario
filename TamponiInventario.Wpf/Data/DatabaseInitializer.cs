using Microsoft.Data.Sqlite;

namespace TamponiInventario.Wpf.Data;

public static class DatabaseInitializer
{
    public const int DefaultGlobalWarnDays = 180;
    public const int DefaultGlobalAlarmDays = 200;
    public const string SettingsKeyWarnDays = "global_warn_days";
    public const string SettingsKeyAlarmDays = "global_alarm_days";

    private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS swabs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sku TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS machines (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT UNIQUE NOT NULL
);

-- Stato tampone: in_stock 1=RESO (magazzino), 0=PRESO (in uso)
-- machine_id valorizzato solo quando PRESO
CREATE TABLE IF NOT EXISTS swab_state (
    swab_id INTEGER PRIMARY KEY,
    in_stock INTEGER NOT NULL DEFAULT 1,
    machine_id INTEGER,
    updated_at TEXT NOT NULL,
    FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
    FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS movements (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    swab_id INTEGER NOT NULL,
    action TEXT NOT NULL CHECK(action IN ('TAKE','RETURN')),
    machine_id INTEGER,
    ts TEXT NOT NULL,
    note TEXT,
    FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
    FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_movements_swab_action_ts
  ON movements(swab_id, action, ts);

CREATE TABLE IF NOT EXISTS usage_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    swab_id INTEGER NOT NULL,
    taken_ts TEXT NOT NULL,
    returned_ts TEXT,
    FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_usage_sessions_open ON usage_sessions(swab_id, returned_ts);

-- Giorni unici di utilizzo (se prendo/reso 10 volte nello stesso giorno => 1 solo)
CREATE TABLE IF NOT EXISTS usage_days (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    swab_id INTEGER NOT NULL,
    day TEXT NOT NULL,
    FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
    UNIQUE(swab_id, day)
);

CREATE INDEX IF NOT EXISTS idx_usage_days_swab ON usage_days(swab_id);

CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
";

    public static void Initialize(SqliteConnection connection)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();

        InsertDefaultSetting(connection, SettingsKeyWarnDays, DefaultGlobalWarnDays);
        InsertDefaultSetting(connection, SettingsKeyAlarmDays, DefaultGlobalAlarmDays);
    }

    private static void InsertDefaultSetting(SqliteConnection connection, string key, int value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO settings (key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value.ToString());
        command.ExecuteNonQuery();
    }
}
