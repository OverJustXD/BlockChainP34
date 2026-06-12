using BlockChainP34.Models;
using BlockChainP34.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChainP34.Models;
namespace BlockChainP34.Service.P2P
{
    public class P2PClient
    {
        private readonly List<string> _peers = new();
        private BlockChainService _blockchain;

        public bool IsSpvClient { get; private set; }
        public IReadOnlyList<string> Peers => _peers;


        private readonly string _peersFilePath = "peers.json";

        public void LoadPeers()
        {
            if (File.Exists(_peersFilePath))
            {
                var json = File.ReadAllText(_peersFilePath);
                var savedPeers = JsonSerializer.Deserialize<List<string>>(json);

                if (savedPeers != null && savedPeers.Count > 0)
                {
                    _peers.Clear();
                    foreach (var p in savedPeers)
                    {
                        if (!_peers.Contains(p)) _peers.Add(p);
                    }
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[DEBUG] Знайдено {savedPeers.Count} пірів у файлі: {string.Join(", ", savedPeers)}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] Файл peers.json не знайдено, пропускаємо автозавантаження.");
            }
        }

        private void SavePeers()
        {
            File.WriteAllText(_peersFilePath, JsonSerializer.Serialize(_peers));
        }

        public void Init(BlockChainService blockchain, bool spvClient = false)
        {
            _blockchain = blockchain;
            IsSpvClient = spvClient;
        }

        public void Connect(string peerAddress)
        {
            if (string.IsNullOrWhiteSpace(peerAddress)) return;

            if (!_peers.Contains(peerAddress))
            {
                _peers.Add(peerAddress);
                SavePeers();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[P2P] Збережено новий вузол: {peerAddress}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[P2P] Спроба підключення до {peerAddress}...");
            Console.ResetColor();
            if (IsSpvClient)
            {
                Task.Run(async () =>
                {
                    await RequestHeadersFromPeerAsync(peerAddress);
                    Console.WriteLine("[SPV] Заголовки синхронізовано. Тепер можна перевіряти транзакції.");
                });
            }
            else
            {
                Task.Run(async () => {
                    await RequestMempoolFromPeerAsync(peerAddress);
                    await RequestChainFromPeerAsync(peerAddress);
                    await RequestHeadersFromPeerAsync(peerAddress);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[P2P] Синхронізацію з {peerAddress} ініційовано.");
                    Console.ResetColor();
                });
            }
        }

        public async Task RequestHeadersFromPeerAsync(string peer)
        {
            try
            {
                string[] parts = peer.Split(':');
                if (parts.Length != 2) return;

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(parts[0], int.Parse(parts[1]));
                var delayTask = Task.Delay(3000);

                if (await Task.WhenAny(connectTask, delayTask) == delayTask)
                    throw new TimeoutException("Час очікування підключення вичерпано.");

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var message = new P2PMessage { Type = "REQUEST_HEADERS", Data = "" };
                await writer.WriteLineAsync(JsonSerializer.Serialize(message));

                var responseJson = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    Console.WriteLine("[DEBUG-SPV] Відповідь від сервера порожня (null або whitespace). З'єднання розірвано?");
                    return;
                }

                var responseMessage = JsonSerializer.Deserialize<P2PMessage>(responseJson);
                if (responseMessage == null)
                {
                    Console.WriteLine("[DEBUG-SPV] Помилка десеріалізації: responseMessage == null.");
                    return;
                }

                if (responseMessage.Type != "HEADERS")
                {
                    Console.WriteLine($"[DEBUG-SPV] Очікували HEADERS, а отримали: {responseMessage.Type}");
                    return;
                }

                var roots = JsonSerializer.Deserialize<List<string>>(responseMessage.Data);
                if (roots != null && roots.Count > 0)
                {
                    TrustedHeaderStore.AddRoots(roots);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[SPV/Headers] Отримано {roots.Count} trusted Merkle roots від {peer}. Загалом у сховищі: {TrustedHeaderStore.Count}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("[DEBUG-SPV] Сервер повернув порожній список коренів (Count = 0).");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"[P2P Клієнт ERROR] Не вдалося запросити headers у {peer}: {ex.Message}");
                Console.ResetColor();
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
                    throw new TimeoutException("Час очікування підключення вичерпано.");

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
                                    _blockchain.AddTransactionToMempool(tx);
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
                var connectTask = client.ConnectAsync(parts[0], int.Parse(parts[1]));
                var delayTask = Task.Delay(3000); 

                if (await Task.WhenAny(connectTask, delayTask) == delayTask)
                    throw new TimeoutException("Час очікування підключення вичерпано.");

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var message = new P2PMessage { Type = "request_chain", Data = "" };
                await writer.WriteLineAsync(JsonSerializer.Serialize(message));

                var responseJson = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(responseJson))
                {
                    var responseMessage = JsonSerializer.Deserialize<P2PMessage>(responseJson);
                    if (responseMessage != null && responseMessage.Type == "chain")
                    {
                        var incomingChain = JsonSerializer.Deserialize<List<Block>>(responseMessage.Data);
                        if (incomingChain != null && _blockchain != null)
                        {
                            _blockchain.ReplaceChain(incomingChain);
                            TrustedHeaderStore.SyncFromChain(incomingChain);
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
            if (_peers.Count == 0) return;

            await Broadcast(new P2PMessage
            {
                Type = "SYNC_MEMPOOL",
                Data = JsonSerializer.Serialize(new List<Transaction> { tx })
            });
        }

        public async Task BroadcastChainAsync(List<Block> chain)
        {
            if (_peers.Count == 0) return;

            await Broadcast(new P2PMessage
            {
                Type = "chain",
                Data = JsonSerializer.Serialize(chain)
            });
        }

        public async Task RequestSpvProofAsync(string txId)
        {
            if (!IsSpvClient)
            {
                Console.WriteLine("[SPV] Запит SPV-доказу доступний тільки в режимі SPV Client.");
                return;
            }

            if (_peers.Count == 0)
            {
                Console.WriteLine("[SPV] Спочатку підключіться до ноди.");
                return;
            }

            var peer = _peers[0];

            try
            {
                string[] parts = peer.Split(':');
                if (parts.Length != 2)
                {
                    Console.WriteLine($"[SPV] Некоректний формат адреси піра: {peer}");
                    return;
                }

                using var client = new TcpClient();

                await client.ConnectAsync(parts[0], int.Parse(parts[1])).WaitAsync(TimeSpan.FromSeconds(3));

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var request = new P2PMessage
                {
                    Type = "REQUEST_SPV_PROOF",
                    Data = txId
                };

                await writer.WriteLineAsync(JsonSerializer.Serialize(request));

                var responseJson = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    Console.WriteLine("[SPV] Порожня відповідь від ноди.");
                    return;
                }

                var responseMessage = JsonSerializer.Deserialize<P2PMessage>(responseJson);
                if (responseMessage == null || responseMessage.Type != "SPV_RESULT")
                {
                    Console.WriteLine("[SPV] Нода повернула невідомий або порожній тип відповіді.");
                    return;
                }

                var proof = JsonSerializer.Deserialize<SpvProofResponse>(responseMessage.Data);
                if (proof == null)
                {
                    Console.WriteLine("[SPV] Некоректний формат SPV_result.");
                    return;
                }

                if (!TrustedHeaderStore.Contains(proof.ExpectedRoot))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[SPV ШТОРМ] Повна нода намагалася підсунути фейковий корінь Меркла! Доказ відхилено.");
                    Console.ResetColor();

                    RemovePeer(peer);
                    return;
                }

                var isProofValid = MerkleUtilities.VerifyMerkleProof(proof.TxHash, proof.Proof, proof.ExpectedRoot);

                if (!isProofValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[SPV] Математичний Merkle proof не пройшов перевірку (хеші не збігаються).");
                    Console.ResetColor();

                    RemovePeer(peer);
                    return;
                }

                if (!proof.Included)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SPV] Транзакція {txId} не знайдена в легітимному ланцюзі.");
                    Console.ResetColor();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SPV] Успіх! Транзакція {txId} підтверджена в мережі.");
                Console.WriteLine($"[SPV] ExpectedRoot (валідний): {proof.ExpectedRoot}");
                Console.ResetColor();
            }
            catch (TimeoutException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[SPV] Час очікування підключення до ноди {peer} вичерпано.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[SPV ERROR] Не вдалося отримати SPV-доказ: {ex.Message}");
                Console.ResetColor();
            }
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
                    var connectTask = client.ConnectAsync(parts[0], int.Parse(parts[1]));
                    var delayTask = Task.Delay(2000);

                    if (await Task.WhenAny(connectTask, delayTask) == delayTask)
                        throw new TimeoutException();

                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    await writer.WriteLineAsync(json);
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"[P2P Клієнт ERROR] Не вдалося надіслати [{message.Type}] до {peer}");
                    Console.ResetColor();

                    peersToRemove.Add(peer);
                }
            }

            if (peersToRemove.Count > 0)
            {
                _peers.RemoveAll(p => peersToRemove.Contains(p));
            }
        }

        private void RemovePeer(string peer)
        {
            if (_peers.Contains(peer))
                _peers.Remove(peer);

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[SPV] З'єднання з {peer} розірвано та вузол прибрано зі списку.");
            Console.ResetColor();
        }
    }
}