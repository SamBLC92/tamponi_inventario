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
    Private _decodedSku As String
    Private _decodedFormat As String
    Private _cameraStatus As String
    Private _isCameraRunning As Boolean
    Private _manualSkuInput As String
    Private _pendingSku As String

    Public Sub New()
      Modes = New ObservableCollection(Of String) From {"TOGGLE", "TAKE", "RETURN"}
      Machines = New ObservableCollection(Of MachineRecord) From {
        New MachineRecord With {.Id = 1, .Name = "CNC 01"},
        New MachineRecord With {.Id = 2, .Name = "Laser 02"}
      }
      BarcodeLibraries = New ObservableCollection(Of LibraryOption) From {
        New LibraryOption With {.Name = "ZXing.Net", .Notes = "Decodifica 1D/2D, supporto WPF via BitmapSource."},
        New LibraryOption With {.Name = "BarcodeLib", .Notes = "Lettura/scrittura base, più limitata su QR."},
        New LibraryOption With {.Name = "QRCoder", .Notes = "Solo generazione QR, utile se servono etichette interne."}
      }
      CameraLibraries = New ObservableCollection(Of LibraryOption) From {
        New LibraryOption With {.Name = "AForge.Video.DirectShow", .Notes = "Legacy DirectShow, rapido per device USB."},
        New LibraryOption With {.Name = "Accord.Video.DirectShow", .Notes = "Fork moderno di AForge, API simili."},
        New LibraryOption With {.Name = "Windows.Media.Capture", .Notes = "MediaCapture UWP/Win10, pipeline più moderna."}
      }

      SelectedMode = Modes.First()
      ResultMessage = "Nessuna scansione in corso."
      LastResult = "Nessuna scansione ancora."
      CameraStatus = "Camera non avviata."
      DecodedFormat = "Nessuna decodifica."

      SendScanCommand = New RelayCommand(AddressOf SendScan)
      StartCameraCommand = New RelayCommand(AddressOf StartCamera, AddressOf CanStartCamera)
      StopCameraCommand = New RelayCommand(AddressOf StopCamera, AddressOf CanStopCamera)
      SimulateDecodeCommand = New RelayCommand(AddressOf SimulateDecode, AddressOf CanSimulateDecode)
      ApplyDecodedCommand = New RelayCommand(AddressOf ApplyDecoded, AddressOf CanApplyDecoded)
      ManualSendCommand = New RelayCommand(AddressOf ManualSend)
      ConfirmMachineCommand = New RelayCommand(AddressOf ConfirmMachine, AddressOf CanConfirmMachine)
      CancelMachineCommand = New RelayCommand(AddressOf CancelMachine)
    End Sub

    Public ReadOnly Property Modes As ObservableCollection(Of String)
    Public ReadOnly Property Machines As ObservableCollection(Of MachineRecord)
    Public ReadOnly Property BarcodeLibraries As ObservableCollection(Of LibraryOption)
    Public ReadOnly Property CameraLibraries As ObservableCollection(Of LibraryOption)

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

    Public Property DecodedSku As String
      Get
        Return _decodedSku
      End Get
      Set(value As String)
        If SetProperty(_decodedSku, value) Then
          CType(ApplyDecodedCommand, RelayCommand).RaiseCanExecuteChanged()
        End If
      End Set
    End Property

    Public Property DecodedFormat As String
      Get
        Return _decodedFormat
      End Get
      Set(value As String)
        SetProperty(_decodedFormat, value)
      End Set
    End Property

    Public Property CameraStatus As String
      Get
        Return _cameraStatus
      End Get
      Set(value As String)
        SetProperty(_cameraStatus, value)
      End Set
    End Property

    Public Property IsCameraRunning As Boolean
      Get
        Return _isCameraRunning
      End Get
      Set(value As Boolean)
        If SetProperty(_isCameraRunning, value) Then
          CType(StartCameraCommand, RelayCommand).RaiseCanExecuteChanged()
          CType(StopCameraCommand, RelayCommand).RaiseCanExecuteChanged()
          CType(SimulateDecodeCommand, RelayCommand).RaiseCanExecuteChanged()
        End If
      End Set
    End Property

    Public Property ManualSkuInput As String
      Get
        Return _manualSkuInput
      End Get
      Set(value As String)
        SetProperty(_manualSkuInput, value)
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
    Public ReadOnly Property StartCameraCommand As RelayCommand
    Public ReadOnly Property StopCameraCommand As RelayCommand
    Public ReadOnly Property SimulateDecodeCommand As RelayCommand
    Public ReadOnly Property ApplyDecodedCommand As RelayCommand
    Public ReadOnly Property ManualSendCommand As RelayCommand
    Public ReadOnly Property ConfirmMachineCommand As RelayCommand
    Public ReadOnly Property CancelMachineCommand As RelayCommand

    Private Sub SendScan(parameter As Object)
      ProcessScan(SkuInput, "scanner")
    End Sub

    Private Sub ManualSend(parameter As Object)
      ProcessScan(ManualSkuInput, "manuale")
      ManualSkuInput = String.Empty
    End Sub

    Private Sub ApplyDecoded(parameter As Object)
      ProcessScan(DecodedSku, "camera")
    End Sub

    Private Sub ProcessScan(rawSku As String, sourceLabel As String)
      If String.IsNullOrWhiteSpace(rawSku) Then
        ResultMessage = "Inserisci uno SKU da scansionare."
        Return
      End If

      Dim cleanedSku = rawSku.Trim()
      _pendingSku = cleanedSku
      SkuInput = cleanedSku

      If SelectedMode = "TAKE" Then
        IsMachineSelectionVisible = True
        ResultMessage = $"Servono dettagli macchina per {cleanedSku} ({sourceLabel})."
        Return
      End If

      Dim actionLabel = If(SelectedMode = "RETURN", "RESO", "PRESO")
      ResultMessage = $"{actionLabel} — SKU {cleanedSku} registrato correttamente ({sourceLabel})."
      LastResult = ResultMessage
      SkuInput = String.Empty
      _pendingSku = String.Empty
    End Sub

    Private Sub ConfirmMachine(parameter As Object)
      If SelectedMachine Is Nothing Then
        Return
      End If

      ResultMessage = $"PRESO — SKU {_pendingSku} assegnato a {SelectedMachine.Name}."
      LastResult = ResultMessage
      IsMachineSelectionVisible = False
      SkuInput = String.Empty
      _pendingSku = String.Empty
    End Sub

    Private Sub CancelMachine(parameter As Object)
      IsMachineSelectionVisible = False
      ResultMessage = "Selezione macchina annullata."
      _pendingSku = String.Empty
    End Sub

    Private Function CanConfirmMachine(parameter As Object) As Boolean
      Return SelectedMachine IsNot Nothing
    End Function

    Private Sub StartCamera(parameter As Object)
      IsCameraRunning = True
      CameraStatus = "Camera avviata. In attesa di un barcode."
    End Sub

    Private Sub StopCamera(parameter As Object)
      IsCameraRunning = False
      CameraStatus = "Camera in pausa."
    End Sub

    Private Sub SimulateDecode(parameter As Object)
      Dim sampleSku = $"TAMP-{DateTime.Now:HHmmss}"
      DecodedSku = sampleSku
      DecodedFormat = "EAN-13 (simulato)"
      ResultMessage = $"Decodifica completata per {sampleSku} (camera)."
    End Sub

    Private Function CanStartCamera(parameter As Object) As Boolean
      Return Not IsCameraRunning
    End Function

    Private Function CanStopCamera(parameter As Object) As Boolean
      Return IsCameraRunning
    End Function

    Private Function CanSimulateDecode(parameter As Object) As Boolean
      Return IsCameraRunning
    End Function

    Private Function CanApplyDecoded(parameter As Object) As Boolean
      Return Not String.IsNullOrWhiteSpace(DecodedSku)
    End Function
  End Class
End Namespace
