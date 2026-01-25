Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows.Data
Imports TamponiInventario.Wpf.Models

Namespace TamponiInventario.Wpf.ViewModels
  Public Class AdminViewModel
    Inherits ObservableObject

    Private ReadOnly _swabs As ObservableCollection(Of SwabRecord)
    Private ReadOnly _machines As ObservableCollection(Of MachineRecord)
    Private _swabSearchQuery As String
    Private _newSwabSku As String
    Private _newSwabName As String
    Private _newMachineName As String
    Private _selectedSwab As SwabRecord
    Private _selectedMachine As MachineRecord
    Private _globalWarnDays As Integer = 30
    Private _globalAlarmDays As Integer = 45

    Public Sub New()
      _swabs = New ObservableCollection(Of SwabRecord) From {
        New SwabRecord With {.Id = 101, .Name = "Tampone A", .Sku = "TB-0001", .Status = "RESO", .MachineName = "Magazzino", .LastTake = "10/02/2024", .LastReturn = "12/02/2024", .TotalDays = 4, .CurrentDays = 0},
        New SwabRecord With {.Id = 102, .Name = "Tampone B", .Sku = "TB-0002", .Status = "PRESO", .MachineName = "CNC 01", .LastTake = "18/02/2024", .LastReturn = "—", .TotalDays = 12, .CurrentDays = 4}
      }

      _machines = New ObservableCollection(Of MachineRecord) From {
        New MachineRecord With {.Id = 1, .Name = "CNC 01"},
        New MachineRecord With {.Id = 2, .Name = "Laser 02"}
      }

      FilteredSwabs = CollectionViewSource.GetDefaultView(_swabs)
      FilteredSwabs.Filter = AddressOf ApplyFilter

      AddSwabCommand = New RelayCommand(AddressOf AddSwab)
      EditSwabCommand = New RelayCommand(AddressOf EditSwab, AddressOf CanManageSwab)
      DeleteSwabCommand = New RelayCommand(AddressOf DeleteSwab, AddressOf CanManageSwab)
      AddMachineCommand = New RelayCommand(AddressOf AddMachine)
      DeleteMachineCommand = New RelayCommand(AddressOf DeleteMachine, AddressOf CanManageMachine)
      SaveSettingsCommand = New RelayCommand(AddressOf SaveSettings)
      ApplyFilterCommand = New RelayCommand(Sub(_) FilteredSwabs.Refresh())
    End Sub

    Public ReadOnly Property Swabs As ObservableCollection(Of SwabRecord)
      Get
        Return _swabs
      End Get
    End Property

    Public ReadOnly Property Machines As ObservableCollection(Of MachineRecord)
      Get
        Return _machines
      End Get
    End Property

    Public Property SwabSearchQuery As String
      Get
        Return _swabSearchQuery
      End Get
      Set(value As String)
        If SetProperty(_swabSearchQuery, value) Then
          FilteredSwabs.Refresh()
        End If
      End Set
    End Property

    Public Property NewSwabSku As String
      Get
        Return _newSwabSku
      End Get
      Set(value As String)
        SetProperty(_newSwabSku, value)
      End Set
    End Property

    Public Property NewSwabName As String
      Get
        Return _newSwabName
      End Get
      Set(value As String)
        SetProperty(_newSwabName, value)
      End Set
    End Property

    Public Property NewMachineName As String
      Get
        Return _newMachineName
      End Get
      Set(value As String)
        SetProperty(_newMachineName, value)
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

    Public Property SelectedMachine As MachineRecord
      Get
        Return _selectedMachine
      End Get
      Set(value As MachineRecord)
        If SetProperty(_selectedMachine, value) Then
          CType(DeleteMachineCommand, RelayCommand).RaiseCanExecuteChanged()
        End If
      End Set
    End Property

    Public Property GlobalWarnDays As Integer
      Get
        Return _globalWarnDays
      End Get
      Set(value As Integer)
        SetProperty(_globalWarnDays, value)
      End Set
    End Property

    Public Property GlobalAlarmDays As Integer
      Get
        Return _globalAlarmDays
      End Get
      Set(value As Integer)
        SetProperty(_globalAlarmDays, value)
      End Set
    End Property

    Public ReadOnly Property FilteredSwabs As ICollectionView

    Public ReadOnly Property AddSwabCommand As RelayCommand
    Public ReadOnly Property EditSwabCommand As RelayCommand
    Public ReadOnly Property DeleteSwabCommand As RelayCommand
    Public ReadOnly Property AddMachineCommand As RelayCommand
    Public ReadOnly Property DeleteMachineCommand As RelayCommand
    Public ReadOnly Property SaveSettingsCommand As RelayCommand
    Public ReadOnly Property ApplyFilterCommand As RelayCommand

    Private Function ApplyFilter(item As Object) As Boolean
      If String.IsNullOrWhiteSpace(SwabSearchQuery) Then
        Return True
      End If

      Dim record = TryCast(item, SwabRecord)
      If record Is Nothing Then
        Return False
      End If

      Dim query = SwabSearchQuery.Trim().ToLowerInvariant()
      Return record.Name.ToLowerInvariant().Contains(query) OrElse record.MachineName.ToLowerInvariant().Contains(query) OrElse record.Sku.ToLowerInvariant().Contains(query)
    End Function

    Private Sub AddSwab(parameter As Object)
      If String.IsNullOrWhiteSpace(NewSwabSku) OrElse String.IsNullOrWhiteSpace(NewSwabName) Then
        Return
      End If

      Dim newId = If(_swabs.Count = 0, 1, _swabs.Max(Function(s) s.Id) + 1)
      _swabs.Add(New SwabRecord With {
        .Id = newId,
        .Name = NewSwabName,
        .Sku = NewSwabSku,
        .Status = "RESO",
        .MachineName = "Magazzino",
        .LastTake = "—",
        .LastReturn = "—",
        .TotalDays = 0,
        .CurrentDays = 0
      })

      NewSwabSku = String.Empty
      NewSwabName = String.Empty
    End Sub

    Private Sub EditSwab(parameter As Object)
      Dim record = TryCast(parameter, SwabRecord)
      If record Is Nothing Then
        Return
      End If

      record.Name = $"{record.Name} (modificato)"
      FilteredSwabs.Refresh()
    End Sub

    Private Sub DeleteSwab(parameter As Object)
      Dim record = TryCast(parameter, SwabRecord)
      If record Is Nothing Then
        Return
      End If

      _swabs.Remove(record)
    End Sub

    Private Sub AddMachine(parameter As Object)
      If String.IsNullOrWhiteSpace(NewMachineName) Then
        Return
      End If

      Dim newId = If(_machines.Count = 0, 1, _machines.Max(Function(m) m.Id) + 1)
      _machines.Add(New MachineRecord With {.Id = newId, .Name = NewMachineName})
      NewMachineName = String.Empty
    End Sub

    Private Sub DeleteMachine(parameter As Object)
      Dim record = TryCast(parameter, MachineRecord)
      If record Is Nothing Then
        Return
      End If

      _machines.Remove(record)
    End Sub

    Private Sub SaveSettings(parameter As Object)
      ' Placeholder for persistence
    End Sub

    Private Function CanManageSwab(parameter As Object) As Boolean
      Return SelectedSwab IsNot Nothing
    End Function

    Private Function CanManageMachine(parameter As Object) As Boolean
      Return SelectedMachine IsNot Nothing
    End Function
  End Class
End Namespace
