using System.Net;
using System.Net.Sockets;

string serverIp = "127.0.0.1";
int serverPort = 12345;
string filePath = "C:\\file.txt";
int timeout = 10000;
int packetSize = 1024;

const int INT_BYTES_COUNT = 4;


await StartClientAsync(serverIp, serverPort, filePath, timeout, packetSize);


static async Task StartClientAsync(string ip, int port, string filePath, int timeout, int packetSize)
{
    try
    {
        var tcpClient = new TcpClient(ip, port);

        Console.WriteLine($"Connected to server at {ip}:{port}");

        using (tcpClient)
        using (NetworkStream networkStream = tcpClient.GetStream())
        using (BinaryWriter writer = new BinaryWriter(networkStream))
        {
            var udpPort = GetAvailableUdpPort();

            writer.Write(Path.GetFileName(filePath));
            writer.Write(udpPort);

            UdpClient udpClient = new UdpClient();
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), udpPort);

            udpClient.Connect(serverEndPoint);

            var fileData = await File.ReadAllBytesAsync(filePath);

            var finalPacketSize = packetSize - INT_BYTES_COUNT;

            await SendFileAsync(udpClient, networkStream, fileData, finalPacketSize, timeout, 5);

            networkStream.Close();
        }
    }
    catch(Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
   
}

static async Task SendFileAsync(UdpClient udpClient, NetworkStream networkStream, byte[] fileData, int packetSize, int timeout, int retriesCount)
{
    int numPackets = (int)Math.Ceiling((double)fileData.Length / packetSize);

    for (int packetId = 0; packetId < numPackets; packetId++)
    {
        int offset = packetId * packetSize;
        int length = Math.Min(packetSize, fileData.Length - offset);
        byte[] packet = new byte[length + INT_BYTES_COUNT];
        Buffer.BlockCopy(BitConverter.GetBytes(packetId), 0, packet, 0, INT_BYTES_COUNT);
        Buffer.BlockCopy(fileData, offset, packet, INT_BYTES_COUNT, length);

        bool acknowledged = false;
        int retries = 0;

        while (!acknowledged && retries < retriesCount)
        {
            await udpClient.SendAsync(packet, packet.Length);

            byte[] acknowledgementBuffer = new byte[INT_BYTES_COUNT];
            Task<int> acknowledgementTask = networkStream.ReadAsync(acknowledgementBuffer, 0, INT_BYTES_COUNT);
            Task timeoutTask = Task.Delay(timeout);
            Task completedTask = await Task.WhenAny(acknowledgementTask, timeoutTask);

            retries++;

            if (completedTask == timeoutTask)
                Console.WriteLine($"Packet {packetId} not acknowledged, retrying...");
            else
               acknowledged = BitConverter.ToInt32(acknowledgementBuffer) == packetId;


        }

        if (!acknowledged)
            Console.WriteLine($"Packet {packetId} not acknowledged, maximum retries exceeded. Aborting transmission.");
        else
            Console.WriteLine($"Packet {packetId} acknowledged");

    }

    udpClient.Send(new byte[0], 0);

    Console.WriteLine("File transmission completed");
}

static int GetAvailableUdpPort()
{
    TcpClient tcpClient = new TcpClient();
    tcpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
    int port = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Port;
    tcpClient.Close();
    return port;
}

