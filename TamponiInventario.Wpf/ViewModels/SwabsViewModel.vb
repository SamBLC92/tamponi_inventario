Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows.Data
Imports TamponiInventario.Wpf.Models

Namespace TamponiInventario.Wpf.ViewModels
  Public Class SwabsViewModel
    Inherits ObservableObject

    Private ReadOnly _swabs As ObservableCollection(Of SwabRecord)
    Private _searchQuery As String
    Private _selectedSwab As SwabRecord

    Public Sub New()
      _swabs = New ObservableCollection(Of SwabRecord) From {
        New SwabRecord With {.Id = 1, .Name = "Tampone A", .Sku = "TB-0001", .Status = "RESO", .MachineName = "Magazzino", .LastTake = "12/02/2024 08:10", .LastReturn = "13/02/2024 17:22", .TotalDays = 5, .CurrentDays = 0, .Alarm = False, .Warning = False},
        New SwabRecord With {.Id = 2, .Name = "Tampone B", .Sku = "TB-0002", .Status = "PRESO", .MachineName = "CNC 01", .LastTake = "18/02/2024 09:50", .LastReturn = "—", .TotalDays = 12, .CurrentDays = 4, .Alarm = False, .Warning = True},
        New SwabRecord With {.Id = 3, .Name = "Tampone C", .Sku = "TB-0003", .Status = "PRESO", .MachineName = "Laser 02", .LastTake = "10/02/2024 10:20", .LastReturn = "—", .TotalDays = 22, .CurrentDays = 10, .Alarm = True, .Warning = False}
      }

      FilteredSwabs = CollectionViewSource.GetDefaultView(_swabs)
      FilteredSwabs.Filter = AddressOf ApplyFilter

      AddSwabCommand = New RelayCommand(AddressOf AddSwab)
      EditSwabCommand = New RelayCommand(AddressOf EditSwab, AddressOf CanManageSwab)
      DeleteSwabCommand = New RelayCommand(AddressOf DeleteSwab, AddressOf CanManageSwab)
      ApplyFilterCommand = New RelayCommand(Sub(_) FilteredSwabs.Refresh())
    End Sub

    Public ReadOnly Property Swabs As ObservableCollection(Of SwabRecord)
      Get
        Return _swabs
      End Get
    End Property

    Public Property SearchQuery As String
      Get
        Return _searchQuery
      End Get
      Set(value As String)
        If SetProperty(_searchQuery, value) Then
          FilteredSwabs.Refresh()
        End If
      End Set
    End Property

    Public Property SelectedSwab As SwabRecord
      Get
        Return _selectedSwab
      End Get
      Set(value As SwabRecord)
        If SetProperty(_selectedSwab, value) Then
          CType(EditSwabCommand, RelayCommand).RaiseCanExecuteChanged()
          CType(DeleteSwabCommand, RelayCommand).RaiseCanExecuteChanged()
        End If
      End Set
    End Property

    Public ReadOnly Property FilteredSwabs As ICollectionView

    Public ReadOnly Property AddSwabCommand As RelayCommand
    Public ReadOnly Property EditSwabCommand As RelayCommand
    Public ReadOnly Property DeleteSwabCommand As RelayCommand
    Public ReadOnly Property ApplyFilterCommand As RelayCommand

    Private Function ApplyFilter(item As Object) As Boolean
      If String.IsNullOrWhiteSpace(SearchQuery) Then
        Return True
      End If

      Dim record = TryCast(item, SwabRecord)
      If record Is Nothing Then
        Return False
      End If

      Dim query = SearchQuery.Trim().ToLowerInvariant()
      Return record.Name.ToLowerInvariant().Contains(query) OrElse record.MachineName.ToLowerInvariant().Contains(query) OrElse record.Sku.ToLowerInvariant().Contains(query)
    End Function

    Private Sub AddSwab(parameter As Object)
      Dim newId = If(_swabs.Count = 0, 1, _swabs.Max(Function(s) s.Id) + 1)
      _swabs.Add(New SwabRecord With {
        .Id = newId,
        .Name = "Nuovo Tampone",
        .Sku = $"TB-{newId:0000}",
        .Status = "RESO",
        .MachineName = "Magazzino",
        .LastTake = "—",
        .LastReturn = "—",
        .TotalDays = 0,
        .CurrentDays = 0
      })
    End Sub

    Private Sub EditSwab(parameter As Object)
      If SelectedSwab Is Nothing Then
        Return
      End If

      SelectedSwab.Name = $"{SelectedSwab.Name} (modificato)"
      FilteredSwabs.Refresh()
    End Sub

    Private Sub DeleteSwab(parameter As Object)
      If SelectedSwab Is Nothing Then
        Return
      End If

      _swabs.Remove(SelectedSwab)
      SelectedSwab = Nothing
    End Sub

    Private Function CanManageSwab(parameter As Object) As Boolean
      Return SelectedSwab IsNot Nothing
    End Function
  End Class
End Namespace
