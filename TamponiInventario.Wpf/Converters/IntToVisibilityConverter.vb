Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data

Namespace TamponiInventario.Wpf.Converters
  Public Class IntToVisibilityConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
      Dim number As Integer = 0
      If value IsNot Nothing AndAlso Integer.TryParse(value.ToString(), number) Then
        Return If(number > 0, Visibility.Visible, Visibility.Collapsed)
      End If

      Return Visibility.Collapsed
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
      Throw New NotSupportedException()
    End Function
  End Class
End Namespace
