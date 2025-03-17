using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LibData;

//ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}


class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    // TODO: [Read the JSON file and return the list of DNSRecords]
    static string recordsFile = @"./DNSrecords.json";
    static string recordsContent = File.ReadAllText(recordsFile);
    static List<DNSRecord>? records = JsonSerializer.Deserialize<List<DNSRecord>>(recordsContent);


    public static void start()
    {
        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]
        if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
        {
            throw new InvalidOperationException("Invalid settings in configuration file.");
        }

        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);

        Socket listener = new(serverEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        listener.Bind(serverEndpoint);

        // TODO:[Receive and print a received Message from the client]
        byte[] buffer = new byte[1024];
        int bytesReceived = listener.Receive(buffer);
        string messageJson = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
        Console.WriteLine("Received message: " + messageJson);

        // TODO:[Receive and print Hello]
        Message? receivedMessage = JsonSerializer.Deserialize<Message>(messageJson);
        if (receivedMessage != null && receivedMessage.MsgType == MessageType.Hello)
        {
            Console.WriteLine("Received Hello message from client.");
        }

        // TODO:[Send Welcome to the client]
        if (receivedMessage != null && receivedMessage.MsgType == MessageType.Hello)
        {
            // Create a Welcome message
            Message welcomeMessage = new()
            {
                MsgId = receivedMessage.MsgId + 1,
                MsgType = MessageType.Welcome,
                Content = "Welcome to the server!"
            };

            // Serialize the Welcome message to JSON
            string welcomeMessageJson = JsonSerializer.Serialize(welcomeMessage);
            byte[] welcomeMessageBytes = Encoding.ASCII.GetBytes(welcomeMessageJson);

            // Send the Welcome message to the client
            listener.SendTo(welcomeMessageBytes, clientEndpoint);
            Console.WriteLine("Sent Welcome message to client.");
        }

        // TODO:[Receive and print DNSLookup]
        byte[] dnsLookupBuffer = new byte[1024];
        int dnsLookupBytesReceived = listener.Receive(dnsLookupBuffer);
        string dnsLookupMessageJson = Encoding.ASCII.GetString(dnsLookupBuffer, 0, dnsLookupBytesReceived);
        Console.WriteLine("Received DNSLookup message: " + dnsLookupMessageJson);

        Message? dnsLookupMessage = JsonSerializer.Deserialize<Message>(dnsLookupMessageJson);
        if (dnsLookupMessage != null && dnsLookupMessage.MsgType == MessageType.DNSLookup)
        {
            Console.WriteLine("Received DNSLookup message from client.");
            Console.WriteLine($"Message ID: {dnsLookupMessage.MsgId}");
            Console.WriteLine($"Content: {dnsLookupMessage.Content}");
        }


        // TODO:[Query the DNSRecord in Json file]
        if (dnsLookupMessage != null && dnsLookupMessage.MsgType == MessageType.DNSLookup)
        {
            string? lookupName = dnsLookupMessage.Content as string;
            if (!string.IsNullOrEmpty(lookupName) && records != null)
            {
                DNSRecord? foundRecord = records.FirstOrDefault(record => record.Name.Equals(lookupName, StringComparison.OrdinalIgnoreCase));

                if (foundRecord != null)
                {
                    // TODO:[If found Send DNSLookupReply containing the DNSRecord]
                    Message dnsLookupReplyMessage = new()
                    {
                        MsgId = dnsLookupMessage.MsgId + 1,
                        MsgType = MessageType.DNSLookupReply,
                        Content = foundRecord
                    };

                    // Serialize the DNSLookupReply message to JSON
                    string dnsLookupReplyMessageJson = JsonSerializer.Serialize(dnsLookupReplyMessage);
                    byte[] dnsLookupReplyMessageBytes = Encoding.ASCII.GetBytes(dnsLookupReplyMessageJson);

                    // Send the DNSLookupReply message to the client
                    listener.SendTo(dnsLookupReplyMessageBytes, clientEndpoint);
                    Console.WriteLine("Sent DNSLookupReply message to client.");
                }
                else
                {
                    // TODO:[If not found Send Error]
                    Message errorMessage = new()
                    {
                        MsgId = dnsLookupMessage.MsgId + 1,
                        MsgType = MessageType.Error,
                        Content = "DNS record not found"
                    };

                    // Serialize the Error message to JSON
                    string errorMessageJson = JsonSerializer.Serialize(errorMessage);
                    byte[] errorMessageBytes = Encoding.ASCII.GetBytes(errorMessageJson);

                    // Send the Error message to the client
                    listener.SendTo(errorMessageBytes, clientEndpoint);
                    Console.WriteLine("Sent Error message to client.");
                }
            }
        }


        // TODO:[Receive Ack about correct DNSLookupReply from the client]


        // TODO:[If no further requests receieved send End to the client]

    }


}