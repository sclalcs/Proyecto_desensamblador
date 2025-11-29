Imports System.IO
Imports SharpDisasm
Imports SharpDisasm.Udis86
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Text.RegularExpressions

Public Class Form1
    Private Instrucciones As New List(Of String)

    ' Panel contenedor del PictureBox (con scroll)
    Private pnlScroll As Panel

    Private Sub frmDesensamblador_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

        Me.WindowState = FormWindowState.Maximized
        Me.MaximizeBox = False
        Me.MinimizeBox = False




        TextBox1.Multiline = True
        TextBox1.ScrollBars = ScrollBars.Vertical
        TextBox1.Font = New Font("Consolas", 9)
        TextBox1.WordWrap = False
        AddHandler TextBox1.MouseUp, AddressOf TextBox1_ActualizarSeleccion
        AddHandler TextBox1.KeyUp, AddressOf TextBox1_ActualizarSeleccion

        ' Panel con scroll para el PictureBox
        pnlScroll = New Panel()
        pnlScroll.AutoScroll = True
        pnlScroll.Dock = DockStyle.Bottom
        pnlScroll.Height = 400

        PictureBox1.Dock = DockStyle.None
        PictureBox1.SizeMode = PictureBoxSizeMode.AutoSize
        pnlScroll.Controls.Add(PictureBox1)

        Me.Controls.Add(pnlScroll)
    End Sub

    '=== BOTÓN DESENSAMBLAR ===
    Private Sub btndesensamblador_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        Try
            Dim ofd As New OpenFileDialog()
            ofd.Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
            If ofd.ShowDialog() <> Windows.Forms.DialogResult.OK Then Exit Sub

            Dim filePath As String = ofd.FileName
            Dim offset As Integer = 0
            Dim bytesToRead As Integer = 256

            Integer.TryParse(TextBox2.Text, offset)
            Integer.TryParse(TextBox3.Text, bytesToRead)

            Dim fileBytes As Byte() = File.ReadAllBytes(filePath)
            If offset >= fileBytes.Length Then
                MessageBox.Show("El offset está fuera del tamaño del archivo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Exit Sub
            End If

            Dim length As Integer = Math.Min(bytesToRead, fileBytes.Length - offset)
            Dim buffer(length - 1) As Byte
            Array.Copy(fileBytes, offset, buffer, 0, length)

            Dim disasm As New Disassembler(buffer, ArchitectureMode.x86_32, offset, True)
            Dim sb As New System.Text.StringBuilder()
            Instrucciones.Clear()

            For Each ins As Instruction In disasm.Disassemble()
                Dim addr As String = "0x" & ins.Offset.ToString("X8")
                Dim rawMnemonic As String = ins.Mnemonic.ToString()
                Dim mnem As String = NormalizeMnemonic(rawMnemonic)

                Dim operandos As String = ""
                Try
                    Dim propOperands = ins.GetType().GetProperty("Operands")
                    If propOperands IsNot Nothing Then
                        Dim rawOpsObj As Object = propOperands.GetValue(ins, Nothing)
                        Dim arr As Array = TryCast(rawOpsObj, Array)
                        If arr IsNot Nothing AndAlso arr.Length > 0 Then
                            Dim parts As New List(Of String)
                            For i As Integer = 0 To arr.Length - 1
                                Dim opObj As Object = arr.GetValue(i)
                                If opObj Is Nothing Then Continue For
                                Dim txt As String = opObj.ToString()
                                If String.IsNullOrEmpty(txt) OrElse txt = opObj.GetType().ToString() Then
                                    txt = ConstruirOperandoDesdePropiedades(opObj)
                                End If
                                parts.Add(txt)
                            Next
                            operandos = String.Join(", ", parts.ToArray())
                        End If
                    End If
                Catch
                    operandos = ""
                End Try

                operandos = NormalizeOperandsString(operandos)

                Dim comentario As String = ""
                If mnem = "mov" Then
                    Try
                        Dim parts() As String = operandos.Split(","c)
                        If parts.Length >= 2 Then
                            Dim dest As String = parts(0).Trim()
                            Dim src As String = parts(1).Trim()
                            comentario = "; mover " & src & " a " & dest & "."
                        End If
                    Catch
                        comentario = ""
                    End Try
                Else
                    Select Case mnem
                        Case "add" : comentario = "; sumar valores."
                        Case "sub" : comentario = "; restar valores."
                        Case "pop" : comentario = "; extraer de la pila."
                        Case "push" : comentario = "; insertar en la pila."
                        Case "nop" : comentario = "; no operación."
                        Case "call" : comentario = "; llamada a subrutina."
                        Case "jmp" : comentario = "; salto incondicional."
                        Case "cmp", "test" : comentario = "; comparar/testear."
                        Case Else : comentario = ""
                    End Select
                End If

                Dim linea As String = addr & " " & mnem
                If operandos <> "" Then linea &= " " & operandos
                If comentario <> "" Then linea &= " " & comentario

                sb.AppendLine(linea)
                Instrucciones.Add(linea)
            Next

            TextBox1.Text = sb.ToString()
            PictureBox1.Image = Nothing
            PictureBox1.Size = pnlScroll.ClientSize

        Catch ex As Exception
            MessageBox.Show("Error al desensamblar: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    '=== EVENTO SELECCIÓN ===
    Private Sub TextBox1_ActualizarSeleccion(ByVal sender As Object, ByVal e As EventArgs)
        If chkGlobal.Checked Then
            Dim img As Image = DibujarOrganigramaGlobal()
            PictureBox1.Image = img
            PictureBox1.Size = img.Size
        Else
            Dim linea As String = ObtenerLineaSeleccionada(TextBox1)
            If String.IsNullOrEmpty(linea) Then
                PictureBox1.Image = Nothing
                Return
            End If
            Dim instr As String = ExtraerInstruccion(linea)
            PictureBox1.Image = DibujarOrganigramaLocal(instr)
            PictureBox1.Size = pnlScroll.ClientSize
        End If
    End Sub

    Private Function ObtenerLineaSeleccionada(ByVal tb As TextBox) As String
        Try
            Dim selStart As Integer = Math.Max(0, tb.SelectionStart)
            Dim lineIndex As Integer = tb.GetLineFromCharIndex(selStart)
            Dim lines() As String = tb.Lines
            If lines Is Nothing OrElse lineIndex < 0 OrElse lineIndex >= lines.Length Then
                Return String.Empty
            End If
            Return lines(lineIndex).Trim()
        Catch
            Return String.Empty
        End Try
    End Function

    Private Function ExtraerInstruccion(ByVal linea As String) As String
        Try
            Dim partes() As String = linea.Split(";"c)
            Dim principal As String = partes(0).Trim()
            If principal.StartsWith("0x") AndAlso principal.Length > 10 Then
                principal = principal.Substring(10).Trim()
            End If
            Return principal
        Catch
            Return ""
        End Try
    End Function

    Private Function EsDecision(ByVal instr As String) As Boolean
        Dim l As String = instr.ToLower()
        Return l.StartsWith("jmp") OrElse l.StartsWith("call") OrElse l.StartsWith("cmp") OrElse l.StartsWith("test")
    End Function

    '=== DIBUJO LOCAL ===
    Private Function DibujarOrganigramaLocal(ByVal texto As String) As Image
        Dim bmp As New Bitmap(PictureBox1.Width, PictureBox1.Height)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(Color.White)

            Dim pen As New Pen(Color.Black, 2)
            Dim brush As New SolidBrush(Color.LightBlue)
            Dim font As New Font("Consolas", 10, FontStyle.Bold)

            Dim sesDecision As Boolean = EsDecision(texto)
            Dim rect As New Rectangle(100, 60, bmp.Width - 200, 80)

            If sesDecision Then
                Dim pts() As Point = {New Point(bmp.Width \ 2, 60), New Point(bmp.Width - 120, 100), New Point(bmp.Width \ 2, 140), New Point(120, 100)}
                g.FillPolygon(brush, pts)
                g.DrawPolygon(pen, pts)
                DrawMultilineCenteredString(g, texto, font, Brushes.Black, New Rectangle(120, 70, bmp.Width - 240, 60))
            Else
                g.FillRectangle(brush, rect)
                g.DrawRectangle(pen, rect)
                DrawMultilineCenteredString(g, texto, font, Brushes.Black, rect)
            End If
        End Using
        Return bmp
    End Function

    '=== DIBUJO GLOBAL ===
    Private Function DibujarOrganigramaGlobal() As Image
        If Instrucciones.Count = 0 Then Return Nothing

        Dim blockHeight As Integer = 60
        Dim spacing As Integer = 40
        Dim blockWidth As Integer = 400
        Dim totalHeight As Integer = Instrucciones.Count * (blockHeight + spacing) + 40
        Dim totalWidth As Integer = blockWidth + 200

        Dim bmp As New Bitmap(totalWidth, totalHeight)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.Clear(Color.White)

            Dim pen As New Pen(Color.Black, 2)
            Dim brush As New SolidBrush(Color.LightBlue)
            Dim font As New Font("Consolas", 9, FontStyle.Bold)
            Dim centerX As Integer = totalWidth \ 2

            For i As Integer = 0 To Instrucciones.Count - 1
                Dim y As Integer = 20 + i * (blockHeight + spacing)
                Dim instr As String = ExtraerInstruccion(Instrucciones(i))
                Dim sesDecision As Boolean = EsDecision(instr)
                Dim rect As New Rectangle(centerX - blockWidth \ 2, y, blockWidth, blockHeight)

                If i > 0 Then
                    g.DrawLine(pen, centerX, y - spacing + 10, centerX, y)
                End If

                If sesDecision Then
                    Dim pts() As Point = {New Point(centerX, y), New Point(centerX + blockWidth \ 2, y + blockHeight \ 2), New Point(centerX, y + blockHeight), New Point(centerX - blockWidth \ 2, y + blockHeight \ 2)}
                    g.FillPolygon(brush, pts)
                    g.DrawPolygon(pen, pts)
                    DrawMultilineCenteredString(g, instr, font, Brushes.Black, New Rectangle(rect.Left + 10, rect.Top + 10, rect.Width - 20, rect.Height - 20))
                Else
                    g.FillRectangle(brush, rect)
                    g.DrawRectangle(pen, rect)
                    DrawMultilineCenteredString(g, instr, font, Brushes.Black, rect)
                End If
            Next
        End Using
        Return bmp
    End Function

    '=== CENTRAR TEXTO ===
    Private Sub DrawMultilineCenteredString(ByVal g As Graphics, ByVal text As String, ByVal font As Font, ByVal brush As Brush, ByVal rect As Rectangle)
        If String.IsNullOrEmpty(text) Then Return
        Dim lines As New List(Of String)
        Dim words() As String = text.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        Dim cur As New System.Text.StringBuilder()
        For Each w As String In words
            If cur.Length + w.Length + 1 > Math.Max(20, rect.Width \ 10) Then
                lines.Add(cur.ToString().Trim())
                cur.Length = 0
            End If
            cur.Append(w & " ")
        Next
        If cur.Length > 0 Then lines.Add(cur.ToString().Trim())

        Dim totalHeight As Integer = CInt(lines.Count * font.GetHeight(g))
        Dim startY As Single = rect.Top + (rect.Height - totalHeight) / 2.0F
        For i As Integer = 0 To lines.Count - 1
            Dim lineRect As New RectangleF(rect.Left, startY + i * font.GetHeight(g), rect.Width, font.GetHeight(g))
            Dim sf As New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            g.DrawString(lines(i), font, brush, lineRect, sf)
        Next
    End Sub

    '=== NORMALIZADORES ===
    Private Function NormalizeMnemonic(ByVal raw As String) As String
        If String.IsNullOrEmpty(raw) Then Return ""
        Dim s As String = raw.Trim()
        If s.StartsWith("ud_i", StringComparison.OrdinalIgnoreCase) Then s = s.Substring(4)
        If s.StartsWith("ud_", StringComparison.OrdinalIgnoreCase) Then s = s.Substring(3)
        Return s.ToLowerInvariant()
    End Function

    Private Function NormalizeOperandsString(ByVal rawOps As String) As String
        If String.IsNullOrEmpty(rawOps) Then Return ""
        Dim s As String = rawOps
        s = Regex.Replace(s, "UD_R_([A-Z0-9]+)", Function(m) m.Groups(1).Value.ToLower())
        s = Regex.Replace(s, "\bud_i([a-z0-9_]+)\b", Function(m) m.Groups(1).Value.ToLower())
        s = Regex.Replace(s, "\bud_([a-z0-9_]+)\b", Function(m) m.Groups(1).Value.ToLower())
        s = Regex.Replace(s, "\bBYTE\b", "byte ptr", RegexOptions.IgnoreCase)
        s = Regex.Replace(s, ",\s*,+", ",")
        s = Regex.Replace(s, "\s+,", ", ")
        s = s.Trim().Trim(","c)
        s = Regex.Replace(s, "\s+", " ")
        Return s
    End Function

    Private Function ConstruirOperandoDesdePropiedades(ByVal opObj As Object) As String
        Try
            Dim t As Type = opObj.GetType()
            Dim regProps As String() = {"BaseRegister", "Base", "Reg", "Register"}
            For Each pn As String In regProps
                Dim p As Reflection.PropertyInfo = t.GetProperty(pn)
                If p IsNot Nothing Then
                    Dim v = p.GetValue(opObj, Nothing)
                    If v IsNot Nothing Then Return v.ToString().ToLower()
                End If
            Next
            Dim immProps As String() = {"LvalUqword", "LvalU", "LvalS"}
            For Each ip As String In immProps
                Dim p As Reflection.PropertyInfo = t.GetProperty(ip)
                If p IsNot Nothing Then
                    Dim v = p.GetValue(opObj, Nothing)
                    If v IsNot Nothing Then Return "0x" & Convert.ToUInt64(v).ToString("X")
                End If
            Next
            Return opObj.ToString().ToLower()
        Catch
            Return "?"
        End Try
    End Function
End Class
