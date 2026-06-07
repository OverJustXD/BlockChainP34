using BlockChainP34.Models;
using BlockChainP34.Service;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace BlockChainP34.Service.P2P;
using System.Threading.Tasks;

class Program
{
    private static string walletPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallet.json");

    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var blockchain = new BlockChainService(1);
        blockchain.LoadStateSnapshot();

        if (File.Exists("blockchain.json"))
        {
            try
            {
                var json = File.ReadAllText("blockchain.json");
                var chain = JsonSerializer.Deserialize<List<Block>>(json);

                if (chain != null && chain.Count > 0)
                {
                    blockchain.Chain = chain;
                    blockchain.RebuildState();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Система] Блокчейн успішно завантажено з файла blockchain.json");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Помилка завантаження блокчейну]: {ex.Message}");
            }
        }

        if (!blockchain.IsValid())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[CRITICAL] Підроблена або невалідна транзакція виявлена при старті системи!");
            Console.ResetColor();
        }

        Wallet wallet = null;
        var cryptoService = new CryptoService();

        if (File.Exists(walletPath))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== АВТОРИЗАЦІЯ ГАМАНЦЯ ===");
            Console.ResetColor();

            while (wallet == null)
            {
                Console.Write("Введіть пароль для розшифрування ключів: ");
                string password = ReadPassword();

                try
                {
                    var keys = WalletService.LoadWallet(password, walletPath);
                    wallet = new Wallet(keys.publicKey, keys.privateKey);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[Keystore] Гаманець успішно розшифровано. Вхід дозволено.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Помилка доступу]: {ex.Message}. Спробуйте ще раз.\n");
                    Console.ResetColor();
                }
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== РЕЄСТРАЦІЯ НОВОГО ГАМАНЦЯ ===");
            Console.ResetColor();
            Console.Write("Задайте надійний пароль для захисту AES-256: ");
            string password = ReadPassword();

            wallet = new Wallet(cryptoService);

            WalletService.SaveWallet(wallet.PublicKey, wallet.PrivateKey, password, walletPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Keystore] Новий зашифрований гаманець wallet.json збережено на диску.");
            Console.ResetColor();
        }

        Console.WriteLine($"\n=== АКТИВНИЙ ГАМАНЕЦЬ ===");
        Console.WriteLine($"PublicKey: {wallet.PublicKey.Substring(0, 20)}...");
        Console.WriteLine($"Баланс:    {blockchain.GetBalance(wallet.PublicKey)} COIN\n");

        var p2pClient = new P2PClient();
        var p2pServer = new P2PServer(blockchain, p2pClient);
        p2pClient.Init(blockchain); 
        var display = new DisplayService();

        Console.Write("Введіть локальний порт для сервера (напр. 5001): ");
        if (!int.TryParse(Console.ReadLine(), out int port))
        {
            port = 5001;
        }

        p2pServer.Start(port);

        while (true)
        {
            Console.WriteLine("\n================ MENU ================");
            Console.WriteLine($"[ Поточний порт ноди: {port} ]");
            Console.WriteLine("[1] Додати транзакцію (та розіслати в мережу)");
            Console.WriteLine("[2] Змайнити блок (із транзакцій мемпулу)");
            Console.WriteLine("[3] Показати блокчейн");
            Console.WriteLine("[4] Перевірити валідність ланцюга");
            Console.WriteLine("[5] Майнити пустий блок");
            Console.WriteLine("[6] Підключитися до віддаленого піра");
            Console.WriteLine("[7] Показати локальний мемпул");
            Console.WriteLine("[8] Запустити benchmark");
            Console.WriteLine("[H] Симуляція хакерської атаки (модифікація файлу)");
            Console.WriteLine("[P] Згенерувати Network Passport");
            Console.WriteLine("[-] Аудит блокчейну");
            Console.WriteLine("[+] Очистити кеш та скинути стан");
            Console.WriteLine("[`] Показати довжину ланцюга");
            Console.WriteLine("[0] Вихід");

            Console.Write("Ваш вибір: ");
            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    try
                    {
                        Console.WriteLine($"Відправник: {wallet.PublicKey.Substring(0, 15)}...");
                        string from = wallet.PublicKey;

                        Console.Write("Отримувач (Вставте PublicKey піра): ");
                        string to = Console.ReadLine();

                        Console.Write("Сума переказу: ");
                        string amountInput = Console.ReadLine();

                        if (!decimal.TryParse(amountInput, out decimal amount))
                        {
                            Console.WriteLine("Помилка: Невірна сума!");
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
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Транзакція відхилена: {result.error}");
                            Console.ResetColor();
                            break;
                        }

                        await p2pClient.BroadcastTransactionAsync(tx);
                        Console.WriteLine("Транзакція успішно додана в мемпул та надіслана в мережу.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка створення транзакції: {ex.Message}");
                    }
                    break;

                case "2":
                    if (blockchain.PendingTransactions.Count == 0)
                    {
                        Console.WriteLine("Мемпул порожній. Немає транзакцій для майнінгу.");
                        break;
                    }

                    try
                    {
                        var mineResult = blockchain.MinePendingTransactions(wallet.PublicKey);

                        if (!mineResult.success)
                        {
                            Console.WriteLine($"Майнінг не вдався: {mineResult.error}");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Блок успішно змайнено та додано в ланцюг!");
                            Console.ResetColor();

                            File.WriteAllText("blockchain.json", JsonSerializer.Serialize(blockchain.Chain));

                            await p2pClient.BroadcastChainAsync(blockchain.Chain);
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
                    if (valid)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Блокчейн повністю валідний.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Увага! Блокчейн ХАКНУТИЙ або пошкоджений!");
                    }
                    Console.ResetColor();
                    break;

                case "5":
                    try
                    {
                        var result = blockchain.MineEmptyBlock(wallet.PublicKey);

                        if (result.success)
                        {
                            Console.WriteLine("Порожній блок успішно змайнено.");
                            File.WriteAllText("blockchain.json", JsonSerializer.Serialize(blockchain.Chain));
                            await p2pClient.BroadcastChainAsync(blockchain.Chain);
                        }
                        else
                        {
                            Console.WriteLine($"Помилка: {result.error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка: {ex.Message}");
                    }
                    break;

                case "6":
                    Console.Write("Введіть адресу піра для підключення (напр. 127.0.0.1:5002): ");
                    var peer = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(peer)) break;

                    p2pClient.Connect(peer);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Мережа] Запит на синхронізацію надіслано до {peer}. Очікуйте Gossip-відповіді.");
                    Console.ResetColor();
                    break;

                case "7":
                    Console.WriteLine("\n=== ПОТОЧНИЙ МЕМПУЛ (PENDING TRANSACTIONS) ===");
                    if (blockchain.PendingTransactions.Count == 0)
                    {
                        Console.WriteLine("Порожньо.");
                    }
                    foreach (var txItem in blockchain.PendingTransactions)
                    {
                        Console.WriteLine($"TX ID:  {txItem.Id}");
                        Console.WriteLine($"From:   {txItem.From.Substring(0, Math.Min(15, txItem.From.Length))}...");
                        Console.WriteLine($"To:     {txItem.To.Substring(0, Math.Min(15, txItem.To.Length))}...");
                        Console.WriteLine($"Amount: {txItem.Amount} COIN");
                        Console.WriteLine("--------------------------------------------");
                    }
                    break;

                case "8":
                    RunBenchmark(blockchain);
                    break;

                case "H":
                case "h":
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n=== СИМУЛЯЦІЯ ХАКЕРСЬКОЇ АТАКИ ===");
                        Console.ResetColor();

                        var targetBlock = blockchain.Chain
                            .FirstOrDefault(b => b.Transactions.Any(t => t.From != "SYSTEM"));

                        if (targetBlock == null)
                        {
                            Console.WriteLine("Помилка: У ланцюгу немає блоків з реальними користувацькими транзакціями.");
                            break;
                        }

                        var attackTx = targetBlock.Transactions
                            .FirstOrDefault(t => t.From != "SYSTEM");

                        if (attackTx == null)
                        {
                            Console.WriteLine("Немає підходящої транзакції для зламу.");
                            break;
                        }

                        Console.WriteLine($"Злам Блоку №: {targetBlock.Index}");
                        Console.WriteLine($"ID транзакції: {attackTx.Id}");
                        Console.WriteLine($"Оригінальна сума: {attackTx.Amount}");

                        attackTx.Amount = 1_000_000m;

                        Console.WriteLine($"Модифіковано на: {attackTx.Amount}");

                        File.WriteAllText(
                            "blockchain.json",
                            JsonSerializer.Serialize(blockchain.Chain,
                                new JsonSerializerOptions { WriteIndented = true })
                        );

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[Атака] Модифікований (зламаний) блокчейн перезаписано на диск!");
                        Console.ResetColor();
                        break;
                    }

                case "P":
                case "p":
                    {
                        Console.Write("Введіть Student ID: ");
                        string id = Console.ReadLine();

                        var report = blockchain.RunFullAudit(blockchain.Chain);
                        var origin = blockchain.FindAttackOrigin(report, blockchain.Chain);

                        var passport = StorageService.GenerateNetworkPassport(
                            blockchain,
                            id,
                            origin?.Index ?? -1,
                            origin?.Hash ?? "NOT_FOUND"
                        );

                        StorageService.SavePassport(passport);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Файл passport.json успішно згенеровано.");
                        Console.ResetColor();
                        break;
                    }

                case "-":
                    {
                        var report = blockchain.RunFullAudit(blockchain.Chain);
                        var origin = blockchain.FindAttackOrigin(report, blockchain.Chain);
                        var forensic = blockchain.GenerateForensicReport(report, origin);

                        Console.WriteLine();
                        Console.WriteLine(forensic);
                        break;
                    }

                case "+":
                    {
                        File.Delete("blockchain.json");
                        File.Delete("state.json");

                        Console.WriteLine("Локальні кеш-файли видалено. Стан скинуто.");

                        blockchain.Chain.Clear();
                        blockchain = new BlockChainService(blockchain.Difficulty);
                        break;
                    }

                case "`":
                    Console.WriteLine($"Довжина ланцюга = {blockchain.Chain.Count} блоків.");
                    break;

                case "0":
                    p2pServer.Stop();
                    return;

                default:
                    Console.WriteLine("Невірний вибір. Оберіть пункт із меню.");
                    break;
            }
        }
    }

    private static void RunBenchmark(BlockChainService blockchain)
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
        Console.WriteLine($"Швидкість пошуку в стані: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Перевірочний Баланс User9999 = {fastBalance}");
        Console.ResetColor();

        Console.WriteLine("=== BENCHMARK END ===\n");
    }

    private static string ReadPassword()
    {
        string pass = "";
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                    Console.Write("\b \b");
                }
            }
            else
            {
                pass += key.KeyChar;
                Console.Write("*");
            }
        }
        Console.WriteLine();
        return pass;
    }
}