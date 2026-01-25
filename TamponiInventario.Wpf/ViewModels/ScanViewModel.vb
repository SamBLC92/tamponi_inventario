Imports System.Collections.ObjectModel
Imports System.Linq
Imports TamponiInventario.Wpf.Models

Namespace TamponiInventario.Wpf.ViewModels
  Public Class ScanViewModel
    Inherits ObservableObject

    Private _selectedMode As String
    Private _skuInput As String
    Private _resultMessage As String
    Private _lastResult As String
    Private _isMachineSelectionVisible As Boolean
    Private _selectedMachine As MachineRecord

    Public Sub New()
      Modes = New ObservableCollection(Of String) From {"TOGGLE", "TAKE", "RETURN"}
      Machines = New ObservableCollection(Of MachineRecord) From {
        New MachineRecord With {.Id = 1, .Name = "CNC 01"},
        New MachineRecord With {.Id = 2, .Name = "Laser 02"}
      }

      SelectedMode = Modes.First()
      ResultMessage = "Nessuna scansione in corso."
      LastResult = "Nessuna scansione ancora."

      SendScanCommand = New RelayCommand(AddressOf SendScan)
      ConfirmMachineCommand = New RelayCommand(AddressOf ConfirmMachine, AddressOf CanConfirmMachine)
      CancelMachineCommand = New RelayCommand(AddressOf CancelMachine)
    End Sub

    Public ReadOnly Property Modes As ObservableCollection(Of String)
    Public ReadOnly Property Machines As ObservableCollection(Of MachineRecord)

    Public Property SelectedMode As String
      Get
        Return _selectedMode
      End Get
      Set(value As String)
        SetProperty(_selectedMode, value)
      End Set
    End Property

    Public Property SkuInput As String
      Get
        Return _skuInput
      End Get
      Set(value As String)
        SetProperty(_skuInput, value)
      End Set
    End Property

    Public Property ResultMessage As String
      Get
        Return _resultMessage
      End Get
      Set(value As String)
        SetProperty(_resultMessage, value)
      End Set
    End Property

    Public Property LastResult As String
      Get
        Return _lastResult
      End Get
      Set(value As String)
        SetProperty(_lastResult, value)
      End Set
    End Property

    Public Property IsMachineSelectionVisible As Boolean
      Get
        Return _isMachineSelectionVisible
      End Get
      Set(value As Boolean)
        SetProperty(_isMachineSelectionVisible, value)
      End Set
    End Property

    Public Property SelectedMachine As MachineRecord
      Get
        Return _selectedMachine
      End Get
      Set(value As MachineRecord)
        If SetProperty(_selectedMachine, value) Then
          CType(ConfirmMachineCommand, RelayCommand).RaiseCanExecuteChanged()
        End If
      End Set
    End Property

    Public ReadOnly Property SendScanCommand As RelayCommand
    Public ReadOnly Property ConfirmMachineCommand As RelayCommand
    Public ReadOnly Property CancelMachineCommand As RelayCommand

    Private Sub SendScan(parameter As Object)
      If String.IsNullOrWhiteSpace(SkuInput) Then
        ResultMessage = "Inserisci uno SKU da scansionare."
        Return
      End If

      If SelectedMode = "TAKE" Then
        IsMachineSelectionVisible = True
        ResultMessage = $"Servono dettagli macchina per {SkuInput}."
        Return
      End If

      Dim actionLabel = If(SelectedMode = "RETURN", "RESO", "PRESO")
      ResultMessage = $"{actionLabel} — SKU {SkuInput} registrato correttamente."
      LastResult = ResultMessage
      SkuInput = String.Empty
    End Sub

    Private Sub ConfirmMachine(parameter As Object)
      If SelectedMachine Is Nothing Then
        Return
      End If

      ResultMessage = $"PRESO — SKU {SkuInput} assegnato a {SelectedMachine.Name}."
      LastResult = ResultMessage
      IsMachineSelectionVisible = False
      SkuInput = String.Empty
    End Sub

    Private Sub CancelMachine(parameter As Object)
      IsMachineSelectionVisible = False
      ResultMessage = "Selezione macchina annullata."
    End Sub

    Private Function CanConfirmMachine(parameter As Object) As Boolean
      Return SelectedMachine IsNot Nothing
    End Function
  End Class
End Namespace
