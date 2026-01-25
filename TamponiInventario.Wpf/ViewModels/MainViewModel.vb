Namespace TamponiInventario.Wpf.ViewModels
  Public Class MainViewModel
    Public Sub New()
      Swabs = New SwabsViewModel()
      History = New HistoryViewModel()
      Scan = New ScanViewModel()
      Admin = New AdminViewModel()
    End Sub

    Public ReadOnly Property Swabs As SwabsViewModel
    Public ReadOnly Property History As HistoryViewModel
    Public ReadOnly Property Scan As ScanViewModel
    Public ReadOnly Property Admin As AdminViewModel
  End Class
End Namespace
