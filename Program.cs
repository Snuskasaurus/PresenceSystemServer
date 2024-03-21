using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

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

        public static void RespondToWebSocketHandshake(ref NetworkStream Stream, String DataReceived)
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
                ) + eol
                + eol);

            Stream.Write(response, 0, response.Length);
        }

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


                byte[] bytes = new byte[client.Available];

                stream.Read(bytes, 0, bytes.Length);
                String data = Encoding.UTF8.GetString(bytes);
                Console.WriteLine("Data Received fromn client:\n{0}", data);

                if (new System.Text.RegularExpressions.Regex("^GET").IsMatch(data))
                {
                    RespondToWebSocketHandshake(ref stream, data);
                }
                else
                {

                }

            }
        }
    }

}
