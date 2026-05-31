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

                var json = await reader.ReadLineAsync();

                if (string.IsNullOrEmpty(json))
                    return;

                var message = JsonSerializer.Deserialize<P2PMessage>(json);
                if (message == null)
                    return;


                if (message.Type == "tx")
                {
                    var tx = JsonSerializer.Deserialize<Transaction>(message.Data);

                    if (tx != null &&
                        !_blockchain.PendingTransactions.Any(t => t.Id == tx.Id))
                    {
                        var result = _blockchain.AddTransactionToMempool(tx);

                        if (result.success)
                        {
                            Console.WriteLine("[Gossip] TX received");

                            await _client.BroadcastTransactionAsync(tx);
                        }
                    }
                }


                else if (message.Type == "chain")
                {
                    var incomingChain =
                        JsonSerializer.Deserialize<List<Block>>(message.Data);

                    if (incomingChain != null)
                    {
                        Console.WriteLine("[Gossip] Chain received!");

                        if (incomingChain.Count > _blockchain.Chain.Count)
                        {
                            _blockchain.ReplaceChain(incomingChain);
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