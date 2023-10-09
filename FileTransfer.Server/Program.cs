using System.Net.Sockets;
using System.Net;

string ip = "127.0.0.1";
int port = 12345;
string directory = "C:\\folder";

const int INT_BYTES_COUNT = 4;


await StartServer(ip, port, directory);

static async Task StartServer(string ip, int port, string directory)
{
    IPAddress ipAddress = IPAddress.Parse(ip);
    IPEndPoint tcpEndPoint = new IPEndPoint(ipAddress, port);

    TcpListener tcpListener = new TcpListener(tcpEndPoint);
    tcpListener.Start();
    Console.WriteLine($"Server started and listening on {ip}:{port}");

    while (true)
    {

        TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
        Task.Run(async () =>
        {
            await HandleClient(tcpClient, directory);
        });
    }
}

static async Task HandleClient(TcpClient tcpClient, string directory)
{
    try
    {
        Console.WriteLine($"Client connected: {tcpClient.Client.RemoteEndPoint}");

        using (tcpClient)
        using (NetworkStream networkStream = tcpClient.GetStream())
        using (BinaryReader reader = new BinaryReader(networkStream))
        {
            string fileName = reader.ReadString();
            int udpPort = reader.ReadInt32();

            UdpClient udpClient = new UdpClient(udpPort);

            var fileData = await ReceiveFileAsync(udpClient, networkStream);

            string filePath = Path.Combine(directory, fileName);
            File.WriteAllBytes(filePath, fileData.ToArray());

            Console.WriteLine($"File received and saved as {filePath}");

            networkStream.Close();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static async Task<byte[]> ReceiveFileAsync(UdpClient udpClient, NetworkStream networkStream)
{
    IPEndPoint remote = null;
    MemoryStream fileData = new MemoryStream();

    while (true)
    {

        byte[] packet = (await udpClient.ReceiveAsync()).Buffer;
        if (packet.Length == 0)
            break;
        var packetId = BitConverter.ToInt32(packet.Take(INT_BYTES_COUNT).ToArray(), 0);

        Console.WriteLine($"{packetId} packet Receive");

        await networkStream.WriteAsync(packet.Take(INT_BYTES_COUNT).ToArray(), 0, INT_BYTES_COUNT);

        await fileData.WriteAsync(packet.Skip(INT_BYTES_COUNT).ToArray(), 0, packet.Length - INT_BYTES_COUNT);
    }

    return fileData.ToArray();

}