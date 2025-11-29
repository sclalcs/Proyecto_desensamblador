' Estructura de registros básicos de la CPU (muy simplificado)
Public Class CPU
    Public EAX As UInteger = 0
    Public EBX As UInteger = 0
    Public ECX As UInteger = 0
    Public EDX As UInteger = 0
    Public EIP As UInteger = 0
    Public Stack As New Stack(Of UInteger)()
End Class

