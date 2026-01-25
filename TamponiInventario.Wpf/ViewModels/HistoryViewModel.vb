Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows.Data
Imports TamponiInventario.Wpf.Models

Namespace TamponiInventario.Wpf.ViewModels
  Public Class HistoryViewModel
    Inherits ObservableObject

    Private ReadOnly _movements As ObservableCollection(Of MovementRecord)
    Private _filterQuery As String
    Private _limit As Integer = 50

    Public Sub New()
      _movements = New ObservableCollection(Of MovementRecord) From {
        New MovementRecord With {.Timestamp = DateTime.Now.AddHours(-5), .Action = "TAKE", .SwabName = "Tampone B", .Sku = "TB-0002", .MachineName = "CNC 01", .Note = "Cambio turno"},
        New MovementRecord With {.Timestamp = DateTime.Now.AddHours(-2), .Action = "RETURN", .SwabName = "Tampone A", .Sku = "TB-0001", .MachineName = "Magazzino", .Note = "Rientro"},
        New MovementRecord With {.Timestamp = DateTime.Now.AddHours(-1), .Action = "TAKE", .SwabName = "Tampone C", .Sku = "TB-0003", .MachineName = "Laser 02", .Note = "Lavorazione urgente"}
      }

      FilteredMovements = CollectionViewSource.GetDefaultView(_movements)
      FilteredMovements.Filter = AddressOf ApplyFilter

      RefreshCommand = New RelayCommand(AddressOf Refresh)
      ApplyFilterCommand = New RelayCommand(Sub(_) FilteredMovements.Refresh())
    End Sub

    Public ReadOnly Property Movements As ObservableCollection(Of MovementRecord)
      Get
        Return _movements
      End Get
    End Property

    Public Property FilterQuery As String
      Get
        Return _filterQuery
      End Get
      Set(value As String)
        If SetProperty(_filterQuery, value) Then
          FilteredMovements.Refresh()
        End If
      End Set
    End Property

    Public Property Limit As Integer
      Get
        Return _limit
      End Get
      Set(value As Integer)
        SetProperty(_limit, value)
      End Set
    End Property

    Public ReadOnly Property FilteredMovements As ICollectionView

    Public ReadOnly Property RefreshCommand As RelayCommand
    Public ReadOnly Property ApplyFilterCommand As RelayCommand

    Private Function ApplyFilter(item As Object) As Boolean
      If String.IsNullOrWhiteSpace(FilterQuery) Then
        Return True
      End If

      Dim record = TryCast(item, MovementRecord)
      If record Is Nothing Then
        Return False
      End If

      Dim query = FilterQuery.Trim().ToLowerInvariant()
      Return record.SwabName.ToLowerInvariant().Contains(query) OrElse record.Sku.ToLowerInvariant().Contains(query) OrElse record.MachineName.ToLowerInvariant().Contains(query) OrElse record.Note.ToLowerInvariant().Contains(query)
    End Function

    Private Sub Refresh(parameter As Object)
      FilteredMovements.Refresh()
    End Sub
  End Class
End Namespace
