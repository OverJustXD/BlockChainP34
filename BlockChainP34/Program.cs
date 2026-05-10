using System;
using System.Collections.Generic;
using BlockChainP34.Service;
using BlockChainP34.Models;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var blockchain = new BlockChainService(1);
        var display = new DisplayService();

        var tempTransactions = new List<Transaction>();

        var wallet = new Wallet(new CryptoService());

        Console.WriteLine("\n=== WALLET CREATED ===");
        Console.WriteLine($"PublicKey: {wallet.PublicKey.Substring(0, 20)}...");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("\n================ MENU ================");
            Console.WriteLine("[1] Додати транзакцію");
            Console.WriteLine("[2] Змайнити блок");
            Console.WriteLine("[3] Показати блокчейн");
            Console.WriteLine("[4] Перевірити валідність");
            Console.WriteLine("[5] Майнити пустий блок");
            Console.WriteLine("[0] Вихід");
            Console.Write("Ваш вибір: ");

            string choice = Console.ReadLine();

            switch (choice)
            {

                case "1":
                    try
                    {
                        Console.WriteLine($"Sender (auto wallet): {wallet.PublicKey.Substring(0, 15)}...");

                        string from = wallet.PublicKey; 

                        Console.Write("Отримувач (public key або текст): ");
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

                        Console.WriteLine("Транзакція створена і підписана.");
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

                case "0":
                    return;

                default:
                    Console.WriteLine("Невірний вибір");
                    break;
            }
        }
    }
}

//do
//{
//    Console.WriteLine("Enter missing Difficulty (positive integer): ");
//    var input = Console.ReadLine();
//    if (!int.TryParse(input, out Difficulty) || Difficulty <= 0)
//    {
//        Console.WriteLine("Invalid input. Please enter a positive integer.");
//    }
//} while (Difficulty <= 0);

//for (int i = 0; i < 10; i++)
//{

//    blockChainService.AddBlock("First Block", "Alex");
//  blockChainService.AddBlock("Second Block", "Deny");
//blockChainService.AddBlock("Third Block", "Timur");
// blockChainService.AddBlock("Fourth Block", "Lisa");
//displayService.DisplayBlockChain(blockChainService.Chain);
//Console.WriteLine("Difficulty: " + blockChainService.Difficulty);
//}

