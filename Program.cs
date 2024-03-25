using System;
using System.Collections.Generic;
using System.Threading;

using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Runtime.InteropServices;

// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server

namespace Wolcen
{
    class program
    {
        public static void Main()
        {
            Server.GetInstance().StartWebsocketServer();
        }
    }

    struct ClientMessageHolder
    {
        int IndexClient;
        string Message;
    }

    class Server
    {

        private Server() { }
        private static Server Instance;
        public static Server GetInstance()
        {
            if (Instance == null)
            {
                Instance = new Server();
            }
            return Instance;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static string   CONFIG_IP = "127.0.0.1";
        public static int      CONFIG_PORT = 6666;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public TcpListener          TcpListener;

        Dictionary<int, TcpClient> ClientConnections = new Dictionary<int, TcpClient>();
        List<ClientMessageHolder> MessagesReceived;


        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void DoWebSocketHandshake(ref NetworkStream Stream, String DataReceived)
        {
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

            byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                + "Connection: Upgrade" + eol
                + "Upgrade: websocket" + eol
                + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                    System.Security.Cryptography.SHA1.Create().ComputeHash(
                        Encoding.UTF8.GetBytes(
                            new System.Text.RegularExpressions.Regex("Sec-WebSocket-Key: (.*)").Match(DataReceived).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                        )
                    )
                )
                + eol
                + eol);

            Stream.Write(response, 0, response.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void ThreadTask_ReceiveClientStream(object client)
        {

            TcpClient tcpClient = (TcpClient)client;
            while (true)
            {
                NetworkStream stream = tcpClient.GetStream();

                while (!stream.DataAvailable) ;
                while (tcpClient.Available < 3) ;

                byte[] BytesReceived = new byte[tcpClient.Available];
                stream.Read(BytesReceived, 0, BytesReceived.Length);

                String data = Encoding.UTF8.GetString(BytesReceived);
                if (new System.Text.RegularExpressions.Regex("^GET").IsMatch(data))
                {
                    Console.WriteLine("\nHandshake received:\n{0}", data);
                    DoWebSocketHandshake(ref stream, data);
                }
                else
                {
                    String BytesInText = "";
                    for (int iByte = 0; iByte < BytesReceived.Length; iByte++)
                    {
                        BytesInText += BytesReceived[iByte] + " ";
                    }
                    Console.WriteLine("bytes received:\n{0}", BytesInText);

                    bool IsFinalFrame = (BytesReceived[0] & 0b10000000) != 0;
                    bool IsMasked = (BytesReceived[1] & 0b10000000) != 0;
                    int opcode = BytesReceived[0] & 0b00001111;
                    ulong PayloadLenght = (ulong)(BytesReceived[1] & 0b01111111);

                    if (opcode == 1 && PayloadLenght > 0 && IsMasked)
                    {
                        byte[] decoded = new byte[PayloadLenght];

                        byte[] masks = new byte[4] { BytesReceived[2], BytesReceived[3], BytesReceived[4], BytesReceived[5] };

                        for (ulong i = 0; i < PayloadLenght; ++i)
                        {
                            decoded[i] = (byte)(BytesReceived[6 + i] ^ masks[i % 4]);
                        }

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("Message received:\n{0}\n", text);
                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void ThreadTask_ListenForNewClients()
        {
            while (Thread.CurrentThread.IsAlive)
            {
                TcpClient NewClient = Server.Instance.TcpListener.AcceptTcpClient();
                Console.WriteLine("A new client connected: {0}", NewClient.Client.Handle.ToString());

                Thread ReceivingThread = new Thread(new ParameterizedThreadStart(ThreadTask_ReceiveClientStream));
                ReceivingThread.Start(NewClient);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void SendMessageToClient(ref NetworkStream Stream, String Message)
        {
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] EncodedMessage = Encoding.UTF8.GetBytes(Message + eol);
            Stream.Write(EncodedMessage, 0, EncodedMessage.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void StartWebsocketServer()
        {
            TcpListener = new TcpListener(IPAddress.Parse(CONFIG_IP), CONFIG_PORT);
            TcpListener.Start();
            Console.WriteLine("Server has started on {0}:{1}\n", CONFIG_IP, CONFIG_PORT);
            Console.WriteLine("Waiting for a connection…\n");

            Thread ListeningThread = new Thread(new ThreadStart(ThreadTask_ListenForNewClients));
            ListeningThread.Start();

        }
    }
}
