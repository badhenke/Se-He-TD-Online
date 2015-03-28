using SocketEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LobbyLogin
{
    class CommonMethods
    {

        private static System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();

        //Skicka meddelande
        public static void send(TcpClient tcpClient, string message)
        {
            var stream = tcpClient.GetStream();
            byte[] buffer = encoding.GetBytes(message);

            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        //Returnerar en socket anslutning till servern
        public static TcpClient createConnection(string ip, int port)
        {
            TcpClient newTcpClient = new TcpClient(ip, port);
            return newTcpClient;
        }

    }
}
