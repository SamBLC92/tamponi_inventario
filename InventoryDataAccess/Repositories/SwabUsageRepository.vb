Imports Microsoft.Data.Sqlite
Imports InventoryDataAccess.Infrastructure
Imports InventoryDataAccess.Models

Namespace Repositories
    Public Class SwabUsageRepository
        Private ReadOnly _db As InventoryDb

        Public Sub New(db As InventoryDb)
            _db = db
        End Sub

        Public Function GetState(swabId As Integer) As SwabState
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT swab_id, in_stock, machine_id, updated_at FROM swab_state WHERE swab_id=@id;"
                    command.Parameters.AddWithValue("@id", swabId)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return New SwabState With {
                                .SwabId = reader.GetInt32(0),
                                .InStock = reader.GetInt32(1) = 1,
                                .MachineId = If(reader.IsDBNull(2), CType(Nothing, Integer?), reader.GetInt32(2)),
                                .UpdatedAt = If(reader.IsDBNull(3), CType(Nothing, DateTime?), FromIso(reader.GetString(3)))
                            }
                        End If
                    End Using
                End Using
            End Using

            Return New SwabState With {
                .SwabId = swabId,
                .InStock = True,
                .MachineId = Nothing,
                .UpdatedAt = Nothing
            }
        End Function

        Public Function TakeSwab(swabId As Integer, machineId As Integer, timestamp As DateTime) As UsageActionResult
            Using connection = _db.CreateConnection()
                Using transaction = connection.BeginTransaction()
                    InsertMovement(connection, transaction, swabId, "TAKE", machineId, timestamp)

                    Dim openSessionId As Integer? = GetOpenSessionId(connection, transaction, swabId)
                    If Not openSessionId.HasValue Then
                        Using command = connection.CreateCommand()
                            command.Transaction = transaction
                            command.CommandText = "INSERT INTO usage_sessions (swab_id, taken_ts, returned_ts) VALUES (@id, @ts, NULL);"
                            command.Parameters.AddWithValue("@id", swabId)
                            command.Parameters.AddWithValue("@ts", ToIso(timestamp))
                            command.ExecuteNonQuery()
                        End Using
                    End If

                    SetState(connection, transaction, swabId, False, machineId, timestamp)
                    transaction.Commit()
                End Using
            End Using

            Return New UsageActionResult With {
                .SwabId = swabId,
                .Action = "TAKE",
                .Timestamp = timestamp,
                .DaysInSession = Nothing,
                .AddedUniqueDays = 0
            }
        End Function

        Public Function ReturnSwab(swabId As Integer, timestamp As DateTime) As UsageActionResult
            Dim daysInSession As Integer? = Nothing
            Dim addedUniqueDays As Integer = 0

            Using connection = _db.CreateConnection()
                Using transaction = connection.BeginTransaction()
                    InsertMovement(connection, transaction, swabId, "RETURN", Nothing, timestamp)

                    Dim session = GetOpenSession(connection, transaction, swabId)
                    If session IsNot Nothing Then
                        daysInSession = CalendarDaysBetween(session.TakenAt, timestamp)
                        Using command = connection.CreateCommand()
                            command.Transaction = transaction
                            command.CommandText = "UPDATE usage_sessions SET returned_ts=@returned WHERE id=@id;"
                            command.Parameters.AddWithValue("@returned", ToIso(timestamp))
                            command.Parameters.AddWithValue("@id", session.Id)
                            command.ExecuteNonQuery()
                        End Using
                        addedUniqueDays = AddUsageDaysForRange(connection, transaction, swabId, session.TakenAt, timestamp)
                    End If

                    SetState(connection, transaction, swabId, True, Nothing, timestamp)
                    transaction.Commit()
                End Using
            End Using

            Return New UsageActionResult With {
                .SwabId = swabId,
                .Action = "RETURN",
                .Timestamp = timestamp,
                .DaysInSession = daysInSession,
                .AddedUniqueDays = addedUniqueDays
            }
        End Function

        Public Function GetTotalUniqueDays(swabId As Integer) As Integer
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT COUNT(*) FROM usage_days WHERE swab_id=@id;"
                    command.Parameters.AddWithValue("@id", swabId)
                    Return Convert.ToInt32(command.ExecuteScalar())
                End Using
            End Using
        End Function

        Private Sub InsertMovement(connection As SqliteConnection, transaction As SqliteTransaction, swabId As Integer, action As String, machineId As Integer?, timestamp As DateTime)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "INSERT INTO movements (swab_id, action, machine_id, ts, note) VALUES (@swab_id, @action, @machine_id, @ts, NULL);"
                command.Parameters.AddWithValue("@swab_id", swabId)
                command.Parameters.AddWithValue("@action", action)
                If machineId.HasValue Then
                    command.Parameters.AddWithValue("@machine_id", machineId.Value)
                Else
                    command.Parameters.AddWithValue("@machine_id", DBNull.Value)
                End If
                command.Parameters.AddWithValue("@ts", ToIso(timestamp))
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Sub SetState(connection As SqliteConnection, transaction As SqliteTransaction, swabId As Integer, inStock As Boolean, machineId As Integer?, timestamp As DateTime)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "INSERT OR REPLACE INTO swab_state (swab_id, in_stock, machine_id, updated_at) VALUES (@swab_id, @in_stock, @machine_id, @updated_at);"
                command.Parameters.AddWithValue("@swab_id", swabId)
                command.Parameters.AddWithValue("@in_stock", If(inStock, 1, 0))
                If inStock Then
                    command.Parameters.AddWithValue("@machine_id", DBNull.Value)
                Else
                    command.Parameters.AddWithValue("@machine_id", machineId.GetValueOrDefault())
                End If
                command.Parameters.AddWithValue("@updated_at", ToIso(timestamp))
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Function GetOpenSessionId(connection As SqliteConnection, transaction As SqliteTransaction, swabId As Integer) As Integer?
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "SELECT id FROM usage_sessions WHERE swab_id=@id AND returned_ts IS NULL ORDER BY taken_ts DESC LIMIT 1;"
                command.Parameters.AddWithValue("@id", swabId)
                Using reader = command.ExecuteReader()
                    If reader.Read() Then
                        Return reader.GetInt32(0)
                    End If
                End Using
            End Using

            Return Nothing
        End Function

        Private Function GetOpenSession(connection As SqliteConnection, transaction As SqliteTransaction, swabId As Integer) As UsageSession?
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "SELECT id, taken_ts FROM usage_sessions WHERE swab_id=@id AND returned_ts IS NULL ORDER BY taken_ts DESC LIMIT 1;"
                command.Parameters.AddWithValue("@id", swabId)
                Using reader = command.ExecuteReader()
                    If reader.Read() Then
                        Return New UsageSession With {
                            .Id = reader.GetInt32(0),
                            .SwabId = swabId,
                            .TakenAt = FromIso(reader.GetString(1))
                        }
                    End If
                End Using
            End Using

            Return Nothing
        End Function

        Private Function AddUsageDaysForRange(connection As SqliteConnection, transaction As SqliteTransaction, swabId As Integer, startDate As DateTime, endDate As DateTime) As Integer
            Dim added As Integer = 0
            Dim current As DateTime = startDate.Date
            Dim lastDate As DateTime = endDate.Date

            While current <= lastDate
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "INSERT OR IGNORE INTO usage_days (swab_id, day) VALUES (@swab_id, @day);"
                    command.Parameters.AddWithValue("@swab_id", swabId)
                    command.Parameters.AddWithValue("@day", current.ToString("yyyy-MM-dd"))
                    added += command.ExecuteNonQuery()
                End Using
                current = current.AddDays(1)
            End While

            Return added
        End Function

        Private Shared Function CalendarDaysBetween(startDate As DateTime, endDate As DateTime) As Integer
            Dim a = startDate.Date
            Dim b = endDate.Date
            Return (b - a).Days + 1
        End Function

        Private Shared Function ToIso(value As DateTime) As String
            Return value.ToString("yyyy-MM-ddTHH:mm:ss")
        End Function

        Private Shared Function FromIso(value As String) As DateTime
            Return DateTime.Parse(value)
        End Function
    End Class
End Namespace
