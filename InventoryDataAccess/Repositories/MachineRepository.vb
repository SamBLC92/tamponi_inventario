Imports Microsoft.Data.Sqlite
Imports InventoryDataAccess.Infrastructure
Imports InventoryDataAccess.Models

Namespace Repositories
    Public Class MachineRepository
        Private ReadOnly _db As InventoryDb

        Public Sub New(db As InventoryDb)
            _db = db
        End Sub

        Public Function AddMachine(name As String) As Integer
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "INSERT INTO machines (name) VALUES (@name);"
                    command.Parameters.AddWithValue("@name", name)
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT last_insert_rowid();"
                    Return Convert.ToInt32(command.ExecuteScalar())
                End Using
            End Using
        End Function

        Public Sub DeleteMachine(machineId As Integer)
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "DELETE FROM machines WHERE id=@id;"
                    command.Parameters.AddWithValue("@id", machineId)
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub

        Public Function ListMachines() As List(Of Machine)
            Dim results As New List(Of Machine)()
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, name FROM machines ORDER BY name COLLATE NOCASE;"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            results.Add(New Machine With {
                                .Id = reader.GetInt32(0),
                                .Name = reader.GetString(1)
                            })
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        Public Function GetMachineById(machineId As Integer) As Machine?
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, name FROM machines WHERE id=@id;"
                    command.Parameters.AddWithValue("@id", machineId)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return New Machine With {
                                .Id = reader.GetInt32(0),
                                .Name = reader.GetString(1)
                            }
                        End If
                    End Using
                End Using
            End Using

            Return Nothing
        End Function

        Public Function IsMachineInUse(machineId As Integer) As Boolean
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT 1 FROM swab_state WHERE machine_id=@id LIMIT 1;"
                    command.Parameters.AddWithValue("@id", machineId)
                    Using reader = command.ExecuteReader()
                        Return reader.Read()
                    End Using
                End Using
            End Using
        End Function
    End Class
End Namespace
