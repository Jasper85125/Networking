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
            try
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
                int msgId = ReceiveWelcomeMessage(udpClient, serverEndPoint);
                msgId = SendDNSLookupMessagesError(udpClient, serverEndPoint, msgId);
                msgId = SendDNSLookupMessagesError(udpClient, serverEndPoint, msgId);
                msgId = SendDNSLookupMessages(udpClient, serverEndPoint, msgId);
                ReceiveEndMessage(udpClient, serverEndPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
                throw new InvalidOperationException("Invalid settings in configuration file.");
            }
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

        private int ReceiveWelcomeMessage(Socket udpClient, IPEndPoint serverEndPoint)
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = serverEndPoint;
            int bytesReceived = udpClient.ReceiveFrom(buffer, ref remoteEndPoint);

            string receivedMessageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            if (receivedMessage != null && receivedMessage.MsgType == MessageType.Welcome && receivedMessage.MsgId == 2)
            {
                Console.WriteLine($"Received WELCOME message from server: {receivedMessage.Content}");
                Console.WriteLine($"Received WELCOME message from server: {receivedMessage.MsgId}");
                return receivedMessage.MsgId;
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }
            return 0;
        }

        private int SendDNSLookupMessages(Socket udpClient, IPEndPoint serverEndPoint, int msgId)
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
                Message dnsLookupMessage = new()
                {
                    MsgId = msgId + 1,
                    MsgType = MessageType.DNSLookup,
                    Content = record.Name
                };

                string dnsLookupMessageJson = JsonSerializer.Serialize(dnsLookupMessage);
                byte[] dnsLookupMessageBytes = Encoding.ASCII.GetBytes(dnsLookupMessageJson);

                udpClient.SendTo(dnsLookupMessageBytes, serverEndPoint);
                Console.WriteLine($"DNSLookup message for {dnsLookupMessage.MsgId} sent to the server.\n");
                Console.WriteLine($"DNSLookup message for {record.Name} sent to the server.\n");


                ReceiveDNSLookupReply(udpClient, serverEndPoint);
                msgId++;
            }
            return msgId;
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
            else if (receivedMessage != null && receivedMessage.MsgType == MessageType.Error)
            {
                Console.WriteLine($"Received ERROR message from server: {receivedMessage.Content}");
                SendAcknowledgment(udpClient, serverEndPoint, receivedMessage.MsgId);
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }
        }

        private int SendDNSLookupMessagesError(Socket udpClient, IPEndPoint serverEndPoint, int msgId)
        {
            Message dnsLookupMessage = new()
            {
                MsgId = msgId + 1,
                MsgType = MessageType.DNSLookup,
                Content = "error"
            };

            string dnsLookupMessageJson = JsonSerializer.Serialize(dnsLookupMessage);
            byte[] dnsLookupMessageBytes = Encoding.ASCII.GetBytes(dnsLookupMessageJson);

            udpClient.SendTo(dnsLookupMessageBytes, serverEndPoint);

            Console.WriteLine("DNSLookup message for error sent to the server.\n");

            ReceiveDNSLookupReply(udpClient, serverEndPoint);
            return msgId + 1;
        }

        private void SendAcknowledgment(Socket udpClient, IPEndPoint serverEndPoint, int msgId)
        {
            Message ackMessage = new()
            {
                MsgId = msgId,
                MsgType = MessageType.Ack,
                Content = null
            };

            string ackMessageJson = JsonSerializer.Serialize(ackMessage);
            byte[] ackMessageBytes = Encoding.ASCII.GetBytes(ackMessageJson);

            udpClient.SendTo(ackMessageBytes, serverEndPoint);
            Console.WriteLine($"ACK message with ID {msgId} sent to the server.\n");
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