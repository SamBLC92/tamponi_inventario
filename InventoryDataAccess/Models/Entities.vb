Namespace Models
    Public Class Swab
        Public Property Id As Integer
        Public Property Sku As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property CreatedAt As DateTime
    End Class

    Public Class Machine
        Public Property Id As Integer
        Public Property Name As String = String.Empty
    End Class

    Public Class Movement
        Public Property Id As Integer
        Public Property SwabId As Integer
        Public Property Action As String = String.Empty
        Public Property MachineId As Integer?
        Public Property Timestamp As DateTime
        Public Property Note As String?
    End Class

    Public Class SwabState
        Public Property SwabId As Integer
        Public Property InStock As Boolean
        Public Property MachineId As Integer?
        Public Property UpdatedAt As DateTime?
    End Class

    Public Class UsageSession
        Public Property Id As Integer
        Public Property SwabId As Integer
        Public Property TakenAt As DateTime
        Public Property ReturnedAt As DateTime?
    End Class

    Public Class UsageActionResult
        Public Property SwabId As Integer
        Public Property Action As String = String.Empty
        Public Property Timestamp As DateTime
        Public Property DaysInSession As Integer?
        Public Property AddedUniqueDays As Integer
    End Class
End Namespace
