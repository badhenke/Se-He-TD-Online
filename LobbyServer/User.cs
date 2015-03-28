using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace LobbyServer
{
    class User
    {
        public string username { get; set; }
        public string password { get; set; }
        public string email { get; set; }
        public string image { get; set; }
        public int rating { get; set; }
        public string[] matches { get; set; }
        public TcpClient tcpClient { get; set; }

    }
}
