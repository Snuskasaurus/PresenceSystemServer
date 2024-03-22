using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using System.ComponentModel;

// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server

namespace Wolcen
{
    class program
    {
        public static void Main()
        {
            Server.Execute();
        }
    }

    class Server
    {
        static string CONFIG_IP = "127.0.0.1";
        static int CONFIG_PORT = 6666;

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

        public static void SendMessageToClient(ref NetworkStream Stream, String Message)
        {
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] EncodedMessage = Encoding.UTF8.GetBytes(Message + eol);
            Stream.Write(EncodedMessage, 0, EncodedMessage.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void Execute()
        {
            TcpListener server = new TcpListener(IPAddress.Parse(CONFIG_IP), CONFIG_PORT);

            server.Start();
            Console.WriteLine("Server has started on {0}:{1}\n", CONFIG_IP, CONFIG_PORT);
            Console.WriteLine("Waiting for a connection…\n");

            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("A client connected.");

            NetworkStream stream = client.GetStream();
            while (true)
            {
                while (!stream.DataAvailable) ;
                while (client.Available < 3) ;

                byte[] BytesReceived = new byte[client.Available];
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
                    ulong offset = 2;
                    ulong PayloadLenght = (ulong)(BytesReceived[1] & 0b01111111);

                    if (opcode == 1 && PayloadLenght > 0 && IsMasked)
                    {
                        byte[] decoded = new byte[PayloadLenght];
                        byte[] masks = new byte[4] { BytesReceived[offset], BytesReceived[offset + 1], BytesReceived[offset + 2], BytesReceived[offset + 3] };
                        offset += 4;

                        for (ulong i = 0; i < PayloadLenght; ++i)
                        {
                            decoded[i] = (byte)(BytesReceived[offset + i] ^ masks[i % 4]);
                        }

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("Message received:\n{0}\n", text);
                    }
                }
            }
        }
    }
}
