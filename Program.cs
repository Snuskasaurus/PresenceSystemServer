using System;
using System.Collections.Generic;
using System.Threading;

using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

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

    struct ClientConnectionHolder
    {
        public int IndexClient;
        public TcpClient Connection;
    }

    struct ClientMessageHolder
    {
        public int IndexClient;
        public string Message;
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

        private List<ClientConnectionHolder> ClientConnections = new List<ClientConnectionHolder>();
        private List<ClientMessageHolder> MessagesReceived = new List<ClientMessageHolder>();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static int FindClientIndexFromConnection(ref TcpClient Connection)
        {
            foreach (ClientConnectionHolder ClientConnection in Server.Instance.ClientConnections)
            {
                if (ClientConnection.Connection == Connection) 
                { 
                    return ClientConnection.IndexClient;
                }
            }
            return -1;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static TcpClient FindClienConnectionFromIndex(int Index)
        {
            for (int i = 0; i < Server.Instance.ClientConnections.Count; i++)
            {
                if (Server.Instance.ClientConnections[i].IndexClient == Index)
                {
                    return Server.Instance.ClientConnections[i].Connection;
                }
            }
            throw new Exception("Client not found");
        }

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

                        lock(Server.Instance.MessagesReceived)
                        {
                            ClientMessageHolder NewMessage;
                            NewMessage.IndexClient = FindClientIndexFromConnection(ref tcpClient);
                            if (NewMessage.IndexClient >= 0)
                            {
                                NewMessage.Message = Encoding.UTF8.GetString(decoded);
                                Server.Instance.MessagesReceived.Add(NewMessage);
                            }
                        }
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

                ClientConnectionHolder NewConnection;
                NewConnection.IndexClient = NewClient.Client.Handle.ToInt32();
                NewConnection.Connection = NewClient;
                Server.Instance.ClientConnections.Add(NewConnection);

                Thread ReceivingThread = new Thread(new ParameterizedThreadStart(ThreadTask_ReceiveClientStream));
                ReceivingThread.Start(NewClient);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static Byte[] EncodeMessageToSend(String message)
        {
            Byte[] response;
            Byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            Byte[] frame = new Byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = (Byte)129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (Byte)127;
                frame[2] = (Byte)((length >> 56) & 255);
                frame[3] = (Byte)((length >> 48) & 255);
                frame[4] = (Byte)((length >> 40) & 255);
                frame[5] = (Byte)((length >> 32) & 255);
                frame[6] = (Byte)((length >> 24) & 255);
                frame[7] = (Byte)((length >> 16) & 255);
                frame[8] = (Byte)((length >> 8) & 255);
                frame[9] = (Byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new Byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the response
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void SendMessageToClient(NetworkStream Stream, String Message)
        {
            //const string eol = "\r\n";
            byte[] EncodedMessage = EncodeMessageToSend(Message);
            Stream.Write(EncodedMessage, 0, EncodedMessage.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void StartWebsocketServer()
        {
            TcpListener = new TcpListener(IPAddress.Parse(CONFIG_IP), CONFIG_PORT);
            TcpListener.Start();
            Console.WriteLine("Server has started on {0}:{1}\n", CONFIG_IP, CONFIG_PORT);
            Console.WriteLine("Waiting for a connection\n");

            Thread ListeningThread = new Thread(new ThreadStart(ThreadTask_ListenForNewClients));
            ListeningThread.Start();

            while (true)
            {
                
                while (MessagesReceived.Count > 0)
                {
                    lock (Server.Instance.MessagesReceived)
                    {
                        Console.WriteLine("Messagge from client{0}: {1}\n",
                            MessagesReceived[0].IndexClient, MessagesReceived[0].Message);

                        TcpClient clientConnection = FindClienConnectionFromIndex(MessagesReceived[0].IndexClient);
                        string MessageToSend = "Roger that my commander " + MessagesReceived[0].Message;
                        SendMessageToClient(clientConnection.GetStream(), MessageToSend);

                        MessagesReceived.RemoveAt(0);
                    }
                }
            }
        }
    }
}
