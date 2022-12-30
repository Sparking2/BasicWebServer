using System.Net.Sockets;
using System.Text;

namespace BasicWebServer;

public class WebServer
{
    private readonly TcpListener? _tcpListener;
    private const ushort Port = 5050;

    public WebServer()
    {
        try
        {
            _tcpListener = TcpListener.Create(Port);
            _tcpListener.Start();
            Console.WriteLine("Web Server Running... Press ^C to Stop...");
            var th = new Thread(StartListen);
            th.Start();
        }
        catch ( Exception e )
        {
            Console.WriteLine($"An Exception Occurred while Listening: {e}");
        }
    }

    private string? GetDefaultFileName( string? localDirectory )
    {
        string? line;
        try
        {
            var sr = new StreamReader($"{localDirectory}\\Default.Dat");
            while ( ( line = sr.ReadLine() ) != null )
            {
                if ( File.Exists(localDirectory + line) )
                    break;
            }
        }
        catch ( Exception e )
        {
            Console.WriteLine($"An Exception Occurred : {e}");
            throw;
        }

        return File.Exists(localDirectory + line) ? line : "";
    }

    private string? GetLocalPath( string webServerRoot, string directoryName )
    {
        string? virtualDir = null;
        string? realDir = null;
        directoryName = directoryName.Trim();
        webServerRoot = webServerRoot.ToLower();
        directoryName = directoryName.ToLower();
        try
        {
            var sr = new StreamReader($"{webServerRoot}\\VDirs.Dat");
            while ( sr.ReadLine() is { } line )
            {
                line = line.Trim();
                if ( line.Length <= 0 ) continue;
                int startPos = line.IndexOf(";", StringComparison.Ordinal);
                line = line.ToLower();
                virtualDir = line[..startPos];
                realDir = line[( startPos + 1 )..];
                if ( virtualDir == directoryName )
                {
                    break;
                }
            }
        }
        catch ( Exception e )
        {
            Console.Write($"An Exception Occurred: {e}");
        }

        return virtualDir == directoryName
            ? realDir
            : "";
    }

    private string? GetMimeType( string? requestedFile )
    {
        if ( requestedFile == null ) return null;
        var mimeType = "";
        var mimeExt = "";
        requestedFile = requestedFile.ToLower();
        int startPos = requestedFile.IndexOf(".", StringComparison.Ordinal);
        string fileExt = requestedFile[startPos..];
        try
        {
            var sr = new StreamReader("D:\\SharpWebServer\\Mime.Dat");
            while ( sr.ReadLine() is { } line )
            {
                line = line.Trim();
                if ( line.Length <= 0 ) continue;
                startPos = line.IndexOf(";", StringComparison.Ordinal);
                line = line.ToLower();
                mimeExt = line[..startPos];
                mimeType = line[( startPos + 1 )..];
                if ( mimeExt == fileExt )
                    break;
            }
        }
        catch ( Exception e )
        {
            Console.WriteLine($"An Exception Occurred : {e}");
            throw;
        }

        return mimeExt == fileExt
            ? mimeType
            : "";
    }

    private void SendHeader(
        string httpVersion,
        string? mimeHeader,
        int totalBytes,
        string statusCode,
        ref Socket? socket
    )
    {
        var buffer = "";
        if ( mimeHeader?.Length == 0 )
        {
            mimeHeader = "text/html";
        }

        buffer += httpVersion + statusCode + "\r\n";
        buffer += "Server: cx1193719-b\r\n";
        buffer += "Content-Type: " + mimeHeader + "\r\n";
        buffer += "Accept-Ranges: bytes\r\n";
        buffer += "Content-Length: " + totalBytes + "\r\n\r\n";
        Byte[] sendData = Encoding.ASCII.GetBytes(buffer);
        SendToBrowser(sendData, ref socket);
        Console.WriteLine($"Total Bytes: {totalBytes}");
    }

    private void SendToBrowser( string sendData, ref Socket? socket )
    {
        SendToBrowser(Encoding.ASCII.GetBytes(sendData), ref socket);
    }

    private void SendToBrowser( byte[] sendData, ref Socket? socket )
    {
        try
        {
            if ( socket is {Connected: true,} )
            {
                int numBytes;
                if ( ( numBytes = socket.Send(sendData, sendData.Length, 0) ) == -1 )
                    Console.WriteLine("Socket Error cannot Send Packet");
                else
                    Console.WriteLine("No. of bytes send {0}", numBytes);
            }
            else
                Console.WriteLine("Connection Dropped...");
        }
        catch ( Exception e )
        {
            Console.WriteLine("Error Occurred : {0}", e);
        }
    }

    private void StartListen()
    {
        const string myWebServerRoot = "D:\\SharpWebServer\\";
        const string formattedMessage = "";
        while ( true )
        {
            Socket? mySocket = _tcpListener?.AcceptSocket();
            if ( mySocket == null ) return;

            if ( mySocket is {Connected: false,} ) continue;           
            Console.WriteLine("Socket Type " + mySocket.SocketType);

            Console.WriteLine("\nClient Connected!!\n===============\nClient IP {0}\n",
                mySocket.RemoteEndPoint);
            var receive = new byte[ 1024 ];
            mySocket.Receive(receive, receive.Length, 0);
            string buffer = Encoding.ASCII.GetString(receive);
            if ( buffer[..3] != "GET" )
            {
                Console.WriteLine("Only Get Method is supported");
                mySocket.Close();
            }

            int startPos = buffer.IndexOf("HTTP", 1, StringComparison.Ordinal);
            string httpVersion = buffer.Substring(startPos, 8);
            string request = buffer.Substring(0, startPos - 1);
            request = request.Replace("\\", "/");
            if ( request.IndexOf(".", StringComparison.Ordinal) < 1 && !request.EndsWith("/") )
            {
                request += "/";
            }

            startPos = request.LastIndexOf("/", StringComparison.Ordinal) + 1;
            string? requestedFile = request.Substring(startPos);
            string dirName = request.Substring(request.IndexOf("/", StringComparison.Ordinal),
                request.LastIndexOf("/", StringComparison.Ordinal) - 3);

            /////////////////////////////////////////////////////////////////////  
            // Identify the Physical Directory  
            /////////////////////////////////////////////////////////////////////  
            string? localDir = dirName == "/" ? myWebServerRoot : GetLocalPath(myWebServerRoot, dirName);

            Console.WriteLine("Directory Requested : " + localDir);
            string errorMessage;
            if ( localDir is {Length: 0,} )
            {
                errorMessage = "<H2>Error!! Requested Directory does not exists</H2><Br>";
                SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", ref mySocket);
                SendToBrowser(errorMessage, ref mySocket);
                mySocket?.Close();
                continue;
            }

            /////////////////////////////////////////////////////////////////////  
            // Identify the File Name  
            /////////////////////////////////////////////////////////////////////  
            //If The file name is not supplied then look in the default file list  
            if ( requestedFile.Length == 0 )
            {
                requestedFile = GetDefaultFileName(localDir);
                if ( requestedFile == "" )
                {
                    errorMessage = "<H2>Error!! No Default File Name Specified</H2>";
                    SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", ref mySocket);
                    SendToBrowser(errorMessage, ref mySocket);
                    mySocket?.Close();
                    return;
                }
            }

            /////////////////////////////////////////////////////////////////////  
            // Get TheMime Type  
            /////////////////////////////////////////////////////////////////////  
            string? mimeType = GetMimeType(requestedFile);
            string physicalFilePath = localDir + requestedFile;
            Console.WriteLine("File Requested: " + physicalFilePath);

            if ( File.Exists(physicalFilePath) == false )
            {
                errorMessage = "<H2>404 Error! File Does Not Exists...</H2>";
                SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", ref mySocket);
                SendToBrowser(errorMessage, ref mySocket);
                Console.WriteLine(formattedMessage);
            }
            else
            {
                var totalBytes = 0;
                var fs = new FileStream(physicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var reader = new BinaryReader(fs);
                var bytes = new byte[ fs.Length ];
                int read;
                while ( ( read = reader.Read(bytes, 0, bytes.Length) ) != 0 )
                {
                    Encoding.ASCII.GetString(bytes, 0, read);
                    totalBytes += read;
                }

                reader.Close();
                fs.Close();
                SendHeader(httpVersion, mimeType, totalBytes, " 200 OK", ref mySocket);
                SendToBrowser(bytes, ref mySocket);
            }

            mySocket?.Close();
        }
    }
}