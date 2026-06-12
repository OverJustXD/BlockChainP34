using BlockChainP34.Models;
using BlockChainP34.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChainP34.Models;
namespace BlockChainP34.Service.P2P
{
    public class P2PServer
    {
        private readonly BlockChainService _blockchain;
        private readonly P2PClient _client;
        private TcpListener _listener;
        private bool _isRunning;
        public bool SimulateFakeSpvProof { get; set; } = false;

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
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[P2P Сервер ERROR] Помилка прийняття з'єднання: {ex.Message}");
                        Console.ResetColor();
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
                if (string.IsNullOrWhiteSpace(json)) return;

                var message = JsonSerializer.Deserialize<P2PMessage>(json);
                if (message == null) return;

                switch (message.Type)
                {
                    case "request_chain":
                        Console.WriteLine("[P2P Сервер] Інший вузол запросив наш блокчейн. Надсилаємо копію...");

                        var currentChain = _blockchain?.Chain ?? new List<Block>();
                        var chainResponse = new P2PMessage
                        {
                            Type = "chain",
                            Data = JsonSerializer.Serialize(currentChain)
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(chainResponse));
                        break;

                    case "REQUEST_HEADERS":
                        Console.WriteLine("[P2P Сервер] Отримано запит headers. Надсилаємо Merkle roots...");

                        var roots = new List<string>();
                        if (_blockchain?.Chain != null && _blockchain.Chain.Count > 0)
                        {
                            roots = _blockchain.Chain
                                .Where(b => b != null && !string.IsNullOrWhiteSpace(b.MerkleRoot))
                                .Select(b => b.MerkleRoot)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            Console.WriteLine($"[P2P Сервер] Надсилаємо {roots.Count} Merkle roots");
                        }
                        else
                        {
                            Console.WriteLine("[P2P Сервер] Увага! Блокчейн порожній або null!");
                            if (_blockchain?.Chain != null && _blockchain.Chain.Count > 0)
                            {
                                var genesisRoot = _blockchain.Chain[0]?.MerkleRoot;
                                if (!string.IsNullOrWhiteSpace(genesisRoot))
                                    roots.Add(genesisRoot);
                            }
                        }

                        var headersResponse = new P2PMessage
                        {
                            Type = "HEADERS",
                            Data = JsonSerializer.Serialize(roots)
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(headersResponse));
                        break;

                    case "chain":
                        var incomingChain = JsonSerializer.Deserialize<List<Block>>(message.Data);
                        if (incomingChain != null && _blockchain != null)
                        {
                            _blockchain.ReplaceChain(incomingChain);
                            TrustedHeaderStore.SyncFromChain(incomingChain);
                        }
                        break;

                    case "REQUEST_MEMPOOL":
                        Console.WriteLine("[P2P Сервер] Отримано запит [REQUEST_MEMPOOL]. Надсилаємо свій список транзакцій...");
                        var currentMempool = _blockchain?.PendingTransactions ?? new List<Transaction>();
                        var mempoolResponse = new P2PMessage
                        {
                            Type = "SYNC_MEMPOOL",
                            Data = JsonSerializer.Serialize(currentMempool)
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(mempoolResponse));
                        break;

                    case "SYNC_MEMPOOL":
                        var incomingTxs = JsonSerializer.Deserialize<List<Transaction>>(message.Data);
                        if (incomingTxs != null && incomingTxs.Count > 0 && _blockchain?.PendingTransactions != null)
                        {
                            foreach (var tx in incomingTxs)
                            {
                                if (!_blockchain.PendingTransactions.Any(t => t.Id == tx.Id))
                                    _blockchain.AddTransactionToMempool(tx);
                            }
                        }
                        break;

                    case "REQUEST_SPV_PROOF":
                        await HandleSpvProofRequestAsync(message.Data, writer);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[P2P Сервер ERROR] Помилка обробки запиту: {ex.Message}");
                Console.ResetColor();
            }
        }

        private async Task HandleSpvProofRequestAsync(string txId, StreamWriter writer)
        {
            var response = BuildSpvProofResponse(txId);

            var payload = new P2PMessage
            {
                Type = "SPV_RESULT",
                Data = JsonSerializer.Serialize(response)
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(payload));
        }

        private SpvProofResponse BuildSpvProofResponse(string txId)
        {
            if (SimulateFakeSpvProof)
            {
                return new SpvProofResponse
                {
                    TxId = txId,
                    TxHash = Guid.NewGuid().ToString("N"),
                    ExpectedRoot = Guid.NewGuid().ToString("N"),
                    Included = true,
                    Proof = new List<MerkleProofStep>
                    {
                        new MerkleProofStep
                        {
                            SiblingHash = Guid.NewGuid().ToString("N"),
                            IsLeftSibling = true
                        }
                    }
                };
            }

            if (_blockchain?.Chain != null && _blockchain.Chain.Count > 0)
            {
                foreach (var block in _blockchain.Chain)
                {
                    if (block?.Transactions == null) continue;

                    var tx = block.Transactions.FirstOrDefault(t => string.Equals(t.Id, txId, StringComparison.OrdinalIgnoreCase));
                    if (tx == null) continue;

                    return new SpvProofResponse
                    {
                        TxId = txId,
                        TxHash = MerkleUtilities.ComputeTransactionHash(tx),
                        ExpectedRoot = block.MerkleRoot,
                        Included = true,
                        Proof = MerkleUtilities.BuildMerkleProof(block.Transactions, txId)
                    };
                }
            }

            return new SpvProofResponse
            {
                TxId = txId,
                TxHash = string.Empty,
                ExpectedRoot = (_blockchain?.Chain != null && _blockchain.Chain.Count > 0) ? _blockchain.Chain[0].MerkleRoot : string.Empty,
                Included = false,
                Proof = new List<MerkleProofStep>()
            };
        }
    }
}