using System.Net.Sockets;
using System.Net;
using System;

class Program
{
    static public bool IsRunning = true;

    static void Main()
    {
        while (IsRunning == true)
        {
            Server.Start();
        }
    }
}

class Server
{
    static int WebSocketPort;

    TcpListener Listener;

    public static void Start()
    {
        TcpListener Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), WebSocketPort);
        Console.WriteLine("Server has started on 127.0.0.1:80.{0} Waiting for a connection…", Environment.NewLine);

        TcpClient client = server.AcceptTcpClient();

        Console.WriteLine("A client connected.");
    }
}