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
        static int CONFIG_PORT = 7600;

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


            }
        }
    }

}
