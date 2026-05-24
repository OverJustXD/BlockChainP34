using BlockChainP34.Models;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace BlockChainP34.Service.P2P
{
    public class P2PServer
    {
        private readonly BlockChainService _blockchain;
        private readonly P2PClient _client;

        public P2PServer(
            BlockChainService blockchain,
            P2PClient client)
        {
            _blockchain = blockchain;
            _client = client;
        }

        public void Start(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);

            listener.Start();

            Console.WriteLine($"P2P Server started on port {port}");

            Task.Run(async () =>
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();

                    _ = HandleClientAsync(client);
                }
            });
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);

                var jsonTransaction = await reader.ReadLineAsync();

                if (!string.IsNullOrEmpty(jsonTransaction))
                {
                    var tx = JsonSerializer.Deserialize<Transaction>(jsonTransaction);

                    if (tx != null &&
                        !_blockchain.PendingTransactions.Any(t => t.Id == tx.Id))
                    {
                        var result = _blockchain.AddTransactionToMempool(tx);

                        if (result.success)
                        {
                            Console.WriteLine("[Gossip] Transaction received!");
                            Console.WriteLine("[Gossip] Forwarding to other peers...");

                            await _client.BroadcastTransactionAsync(tx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }
    }
}