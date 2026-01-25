Imports Microsoft.Data.Sqlite
Imports InventoryDataAccess.Infrastructure
Imports InventoryDataAccess.Models

Namespace Repositories
    Public Class SwabRepository
        Private ReadOnly _db As InventoryDb

        Public Sub New(db As InventoryDb)
            _db = db
        End Sub

        Public Function AddSwab(sku As String, name As String, createdAt As DateTime) As Integer
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "INSERT INTO swabs (sku, name, created_at) VALUES (@sku, @name, @created_at);"
                    command.Parameters.AddWithValue("@sku", sku)
                    command.Parameters.AddWithValue("@name", name)
                    command.Parameters.AddWithValue("@created_at", ToIso(createdAt))
                    command.ExecuteNonQuery()
                End Using

                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT last_insert_rowid();"
                    Return Convert.ToInt32(command.ExecuteScalar())
                End Using
            End Using
        End Function

        Public Sub UpdateSwab(swabId As Integer, sku As String, name As String)
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "UPDATE swabs SET sku=@sku, name=@name WHERE id=@id;"
                    command.Parameters.AddWithValue("@sku", sku)
                    command.Parameters.AddWithValue("@name", name)
                    command.Parameters.AddWithValue("@id", swabId)
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub

        Public Function GetSwabById(swabId As Integer) As Swab?
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, sku, name, created_at FROM swabs WHERE id=@id;"
                    command.Parameters.AddWithValue("@id", swabId)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return MapSwab(reader)
                        End If
                    End Using
                End Using
            End Using

            Return Nothing
        End Function

        Public Function GetSwabBySku(sku As String) As Swab?
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, sku, name, created_at FROM swabs WHERE sku=@sku;"
                    command.Parameters.AddWithValue("@sku", sku)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return MapSwab(reader)
                        End If
                    End Using
                End Using
            End Using

            Return Nothing
        End Function

        Public Function ListSwabs() As List(Of Swab)
            Dim results As New List(Of Swab)()
            Using connection = _db.CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT id, sku, name, created_at FROM swabs ORDER BY name COLLATE NOCASE;"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            results.Add(MapSwab(reader))
                        End While
                    End Using
                End Using
            End Using

            Return results
        End Function

        Private Shared Function MapSwab(reader As SqliteDataReader) As Swab
            Return New Swab With {
                .Id = reader.GetInt32(0),
                .Sku = reader.GetString(1),
                .Name = reader.GetString(2),
                .CreatedAt = FromIso(reader.GetString(3))
            }
        End Function

        Private Shared Function ToIso(value As DateTime) As String
            Return value.ToString("yyyy-MM-ddTHH:mm:ss")
        End Function

        Private Shared Function FromIso(value As String) As DateTime
            Return DateTime.Parse(value)
        End Function
    End Class
End Namespace
