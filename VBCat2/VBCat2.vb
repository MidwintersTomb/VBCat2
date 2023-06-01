Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.RegularExpressions

Public Class VBCat2
    Dim networkStream As NetworkStream
    Dim shouldExit As Boolean = False
    Public Shared Sub Main(args As String())
        Dim vbCat2 As New VBCat2()
        If args.Length = 0 Then
            Console.WriteLine("Usage: VBCat2.exe <mode> [options]")
            Console.WriteLine("Modes:")
            Console.WriteLine("  -c <hostname> <port> <program>   Connect to a remote host")
            Console.WriteLine("  -l <port>                        Listen for incoming connections")
            Return
        End If

        Dim mode As String = args(0).ToLower()

        Select Case mode
            Case "-c"
                If args.Length < 3 Then
                    Console.WriteLine("Usage: VBCat2.exe -c <hostname> <port> <program>")
                    Return
                End If

                Dim remoteHost As String = args(1)
                Dim remotePort As Integer
                Dim program As String = args(3)
                If Integer.TryParse(args(2), remotePort) Then
                    vbCat2.ConnectToRemoteHost(remoteHost, remotePort, mode, program).Wait()
                ElseIf remotePort = 0 Then
                    Console.WriteLine("Invalid port number. Exiting...")
                ElseIf remotePort < 0 Then
                    Console.WriteLine("Invalid port number. Exiting...")
                ElseIf remotePort > 65535 Then
                    Console.WriteLine("Invalid port number. Exiting...")
                Else
                    Console.WriteLine("Invalid port number. Exiting...")
                    Return
                End If

            Case "-l"
                If args.Length < 2 Then
                    Console.WriteLine("Usage: VBCat2.exe -l <port>")
                    Return
                End If

                Dim localPort As Integer
                If Integer.TryParse(args(1), localPort) Then
                    vbCat2.ListenForConnections(localPort, mode).Wait()
                Else
                    Console.WriteLine("Invalid port number. Exiting...")
                    Return
                End If

            Case Else
                Console.WriteLine("Invalid mode.")
        End Select
    End Sub

    ' Function to connect to a remote host
    Function ConnectToRemoteHost(ByVal remoteHost As String, ByVal remotePort As Integer, ByVal mode As String, ByVal program As String) As Task
        Try
            ' Connect to the remote server
            Dim client As New TcpClient(remoteHost, remotePort)
            Console.WriteLine("Connected to server.")

            ' Set up the network stream for reading and writing
            Dim stream As NetworkStream = client.GetStream()

            ' Set up the input and output streams for the CLI program
            Dim cliProcess As New Process()
            cliProcess.StartInfo.FileName = program
            cliProcess.StartInfo.UseShellExecute = False
            cliProcess.StartInfo.RedirectStandardInput = True
            cliProcess.StartInfo.RedirectStandardOutput = True
            cliProcess.Start()

            ' Set up the StreamReader and StreamWriter for the CLI program
            Dim cliInput As StreamWriter = cliProcess.StandardInput
            Dim cliOutput As StreamReader = cliProcess.StandardOutput

            ' Forward input from CLI program to the server
            Dim serverWriter As New StreamWriter(stream, Encoding.ASCII)
            Dim serverReader As New StreamReader(stream, Encoding.ASCII)

            ' Start asynchronous reading from server and CLI program
            Dim serverReadLineTask = serverReader.ReadLineAsync()
            Dim cliReadLineTask = cliOutput.ReadLineAsync()

            ' Continuously forward input/output between CLI program and server
            While True
                Dim completedTask = Task.WaitAny(serverReadLineTask, cliReadLineTask)

                ' Check if the CLI program is terminated
                If cliProcess.HasExited Then
                    Exit While
                End If

                If completedTask = 0 Then
                    ' Input from server, forward to CLI program
                    Dim input As String = serverReadLineTask.Result

                    ' Check if the connection is closed
                    If input Is Nothing Then
                        Console.WriteLine("Connection closed by remote end.")
                        Exit While
                    End If

                    cliInput.WriteLine(input)
                    cliInput.Flush()

                    ' Start reading next input from server
                    serverReadLineTask = serverReader.ReadLineAsync()
                ElseIf completedTask = 1 Then
                    ' Output from CLI program, forward to server
                    Dim output As String = cliReadLineTask.Result

                    ' Check if the CLI program has finished executing
                    If output Is Nothing Then
                        Console.WriteLine("CLI program terminated.")
                        Exit While
                    End If

                    serverWriter.WriteLine(output)
                    serverWriter.Flush()

                    ' Start reading next output from CLI program
                    cliReadLineTask = cliOutput.ReadLineAsync()
                End If
            End While

            ' Terminate the CLI program if it is still running
            If Not cliProcess.HasExited Then
                cliProcess.Kill()
            End If
        Catch ex As Exception
            Console.WriteLine("Error: " + ex.Message)
        End Try

        Return Task.CompletedTask ' Return a completed Task
    End Function

    ' Function to listen for incoming connections
    Async Function ListenForConnections(ByVal localPort As Integer, ByVal mode As String) As Task

        If localPort = 0 Then
            Console.WriteLine("Invalid port number. Exiting...")
            Return
        End If
        If localPort < 0 Then
            Console.WriteLine("Invalid port number. Exiting...")
            Return
        End If
        If localPort > 65535 Then
            Console.WriteLine("Invalid port number. Exiting...")
            Return
        End If

        Dim server As New TcpListener(IPAddress.Any, localPort)
        server.Start()

        Console.WriteLine("Listening for incoming connections on port " & localPort)

        Dim client As TcpClient = Await server.AcceptTcpClientAsync()

        Console.WriteLine("Client connected.")

        networkStream = client.GetStream()

        Dim receiveTask As Task = ReceiveDataAsync(mode)
        Dim sendTask As Task = SendLocalCommandsAsync(mode)

        Await Task.WhenAny(receiveTask, sendTask)

        shouldExit = True
        client.Close()
        server.Stop()
    End Function

    ' Function to send local commands
    Async Function SendLocalCommandsAsync(ByVal mode As String) As Task
        While Not shouldExit
            Dim command As String = Await Console.In.ReadLineAsync()

            If mode = "-c" Then
                Continue While ' Skip sending the command to the remote session
            End If

            If networkStream IsNot Nothing Then
                Dim sendData As Byte() = Encoding.ASCII.GetBytes(command & vbLf)
                Await networkStream.WriteAsync(sendData, 0, sendData.Length)
                Await networkStream.FlushAsync()

                ' If the command is "exit", exit the program
                If command.Trim().ToLower() = "exit" Then
                    shouldExit = True
                End If
            End If
        End While
    End Function
    ' Function to receive remote commands and print output
    Async Function ReceiveDataAsync(ByVal mode As String) As Task
        Dim receivedBytes(4095) As Byte
        While Not shouldExit
            Dim bytesRead As Integer = Await networkStream.ReadAsync(receivedBytes, 0, receivedBytes.Length)
            If bytesRead = 0 Then
                ' Connection closed
                Exit While
            End If
            Dim receivedCommand As String = Encoding.ASCII.GetString(receivedBytes, 0, bytesRead)
            ' Check if the received command is the exit command
            If receivedCommand.Trim().ToLower() = "exit" Then
                shouldExit = True
                Exit While
            End If

            ' Print the received output
            ' Process and print the received output
            Dim processedOutput As String = receivedCommand.ToString()

            ' Remove "]0;" pattern
            processedOutput = Regex.Replace(processedOutput, "]0;", "")

            ' Remove "[\d+;\d+m.*?[\d+m\$" pattern
            processedOutput = Regex.Replace(processedOutput, "\[\d+;\d+m.*?\[\d+m\$", "")

            Console.Write(processedOutput)
        End While

        ' Terminate the program
        Environment.Exit(0)
    End Function
End Class