using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChainP34.Service;

namespace BlockChainP34.Service.P2P
{
    public class P2PServer
    {
        private readonly BlockChainService _blockchain;
        private readonly P2PClient _client;
        private TcpListener _listener;
        private bool _isRunning;

        public P2PServer(BlockChainService blockchain, P2PClient client)
        {
            _blockchain = blockchain;
            _client = client;
        }

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[P2P Сервер] Запущено успішно на порту {port}");
            Console.ResetColor();

            Task.Run(async () =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = HandleClientAsync(client);
                    }
                    catch (Exception ex)
                    {
                        if (!_isRunning) break;
                        Console.WriteLine($"[P2P Сервер ERROR] Помилка прийняття з'єднання: {ex.Message}");
                    }
                }
            });
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                var json = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(json)) return;

                var message = JsonSerializer.Deserialize<P2PMessage>(json);
                if (message == null) return;

                switch (message.Type)
                {
                    case "request_chain":
                        Console.WriteLine("[P2P Сервер] Інший вузол запросив наш блокчейн. Надсилаємо копію...");
                        var chainResponse = new P2PMessage
                        {
                            Type = "chain",
                            Data = JsonSerializer.Serialize(_blockchain.Chain)
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(chainResponse));
                        break;

                    case "chain":
                        var incomingChain = JsonSerializer.Deserialize<List<Block>>(message.Data);
                        if (incomingChain != null)
                        {
                            _blockchain.ReplaceChain(incomingChain);
                        }
                        break;

                    case "REQUEST_MEMPOOL":
                        Console.WriteLine("[P2P Сервер] Отримано запит [REQUEST_MEMPOOL]. Надсилаємо свій список транзакцій...");
                        var mempoolResponse = new P2PMessage
                        {
                            Type = "SYNC_MEMPOOL",
                            Data = JsonSerializer.Serialize(_blockchain.PendingTransactions)
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(mempoolResponse));
                        break;

                    case "SYNC_MEMPOOL":
                        var incomingTxs = JsonSerializer.Deserialize<List<Transaction>>(message.Data);
                        if (incomingTxs != null && incomingTxs.Count > 0)
                        {
                            foreach (var tx in incomingTxs)
                            {
                                if (!_blockchain.PendingTransactions.Any(t => t.Id == tx.Id))
                                {
                                    _blockchain.AddTransactionToMempool(tx);
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[P2P Сервер ERROR] Помилка обробки запиту: {ex.Message}");
            }
        }
    }
}