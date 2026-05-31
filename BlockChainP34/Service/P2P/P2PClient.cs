using BlockChainP34.Models;
using System.Net.Sockets;
using System.Text.Json;

namespace BlockChainP34.Service.P2P
{
    public class P2PClient
    {
        private readonly List<string> _peers = new();

        public void Connect(string peerAddress)
        {
            if (!_peers.Contains(peerAddress))
            {
                _peers.Add(peerAddress);
                Console.WriteLine($"Connected to peer: {peerAddress}");
            }
        }

        public async Task BroadcastTransactionAsync(Transaction tx)
        {
            await Broadcast(new P2PMessage
            {
                Type = "tx",
                Data = JsonSerializer.Serialize(tx)
            });
        }

        public async Task BroadcastChainAsync(List<Block> chain)
        {
            await Broadcast(new P2PMessage
            {
                Type = "chain",
                Data = JsonSerializer.Serialize(chain)
            });
        }

        private async Task Broadcast(P2PMessage message)
        {
            var json = JsonSerializer.Serialize(message);

            foreach (var peer in _peers)
            {
                try
                {
                    var parts = peer.Split(':');

                    using var client = new TcpClient();
                    await client.ConnectAsync(parts[0], int.Parse(parts[1]));

                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream)
                    {
                        AutoFlush = true
                    };

                    await writer.WriteLineAsync(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[P2P ERROR] {ex.Message}");
                }
            }
        }
    }
}