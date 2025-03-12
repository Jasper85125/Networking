using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

namespace client
{
    class Program
    {
        static void Main()
        {
            ClientUDP.Start();
        }
    }

    public class Setting
    {
        public int ServerPortNumber { get; set; }
        public string? ServerIPAddress { get; set; }
        public int ClientPortNumber { get; set; }
        public string? ClientIPAddress { get; set; }
    }

    class ClientUDP
    {
        //TODO: [Deserialize Setting.json]
        static readonly string configFile = @"../Setting.json";
        static readonly string configContent = File.ReadAllText(configFile);
        static readonly Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

        public static void Start()
        {
            //TODO: [Create endpoints and socket]
            if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
            {
                throw new InvalidOperationException("Invalid settings in configuration file.");
            }

            IPAddress clientIPAddress = IPAddress.Any;
            int clientPortNumber = setting.ClientPortNumber;
            IPEndPoint clientEndPoint = new(clientIPAddress, clientPortNumber);

            IPAddress serverIPAddress = IPAddress.Parse(setting.ServerIPAddress);
            int serverPortNumber = setting.ServerPortNumber;
            IPEndPoint serverEndPoint = new(serverIPAddress, serverPortNumber);

            //TODO: [Create and send HELLO]
            using UdpClient udpClient = new(clientEndPoint);
            // Create a HELLO message
            Message helloMessage = new()
            {
                MsgId = 1,
                MsgType = MessageType.Hello,
                Content = null
            };

            // Serialize the message to JSON
            string helloMessageJson = JsonSerializer.Serialize(helloMessage);
            byte[] helloMessageBytes = Encoding.ASCII.GetBytes(helloMessageJson);

            // Send the HELLO message to the server
            udpClient.Send(helloMessageBytes, helloMessageBytes.Length, serverEndPoint);

            Console.WriteLine("HELLO message sent to the server.");

            //TODO: [Receive and print Welcome from server]


            // TODO: [Create and send DNSLookup Message]


            //TODO: [Receive and print DNSLookupReply from server]


            //TODO: [Send Acknowledgment to Server]

            // TODO: [Send next DNSLookup to server]
            // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

            //TODO: [Receive and print End from server]
        }
    }
}