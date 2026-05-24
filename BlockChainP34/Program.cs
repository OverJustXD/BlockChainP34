using BlockChainP34.Models;
using BlockChainP34.Service;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var blockchain = new BlockChainService(1);

        var p2pClient = new P2PClient();
        var p2pServer = new P2PServer(blockchain, p2pClient);

        var display = new DisplayService();

        var tempTransactions = new List<Transaction>();

        var wallet = new Wallet(new CryptoService());

        Console.WriteLine("\n=== WALLET CREATED ===");
        Console.WriteLine($"PublicKey: {wallet.PublicKey.Substring(0, 20)}...");
        Console.WriteLine();

        Console.Write("Enter port: ");
        int port = int.Parse(Console.ReadLine());

        p2pServer.Start(port);

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

                        tempTransactions.Add(tx);

                        await p2pClient.BroadcastTransactionAsync(tx);

                        Console.WriteLine("Transaction created and broadcasted.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка: {ex.Message}");
                    }

                    break;

                case "2":
                    if (tempTransactions.Count == 0)
                    {
                        Console.WriteLine("Немає транзакцій для майнінгу.");
                        break;
                    }

                    try
                    {
                        foreach (var tx in tempTransactions)
                        {
                            var result = blockchain.AddTransactionToMempool(tx);

                            if (!result.success)
                            {
                                Console.WriteLine($"TX rejected: {result.error}");
                            }
                        }

                        var mineResult = blockchain.MinePendingTransactions(wallet.PublicKey);

                        if (!mineResult.success)
                        {
                            Console.WriteLine($"Mining failed: {mineResult.error}");
                        }
                        else
                        {
                            Console.WriteLine("Block successfully mined!");
                        }

                        tempTransactions.Clear();
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