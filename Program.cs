using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.Json;

using System.Net.Sockets;
using System.Net;

using System.Diagnostics;
using Newtonsoft.Json;
using System.Text.Json.Nodes;


// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
// https://www.geeksforgeeks.org/c-sharp-multithreading/
// https://datatracker.ietf.org/doc/html/rfc6455#section-5.2

namespace Wolcen
{
    class program
    {
        public static void Main()
        {
            Server.GetInstance().StartWebsocketServer();
        }
    }

    class PresenceManager
    {
        enum playerActivity
        {
            DISCONNECTED,
            IN_MENU,
            PLAYING,
        }

        struct SPlayerData
        {
            public int NetworkIndex;
            public playerActivity Activity;
            public List<String> FriendListNames;
        }

        Dictionary<String, SPlayerData> PlayerDatasDictionary;

       
        void Initialize()
        {
            SPlayerData StartingPlayerData;

            StartingPlayerData.NetworkIndex = -1;
            StartingPlayerData.Activity = playerActivity.DISCONNECTED;
            StartingPlayerData.FriendListNames = new List<string>();
            StartingPlayerData.FriendListNames.Add("Player2");
            PlayerDatasDictionary.Add("Player1", StartingPlayerData);

            StartingPlayerData.FriendListNames = new List<string>();
            StartingPlayerData.FriendListNames.Add("Player2");
            StartingPlayerData.FriendListNames.Add("Player2");
            PlayerDatasDictionary.Add("Player2", StartingPlayerData);

            StartingPlayerData.FriendListNames = new List<string>();
            StartingPlayerData.FriendListNames.Add("Player2");
            StartingPlayerData.FriendListNames.Add("Player2");
            PlayerDatasDictionary.Add("Player3", StartingPlayerData);
        }

        void DisconnectPlayer(String PlayerName)
        {

        }

        void ConnectPlayer(String PlayerName)
        {

        }

        void ChangePlayerActivity(String PlayerName, playerActivity PlayerActivity)
        {

        }

    }

    class Server
    {
        // Singleton
        
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
        
        struct ClientConnectionHolder
        {
            public int IndexClient;
            public TcpClient Connection;
            public bool HasConnected;
            public bool HasDisconnected;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        struct TRequestObject
        {
            public string playerName { get; set; }
            public string requestType { get; set; }
            public string Content { get; set; }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        struct ClientMessageHolder
        {
            public int IndexClient;
            public string Message;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // Configs variables

        public static string    CONFIG_IP = "127.0.0.1";
        public static int       CONFIG_PORT = 6666;
        public static bool      CONFIG_ENABLE_DEEP_LOGS = false;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public TcpListener          TcpListener;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        struct TMessageReceived
        {
            public int ThreadId;
            public List<ClientMessageHolder> Messages;
        };

        // Multi-threaded variables


        private List<ClientConnectionHolder> ClientConnections = new List<ClientConnectionHolder>();
        private List<ClientMessageHolder> MessagesReceived = new List<ClientMessageHolder>();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // Events Methods

        void OnClientConnected(int ClientIndex)
        {
            Console.WriteLine("Client{0} Connected\n", ClientIndex);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void OnClientDisconnected(int ClientIndex)
        {
            Console.WriteLine("Client{0} disconnected\n", ClientIndex);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void OnClientMessageReceived(int ClientIndex, string Message)
        {
            Console.WriteLine("client{0} Messaged:\n {1}\n", ClientIndex, Message);

            TRequestObject NewRequest = JsonConvert.DeserializeObject<TRequestObject>(Message);
            if (NewRequest.requestType == "ChangeActivity")
            {
                string MessageToSend = "Roger that my commander " + MessagesReceived[0].Message;
                SendMessageToClient(ClientIndex, MessageToSend);
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // Internal Methods

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
                    if (CONFIG_ENABLE_DEEP_LOGS)
                    {
                        Console.WriteLine("\nHandshake received:\n{0}", data);
                    }
                    DoWebSocketHandshake(ref stream, data);
                }
                else
                {
                    String BytesInText = "";
                    for (int iByte = 0; iByte < BytesReceived.Length; iByte++)
                    {
                        BytesInText += BytesReceived[iByte] + " ";
                    }
                    if (CONFIG_ENABLE_DEEP_LOGS)
                    {
                        Console.WriteLine("bytes received:\n{0}", BytesInText);
                    }

                    bool IsFinalFrame = (BytesReceived[0] & 0b10000000) != 0;
                    bool IsMasked = (BytesReceived[1] & 0b10000000) != 0;
                    int opcode = BytesReceived[0] & 0b00001111;
                    ulong PayloadLenght = (ulong)(BytesReceived[1] & 0b01111111);

                    if (PayloadLenght > 0 && IsMasked)
                    {
                        if (opcode == 1) // Text frame
                        {
                            byte[] decoded = new byte[PayloadLenght];

                            byte[] masks = new byte[4] { BytesReceived[2], BytesReceived[3], BytesReceived[4], BytesReceived[5] };

                            for (ulong i = 0; i < PayloadLenght; ++i)
                            {
                                decoded[i] = (byte)(BytesReceived[6 + i] ^ masks[i % 4]);
                            }

                            lock (Server.Instance.MessagesReceived)
                            {
                                //Debug.Assert(Server.Instance.MessagesReceived.ThreadId == Thread.CurrentThread.ManagedThreadId);

                                ClientMessageHolder NewMessage;
                                NewMessage.IndexClient = tcpClient.Client.Handle.ToInt32();
                                NewMessage.Message = Encoding.UTF8.GetString(decoded);
                                Server.Instance.MessagesReceived.Add(NewMessage);
                            }
                        }
                        else if (opcode == 8) // Connection close
                        {
                            int IndexClientDisconnecting = tcpClient.Client.Handle.ToInt32();
                            lock (Server.Instance.ClientConnections)
                            {
                                int iClientConnection = Server.Instance.ClientConnections.FindIndex(Connection => Connection.IndexClient==IndexClientDisconnecting);
                                if (iClientConnection >= 0)
                                {
                                    ClientConnectionHolder CurrentClientConnection = Server.Instance.ClientConnections[iClientConnection];
                                    CurrentClientConnection.HasDisconnected = true;
                                    Server.Instance.ClientConnections[iClientConnection] = CurrentClientConnection;
                                }
                                else
                                {
                                    Debug.Assert(false);
                                }
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
                if(CONFIG_ENABLE_DEEP_LOGS)
                {
                    Console.WriteLine("A new client connected: {0}", NewClient.Client.Handle.ToString());
                }

                ClientConnectionHolder NewConnection;
                NewConnection.IndexClient = NewClient.Client.Handle.ToInt32();
                NewConnection.Connection = NewClient;
                NewConnection.HasConnected = true;
                NewConnection.HasDisconnected = false;

                lock (Server.Instance.ClientConnections)
                {
                    Server.Instance.ClientConnections.Add(NewConnection);
                }

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

        public static void SendMessageToClient(int ClientIndex, String Message)
        {
            for (int i = 0; i < Server.Instance.ClientConnections.Count; i++)
            {
                if (Server.Instance.ClientConnections[i].IndexClient == ClientIndex)
                {
                    byte[] EncodedMessage = EncodeMessageToSend(Message);
                    Server.Instance.ClientConnections[i].Connection.GetStream().Write(EncodedMessage, 0, EncodedMessage.Length);
                    return;
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void StartWebsocketServer()
        {
            MessagesReceived = new List<ClientMessageHolder>();

            TcpListener = new TcpListener(IPAddress.Parse(CONFIG_IP), CONFIG_PORT);
            TcpListener.Start();

            Console.WriteLine("Server has started on {0}:{1}\n", CONFIG_IP, CONFIG_PORT);
            Console.WriteLine("Waiting for a connection\n");

            Thread ListeningThread = new Thread(new ThreadStart(ThreadTask_ListenForNewClients));
            ListeningThread.Start();

            while (true)
            {
                // Add clients waiting for connections and remove client waiting for disconnections
                lock (Server.Instance.ClientConnections)
                {
                    for (int i = ClientConnections.Count - 1; i >= 0; i--)
                    {
                        if (ClientConnections[i].HasDisconnected == true) // Disconnections
                        {
                            OnClientDisconnected(ClientConnections[i].IndexClient);
                            ClientConnections.RemoveAt(i);
                        }
                        else if (ClientConnections[i].HasConnected == true) // New connection
                        {
                            OnClientConnected(ClientConnections[i].IndexClient);
                            ClientConnectionHolder NewConnection = ClientConnections[i];
                            NewConnection.HasConnected = false;
                            ClientConnections[i] = NewConnection;
                        }
                    }
                }

                // Read and dispatch the received messages
                while (MessagesReceived.Count > 0)
                {
                    lock (Server.Instance.MessagesReceived)
                    {
                       if (Server.Instance.MessagesReceived.Count > 0)
                       {
                           OnClientMessageReceived(Server.Instance.MessagesReceived[0].IndexClient,
                                          Server.Instance.MessagesReceived[0].Message);
                           MessagesReceived.RemoveAt(0);
                       }
                    }
                }
            }
        }
    }
}
