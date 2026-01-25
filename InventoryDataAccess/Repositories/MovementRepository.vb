Imports Microsoft.Data.Sqlite
Imports InventoryDataAccess.Infrastructure
Imports InventoryDataAccess.Models

Namespace Repositories
    Public Class MovementRepository
        Private ReadOnly _db As InventoryDb

        Public Sub New(db As InventoryDb)
            _db = db
        End Sub

        Public Function AddMovement(swabId As Integer, action As String, machineId As Integer?, timestamp As DateTime, note As String?) As Integer
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "INSERT INTO movements (swab_id, action, machine_id, ts, note) VALUES (@swab_id, @action, @machine_id, @ts, @note);"
                    command.Parameters.AddWithValue("@swab_id", swabId)
                    command.Parameters.AddWithValue("@action", action)
                    If machineId.HasValue Then
                        command.Parameters.AddWithValue("@machine_id", machineId.Value)
                    Else
                        command.Parameters.AddWithValue("@machine_id", DBNull.Value)
                    End If
                    command.Parameters.AddWithValue("@ts", ToIso(timestamp))
                    If note IsNot Nothing Then
                        command.Parameters.AddWithValue("@note", note)
                    Else
                        command.Parameters.AddWithValue("@note", DBNull.Value)
                    End If
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT last_insert_rowid();"
                    Return Convert.ToInt32(command.ExecuteScalar())
                End Using
            End Using
        End Function

        Public Function GetRecentMovements(limit As Integer) As List(Of Movement)
            Dim results As New List(Of Movement)()
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, swab_id, action, machine_id, ts, note FROM movements ORDER BY ts DESC LIMIT @limit;"
                    command.Parameters.AddWithValue("@limit", limit)
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            results.Add(New Movement With {
                                .Id = reader.GetInt32(0),
                                .SwabId = reader.GetInt32(1),
                                .Action = reader.GetString(2),
                                .MachineId = If(reader.IsDBNull(3), CType(Nothing, Integer?), reader.GetInt32(3)),
                                .Timestamp = FromIso(reader.GetString(4)),
                                .Note = If(reader.IsDBNull(5), Nothing, reader.GetString(5))
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        Private Shared Function ToIso(value As DateTime) As String
            Return value.ToString("yyyy-MM-ddTHH:mm:ss")
        End Function

        Private Shared Function FromIso(value As String) As DateTime
            Return DateTime.Parse(value)
        End Function
    End Class
End Namespace
