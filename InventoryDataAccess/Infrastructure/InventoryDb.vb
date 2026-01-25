Imports Microsoft.Data.Sqlite

Namespace Infrastructure
    Public Class InventoryDb
        Private ReadOnly _connectionString As String

        Public Sub New(connectionString As String)
            _connectionString = connectionString
        End Sub

        Public Function CreateConnection() As SqliteConnection
            Dim connection As New SqliteConnection(_connectionString)
            connection.Open()
            Using pragma As SqliteCommand = connection.CreateCommand()
                pragma.CommandText = "PRAGMA foreign_keys = ON;"
                pragma.ExecuteNonQuery()
            End Using
            Return connection
        End Function

        Public Sub InitializeSchema()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS swabs (" &
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                        "sku TEXT UNIQUE NOT NULL," &
                        "name TEXT NOT NULL," &
                        "created_at TEXT NOT NULL" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS machines (" &
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                        "name TEXT UNIQUE NOT NULL" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS swab_state (" &
                        "swab_id INTEGER PRIMARY KEY," &
                        "in_stock INTEGER NOT NULL DEFAULT 1," &
                        "machine_id INTEGER," &
                        "updated_at TEXT NOT NULL," &
                        "FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE," &
                        "FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS movements (" &
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                        "swab_id INTEGER NOT NULL," &
                        "action TEXT NOT NULL CHECK(action IN ('TAKE','RETURN'))," &
                        "machine_id INTEGER," &
                        "ts TEXT NOT NULL," &
                        "note TEXT," &
                        "FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE," &
                        "FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_movements_swab_action_ts ON movements(swab_id, action, ts);"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS usage_sessions (" &
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                        "swab_id INTEGER NOT NULL," &
                        "taken_ts TEXT NOT NULL," &
                        "returned_ts TEXT," &
                        "FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_usage_sessions_open ON usage_sessions(swab_id, returned_ts);"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS usage_days (" &
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," &
                        "swab_id INTEGER NOT NULL," &
                        "day TEXT NOT NULL," &
                        "FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE," &
                        "UNIQUE(swab_id, day)" &
                        ");"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_usage_days_swab ON usage_days(swab_id);"
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "CREATE TABLE IF NOT EXISTS settings (" &
                        "key TEXT PRIMARY KEY," &
                        "value TEXT NOT NULL" &
                        ");"
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub
    End Class
End Namespace
