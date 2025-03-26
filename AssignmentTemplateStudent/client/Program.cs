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
            ClientUDP client = new ClientUDP();
            client.Start();
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
        private readonly Setting? setting;

        public ClientUDP()
        {
            string configFile = Path.Combine(AppContext.BaseDirectory, "../../../../Setting.json");
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);
        }

        public void Start()
        {
            if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
            {
                throw new InvalidOperationException("Invalid settings in configuration file.");
            }

            IPEndPoint clientEndPoint = new(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);
            IPEndPoint serverEndPoint = new(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);

            using Socket udpClient = new(clientEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            udpClient.Bind(clientEndPoint);

            SendHelloMessage(udpClient, serverEndPoint);
            ReceiveWelcomeMessage(udpClient, serverEndPoint);
            SendDNSLookupMessages(udpClient, serverEndPoint);
            ReceiveEndMessage(udpClient, serverEndPoint);
        }

        private void SendHelloMessage(Socket udpClient, IPEndPoint serverEndPoint)
        {
            Message helloMessage = new()
            {
                MsgId = 1,
                MsgType = MessageType.Hello,
                Content = null
            };

            string helloMessageJson = JsonSerializer.Serialize(helloMessage);
            byte[] helloMessageBytes = Encoding.ASCII.GetBytes(helloMessageJson);

            udpClient.SendTo(helloMessageBytes, serverEndPoint);
            Console.WriteLine("HELLO message sent to the server.\n");
        }

        private void ReceiveWelcomeMessage(Socket udpClient, IPEndPoint serverEndPoint)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = serverEndPoint;
            int bytesReceived = udpClient.ReceiveFrom(buffer, ref remoteEndPoint);

            string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            if (receivedMessage != null && receivedMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine($"Received WELCOME message from server: {receivedMessage.Content}");
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }
        }

        private void SendDNSLookupMessages(Socket udpClient, IPEndPoint serverEndPoint)
        {
            string dnsRecordsFile = Path.Combine(AppContext.BaseDirectory, "../../../../server/DNSrecords.json");
            string dnsRecordsContent = File.ReadAllText(dnsRecordsFile);
            var dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsRecordsContent);

            if (dnsRecords == null)
            {
                throw new InvalidOperationException("Failed to load DNS records.");
            }

            foreach (var record in dnsRecords)
            {
                if (record.Type == "A")
                {
                    Message dnsLookupMessage = new()
                    {
                        MsgId = 2,
                        MsgType = MessageType.DNSLookup,
                        Content = record.Name
                    };

                    string dnsLookupMessageJson = JsonSerializer.Serialize(dnsLookupMessage);
                    byte[] dnsLookupMessageBytes = Encoding.ASCII.GetBytes(dnsLookupMessageJson);

                    udpClient.SendTo(dnsLookupMessageBytes, serverEndPoint);
                    Console.WriteLine($"DNSLookup message for {record.Name} sent to the server.\n");

                    ReceiveDNSLookupReply(udpClient, serverEndPoint);
                }
            }
        }

        private void ReceiveDNSLookupReply(Socket udpClient, IPEndPoint serverEndPoint)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = serverEndPoint;
            int bytesReceived = udpClient.ReceiveFrom(buffer, ref remoteEndPoint);

            string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            if (receivedMessage != null && receivedMessage.MsgType == MessageType.DNSLookupReply)
            {
                Console.WriteLine($"Received DNSLookupReply from server: {receivedMessage.Content}");
                SendAcknowledgment(udpClient, serverEndPoint, receivedMessage.MsgId);
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }
        }

        private void SendAcknowledgment(Socket udpClient, IPEndPoint serverEndPoint, int msgId)
        {
            Message ackMessage = new()
            {
                MsgId = msgId + 1,
                MsgType = MessageType.Ack,
                Content = null
            };

            string ackMessageJson = JsonSerializer.Serialize(ackMessage);
            byte[] ackMessageBytes = Encoding.ASCII.GetBytes(ackMessageJson);

            udpClient.SendTo(ackMessageBytes, serverEndPoint);
            Console.WriteLine("Acknowledgment sent to the server.\n");
        }

        private void ReceiveEndMessage(Socket udpClient, IPEndPoint serverEndPoint)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = serverEndPoint;
            int bytesReceived = udpClient.ReceiveFrom(buffer, ref remoteEndPoint);

            string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            if (receivedMessage != null && receivedMessage.MsgType == MessageType.End)
            {
                Console.WriteLine("Received END message from server.");
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }
        }
    }
}