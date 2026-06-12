using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockChainP34.Service.P2P
{
    public class P2PClient
    {
        private readonly List<string> _peers = new();
        private BlockChainService _blockchain;

        public void Init(BlockChainService blockchain)
        {
            _blockchain = blockchain;
        }

        public void Connect(string peerAddress)
        {
            if (string.IsNullOrWhiteSpace(peerAddress)) return;

            if (!_peers.Contains(peerAddress))
            {
                _peers.Add(peerAddress);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[P2P Клієнт] Додано новий вузол до списку: {peerAddress}");
                Console.ResetColor();

                Task.Run(async () => await RequestMempoolFromPeerAsync(peerAddress));
                Task.Run(async () => await RequestChainFromPeerAsync(peerAddress));
            }
        }

        public async Task RequestMempoolFromPeerAsync(string peer)
        {
            try
            {
                string[] parts = peer.Split(':');
                if (parts.Length != 2) return;

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(parts[0], int.Parse(parts[1]));
                var delayTask = Task.Delay(3000);

                if (await Task.WhenAny(connectTask, delayTask) == delayTask)
                {
                    throw new TimeoutException("Час очікування підключення вичерпано.");
                }

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var message = new P2PMessage { Type = "REQUEST_MEMPOOL", Data = "" };
                await writer.WriteLineAsync(JsonSerializer.Serialize(message));

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[P2P Клієнт] Авто-запит [REQUEST_MEMPOOL] надіслано до {peer}");
                Console.ResetColor();

                var responseJson = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(responseJson))
                {
                    var responseMessage = JsonSerializer.Deserialize<P2PMessage>(responseJson);
                    if (responseMessage != null && responseMessage.Type == "SYNC_MEMPOOL")
                    {
                        var incomingTxs = JsonSerializer.Deserialize<List<Transaction>>(responseMessage.Data);
                        if (incomingTxs != null && _blockchain != null)
                        {
                            foreach (var tx in incomingTxs)
                            {
                                if (!_blockchain.PendingTransactions.Any(t => t.Id == tx.Id))
                                {
                                    _blockchain.AddTransactionToMempool(tx);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[P2P Клієнт ERROR] Не вдалося запросити мемпул у {peer}: {ex.Message}");
                Console.ResetColor();
            }
        }

        public async Task RequestChainFromPeerAsync(string peer)
        {
            try
            {
                string[] parts = peer.Split(':');
                if (parts.Length != 2) return;

                using var client = new TcpClient();
                await client.ConnectAsync(parts[0], int.Parse(parts[1]));

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var message = new P2PMessage { Type = "request_chain", Data = "" };
                await writer.WriteLineAsync(JsonSerializer.Serialize(message));

                var responseJson = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(responseJson))
                {
                    var responseMessage = JsonSerializer.Deserialize<P2PMessage>(responseJson);
                    if (responseMessage != null && responseMessage.Type == "chain")
                    {
                        var incomingChain = JsonSerializer.Deserialize<List<Block>>(responseMessage.Data);
                        if (incomingChain != null && _blockchain != null)
                        {
                            _blockchain.ReplaceChain(incomingChain);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[P2P Клієнт ERROR] Не вдалося запросити ланцюг у {peer}: {ex.Message}");
                Console.ResetColor();
            }
        }

        public async Task BroadcastTransactionAsync(Transaction tx)
        {
            await Broadcast(new P2PMessage
            {
                Type = "SYNC_MEMPOOL",
                Data = JsonSerializer.Serialize(new List<Transaction> { tx })
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

            var peersToRemove = new List<string>();

            foreach (var peer in _peers)
            {
                try
                {
                    var parts = peer.Split(':');
                    if (parts.Length != 2) continue;

                    using var client = new TcpClient();
                    await client.ConnectAsync(parts[0], int.Parse(parts[1]));

                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    await writer.WriteLineAsync(json);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(
                        $"[P2P Клієнт ERROR] Не вдалося надіслати повідомлення [{message.Type}] до {peer}: {ex.Message}");
                    Console.ResetColor();

                    Console.WriteLine(
                        $"[Мережа] Вузол {peer} вимкнений. Видаляємо зі списку пірів.");

                    peersToRemove.Add(peer);
                }
            }

            foreach (var peer in peersToRemove)
            {
                _peers.Remove(peer);
            }
        }
    }
}