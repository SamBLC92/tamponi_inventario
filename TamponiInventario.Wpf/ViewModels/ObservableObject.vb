Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Namespace TamponiInventario.Wpf.ViewModels
  Public MustInherit Class ObservableObject
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
      RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Protected Function SetProperty(Of T)(ByRef storage As T, value As T, <CallerMemberName> Optional propertyName As String = Nothing) As Boolean
      If Equals(storage, value) Then
        Return False
      End If

      storage = value
      OnPropertyChanged(propertyName)
      Return True
    End Function
  End Class
End Namespace
