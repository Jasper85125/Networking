﻿using System.Net;
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
            byte[] receivedBytes = udpClient.Receive(ref serverEndPoint);
            
            string receivedMessageJson = Encoding.ASCII.GetString(receivedBytes);
            if (receivedMessageJson == null)
            {
                throw new InvalidOperationException("Received message is null.");
            }
            else
            {
                Console.WriteLine($"Received message from server: {receivedMessageJson}");
            }

            // Deserialize the message
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedMessageJson);

            // Print the received message
            if (receivedMessage != null && receivedMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine($"Received message from server: {receivedMessage.MsgType}");
                Console.WriteLine($"Message ID: {receivedMessage.MsgId}");
                Console.WriteLine($"Content: {receivedMessage.Content}");
            }
            else
            {
                Console.WriteLine("Received an invalid or unexpected message.");
            }

            // TODO: [Create and send DNSLookup Messages using DNSrecords]
            if (receivedMessage == null)
            {
                throw new InvalidOperationException("Received message is null.");
            }

            // Load DNS records from JSON file
            string dnsRecordsFile = @"../server/DNSrecords.json";
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
                        MsgId = receivedMessage.MsgId + 1,
                        MsgType = MessageType.DNSLookup,
                        Content = record.Name
                    };

                    // Serialize the message to JSON
                    string dnsLookupMessageJson = JsonSerializer.Serialize(dnsLookupMessage);
                    byte[] dnsLookupMessageBytes = Encoding.ASCII.GetBytes(dnsLookupMessageJson);

                    // Send the DNSLookup message to the server
                    udpClient.Send(dnsLookupMessageBytes, dnsLookupMessageBytes.Length, serverEndPoint);

                    Console.WriteLine($"DNSLookup message for {record.Name} sent to the server.");

                    //TODO: [Receive and print DNSLookupReply from server]
                    byte[] dnsLookupReplyBytes = udpClient.Receive(ref serverEndPoint);
                    string dnsLookupReplyJson = Encoding.ASCII.GetString(dnsLookupReplyBytes);

                    // Deserialize the DNSLookupReply message
                    Message? dnsLookupReplyMessage = JsonSerializer.Deserialize<Message>(dnsLookupReplyJson);

                    // Print the received DNSLookupReply message
                    if (dnsLookupReplyMessage != null && dnsLookupReplyMessage.MsgType == MessageType.DNSLookupReply)
                    {
                        Console.WriteLine($"Received DNSLookupReply from server: {dnsLookupReplyMessage.MsgType}");
                        Console.WriteLine($"Message ID: {dnsLookupReplyMessage.MsgId}");
                        Console.WriteLine($"Content: {dnsLookupReplyMessage.Content}");
                    }
                    else
                    {
                        Console.WriteLine("Received an invalid or unexpected message.");
                    }

                    //TODO: [Send Acknowledgment to Server]
                    Message ackMessage = new()
                    {
                        MsgId = receivedMessage.MsgId + 1,
                        MsgType = MessageType.Ack,
                        Content = null
                    };
                    string ackLookupMessageJson = JsonSerializer.Serialize(ackMessage);
                    byte[] ackLookupMessageBytes = Encoding.ASCII.GetBytes(ackLookupMessageJson);

                    udpClient.Send(ackLookupMessageBytes, ackLookupMessageBytes.Length, serverEndPoint);

                    Console.WriteLine($"Acknowlegement sent to the server.");
                }
            }

            // TODO: [Send next DNSLookup to server]
            // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

            //TODO: [Receive and print End from server]
            byte[] endBytes = udpClient.Receive(ref serverEndPoint);
            string endMessageJson = Encoding.ASCII.GetString(endBytes);
            if (endMessageJson == null)
            {
                throw new InvalidOperationException("Received message is null.");
            }
            else
            {
                Console.WriteLine($"Received message from server: {endMessageJson}");
            }

        }
    }
}