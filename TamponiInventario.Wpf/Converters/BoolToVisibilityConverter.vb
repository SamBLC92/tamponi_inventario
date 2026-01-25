Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data

Namespace TamponiInventario.Wpf.Converters
  Public Class BoolToVisibilityConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
      Dim flag = False
      If TypeOf value Is Boolean Then
        flag = CBool(value)
      End If

      Return If(flag, Visibility.Visible, Visibility.Collapsed)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
      If TypeOf value Is Visibility Then
        Return CType(value, Visibility) = Visibility.Visible
      End If

      Return False
    End Function
  End Class
End Namespace
