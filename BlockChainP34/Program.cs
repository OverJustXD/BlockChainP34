using BlockChainP34.Models;
using BlockChainP34.Service;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.Diagnostics;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var blockchain = new BlockChainService(1);
        blockchain.LoadStateSnapshot();
        var p2pClient = new P2PClient();
        var p2pServer = new P2PServer(blockchain, p2pClient);

        var display = new DisplayService();


        var wallet = new Wallet(new CryptoService());

        Console.WriteLine("\n=== WALLET CREATED ===");
        Console.WriteLine($"PublicKey: {wallet.PublicKey.Substring(0, 20)}...");
        Console.WriteLine();

        Console.Write("Enter port: ");
        int port = int.Parse(Console.ReadLine());

        p2pServer.Start(port);

        static void RunBenchmark(BlockChainService blockchain)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n=== BENCHMARK START ===");
            Console.ResetColor();

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                var tx = new Transaction("SYSTEM", $"User{i}", 1, 0);

                var block = new Block(
                    blockchain.Chain.Count,
                    DateTime.UtcNow,
                    new List<Transaction> { tx },
                    blockchain.Chain.Last().Hash,
                    "benchmark"
                );

                block.Hash = Guid.NewGuid().ToString();

                blockchain.Chain.Add(block);
            }

            blockchain.RebuildState();

            stopwatch.Stop();

            decimal fastBalance = blockchain.GetBalance("User9999");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Fast state lookup: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Balance = {fastBalance}");
            Console.ResetColor();

            Console.WriteLine("=== BENCHMARK END ===\n");
        }

        while (true)
        {
            Console.WriteLine("\n================ MENU ================");
            Console.WriteLine("[1] Додати транзакцію");
            Console.WriteLine("[2] Змайнити блок");
            Console.WriteLine("[3] Показати блокчейн");
            Console.WriteLine("[4] Перевірити валідність");
            Console.WriteLine("[5] Майнити пустий блок");
            Console.WriteLine("[6] Підключитися до вузла");
            Console.WriteLine("[7] Показати мемпул");
            Console.WriteLine("[8] Запустити benchmark");
            Console.WriteLine("[0] Вихід");

            Console.Write("Ваш вибір: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    try
                    {
                        Console.WriteLine($"Sender: {wallet.PublicKey.Substring(0, 15)}...");

                        string from = wallet.PublicKey;

                        Console.Write("Отримувач: ");
                        string to = Console.ReadLine();

                        Console.Write("Сума: ");
                        string amountInput = Console.ReadLine();

                        if (!decimal.TryParse(amountInput, out decimal amount))
                        {
                            Console.WriteLine("Невірна сума!");
                            break;
                        }

                        var tx = TransactionService.CreateTransaction(
                            from,
                            to,
                            amount,
                            wallet.PrivateKey,
                            2m
                        );

                        var result = blockchain.AddTransactionToMempool(tx);

                        if (!result.success)
                        {
                            Console.WriteLine($"TX rejected: {result.error}");
                            break;
                        }

                        await p2pClient.BroadcastTransactionAsync(tx);

                        Console.WriteLine("Transaction added to mempool.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка: {ex.Message}");
                    }

                    break;

                case "2":
                    if (blockchain.PendingTransactions.Count == 0)
                    {
                        Console.WriteLine("Немає транзакцій для майнінгу.");
                        break;
                    }

                    try
                    {
                        var mineResult = blockchain.MinePendingTransactions(wallet.PublicKey);

                        if (!mineResult.success)
                        {
                            Console.WriteLine($"Mining failed: {mineResult.error}");
                        }
                        else
                        {
                            Console.WriteLine("Block successfully mined!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка майнінгу: {ex.Message}");
                    }

                    break;

                case "3":
                    display.Verbose = true;
                    display.DisplayBlockChain(blockchain.Chain);
                    break;

                case "4":
                    bool valid = blockchain.IsValid();

                    Console.WriteLine(valid
                        ? "Blockchain валідний"
                        : "Blockchain пошкоджений");

                    break;

                case "5":
                    {
                        var result = blockchain.MineEmptyBlock(wallet.PublicKey);

                        if (result.success)
                            Console.WriteLine("Empty block mined!");
                        else
                            Console.WriteLine($"Error: {result.error}");
                    }

                    break;

                case "6":
                    {
                        Console.Write("Peer address (127.0.0.1:6002): ");

                        var peer = Console.ReadLine();

                        p2pClient.Connect(peer);

                        break;
                    }

                case "7":
                    {
                        Console.WriteLine("\n=== MEMPOOL ===");

                        foreach (var tx in blockchain.PendingTransactions)
                        {
                            Console.WriteLine($"TX ID: {tx.Id}");
                            Console.WriteLine($"From: {tx.From}");
                            Console.WriteLine($"To: {tx.To}");
                            Console.WriteLine($"Amount: {tx.Amount}");
                            Console.WriteLine("----------------------");
                        }

                        break;
                    }

                case "8":
                    RunBenchmark(blockchain);
                    break;

                case "9":
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("\n=== SIMULATING FOREIGN CHAIN ===");
                        Console.ResetColor();

                        var fakeNode = new BlockChainService(blockchain.Difficulty);

                        Console.WriteLine("Фейкова нода майнить альтернативний всесвіт...");

                        fakeNode.MineEmptyBlock("HackerWallet");
                        fakeNode.MineEmptyBlock("HackerWallet");

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"Fake chain length: {fakeNode.Chain.Count}");
                        Console.WriteLine($"Our chain length : {blockchain.Chain.Count}");
                        Console.ResetColor();

                        bool replaced = blockchain.ReplaceChain(fakeNode.Chain);

                        if (!replaced)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Не вдалося замінити ланцюг.");
                            Console.ResetColor();
                        }

                        break;
                    }

                case "0":
                    return;

                default:
                    Console.WriteLine("Невірний вибір");
                    break;
            }
        }
    }
}