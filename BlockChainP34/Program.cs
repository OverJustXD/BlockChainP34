using BlockChainP34.Models;
using BlockChainP34.Service;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockChainP34.Service.P2P
{
    class Program
    {
        private static readonly string WalletPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallet.json");

        private static BlockChainService _blockchain;
        private static Wallet _wallet;
        private static P2PClient _p2pClient;
        private static P2PServer _p2pServer;
        private static DisplayService _display;
        private static int _port = 5001;
        private static bool _isRunning = true;

        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            InitializeCoreServices();
            LoadBlockchainState();
            AuthenticateOrRegisterWallet();
            InitializeNetworkNode();

            await RunApplicationLoop();
        }

        #region Инициализация системы

        private static void InitializeCoreServices()
        {
            _blockchain = new BlockChainService(1);
            _p2pClient = new P2PClient();
            _p2pServer = new P2PServer(_blockchain, _p2pClient);
            _display = new DisplayService();
        }

        private static void LoadBlockchainState()
        {
            _blockchain.LoadStateSnapshot();

            if (!File.Exists("blockchain.json")) return;

            try
            {
                var json = File.ReadAllText("blockchain.json");
                var chain = JsonSerializer.Deserialize<List<Block>>(json);

                if (chain != null && chain.Count > 0)
                {
                    _blockchain.Chain = chain;
                    _blockchain.RebuildState();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Система] Блокчейн успішно завантажено з файла blockchain.json");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Помилка завантаження блокчейну]: {ex.Message}");
            }

            if (!_blockchain.IsValid())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[CRITICAL] Підроблена або невалідна транзакція виявлена при старті системи!");
                Console.ResetColor();
            }
        }

        private static void AuthenticateOrRegisterWallet()
        {
            var cryptoService = new CryptoService();

            if (File.Exists(WalletPath))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== АВТОРИЗАЦІЯ ГАМАНЦЯ ===");
                Console.ResetColor();

                while (_wallet == null)
                {
                    Console.Write("Введіть пароль для розшифрування ключів: ");
                    string password = ReadPassword();

                    try
                    {
                        var keys = WalletService.LoadWallet(password, WalletPath);
                        _wallet = new Wallet(keys.publicKey, keys.privateKey);

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

                _wallet = new Wallet(cryptoService);
                WalletService.SaveWallet(_wallet.PublicKey, _wallet.PrivateKey, password, WalletPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[Keystore] Новий зашифрований гаманець wallet.json збережено на диску.");
                Console.ResetColor();
            }

            Console.WriteLine($"\n=== АКТИВНИЙ ГАМАНЕЦЬ ===");
            Console.WriteLine($"PublicKey: {_wallet.PublicKey.Substring(0, 20)}...");
            Console.WriteLine($"Баланс:    {_blockchain.GetBalance(_wallet.PublicKey)} COIN\n");
        }

        private static void InitializeNetworkNode()
        {
            _p2pClient.Init(_blockchain);

            Console.Write("Введіть локальний порт для сервера (напр. 5001): ");
            if (!int.TryParse(Console.ReadLine(), out _port))
            {
                _port = 5001;
            }

            _p2pServer.Start(_port);
        }

        #endregion

        #region Основной цикл и Меню

        private static async Task RunApplicationLoop()
        {
            while (_isRunning)
            {
                ShowMenuHeader();
                Console.Write("Ваш вибір: ");
                string choice = Console.ReadLine()?.Trim().ToUpper();

                await ProcessMenuChoice(choice);
            }
        }

        private static void ShowMenuHeader()
        {
            Console.WriteLine("\n================ MENU ================");
            Console.WriteLine($"[ Поточний порт ноди: {_port} ]");
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
            Console.WriteLine("[T] Виконати системну діагностику");
            Console.WriteLine("[-] Аудит блокчейну");
            Console.WriteLine("[+] Очистити кеш та скинути стан");
            Console.WriteLine("[`] Показати довжину ланцюга");
            Console.WriteLine("[0] Вихід");
        }

        private static async Task ProcessMenuChoice(string choice)
        {
            switch (choice)
            {
                case "1": await HandleAddTransaction(); break;
                case "2": await HandleMinePendingTransactions(); break;
                case "3": HandleDisplayBlockchain(); break;
                case "4": HandleValidateChain(); break;
                case "5": await HandleMineEmptyBlock(); break;
                case "6": HandleConnectToPeer(); break;
                case "7": HandleShowMempool(); break;
                case "8": HandleRunBenchmark(); break;
                case "H": HandleSimulateHackerAttack(); break;
                case "P": HandleGenerateNetworkPassport(); break;
                case "T": await HandleExecuteSystemDiagnostics(); break;
                case "-": HandleRunForensicAudit(); break;
                case "+": HandleResetState(); break;
                case "`": HandleShowChainLength(); break;
                case "0": HandleShutdown(); break;
                default:
                    Console.WriteLine("Невірний вибір. Оберіть пункт із меню.");
                    break;
            }
        }

        #endregion

        #region Обработчики команд меню (Handlers)

        private static async Task HandleAddTransaction()
        {
            try
            {
                Console.WriteLine($"Відправник: {_wallet.PublicKey.Substring(0, 15)}...");
                string from = _wallet.PublicKey;

                Console.Write("Отримувач (Вставте PublicKey піра): ");
                string to = Console.ReadLine();

                Console.Write("Сума переказу: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal amount))
                {
                    Console.WriteLine("Помилка: Невірна сума!");
                    return;
                }

                var tx = TransactionService.CreateTransaction(from, to, amount, _wallet.PrivateKey, 2m);
                var result = _blockchain.AddTransactionToMempool(tx);

                if (!result.success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Транзакція відхилена: {result.error}");
                    Console.ResetColor();
                    return;
                }

                await _p2pClient.BroadcastTransactionAsync(tx);
                Console.WriteLine("Транзакція успішно додана в мемпул та надіслана в мережу.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка створення транзакції: {ex.Message}");
            }
        }

        private static async Task HandleMinePendingTransactions()
        {
            if (_blockchain.PendingTransactions.Count == 0)
            {
                Console.WriteLine("Мемпул порожній. Немає транзакцій для майнінгу.");
                return;
            }

            try
            {
                var mineResult = _blockchain.MinePendingTransactions(_wallet.PublicKey);

                if (!mineResult.success)
                {
                    Console.WriteLine($"Майнінг не вдався: {mineResult.error}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Блок успішно змайнено та додано в ланцюг!");
                    Console.ResetColor();

                    File.WriteAllText("blockchain.json", JsonSerializer.Serialize(_blockchain.Chain));
                    await _p2pClient.BroadcastChainAsync(_blockchain.Chain);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка майнінгу: {ex.Message}");
            }
        }

        private static void HandleDisplayBlockchain()
        {
            _display.Verbose = true;
            _display.DisplayBlockChain(_blockchain.Chain);
        }

        private static void HandleValidateChain()
        {
            bool valid = _blockchain.IsValid();
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
        }

        private static async Task HandleMineEmptyBlock()
        {
            try
            {
                var result = _blockchain.MineEmptyBlock(_wallet.PublicKey);

                if (result.success)
                {
                    Console.WriteLine("Порожній блок успішно змайнено.");
                    File.WriteAllText("blockchain.json", JsonSerializer.Serialize(_blockchain.Chain));
                    await _p2pClient.BroadcastChainAsync(_blockchain.Chain);
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
        }

        private static void HandleConnectToPeer()
        {
            Console.Write("Введіть адресу піра для підключення (напр. 127.0.0.1:5002): ");
            var peer = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(peer)) return;

            _p2pClient.Connect(peer);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Мережа] Запит на синхронізацію надіслано до {peer}. Очікуйте Gossip-відповіді.");
            Console.ResetColor();
        }

        private static void HandleShowMempool()
        {
            Console.WriteLine("\n=== ПОТОЧНИЙ МЕМПУЛ (PENDING TRANSACTIONS) ===");
            if (_blockchain.PendingTransactions.Count == 0)
            {
                Console.WriteLine("Порожньо.");
                return;
            }
            foreach (var txItem in _blockchain.PendingTransactions)
            {
                Console.WriteLine($"TX ID:  {txItem.Id}");
                Console.WriteLine($"From:   {txItem.From.Substring(0, Math.Min(15, txItem.From.Length))}...");
                Console.WriteLine($"To:     {txItem.To.Substring(0, Math.Min(15, txItem.To.Length))}...");
                Console.WriteLine($"Amount: {txItem.Amount} COIN");
                Console.WriteLine("--------------------------------------------");
            }
        }

        private static void HandleRunBenchmark()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n=== BENCHMARK START ===");
            Console.ResetColor();

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                var tx = new Transaction("SYSTEM", $"User{i}", 1, 0);
                var block = new Block(
                    _blockchain.Chain.Count,
                    DateTime.UtcNow,
                    new List<Transaction> { tx },
                    _blockchain.Chain.Last().Hash,
                    "benchmark"
                );

                block.Hash = Guid.NewGuid().ToString();
                _blockchain.Chain.Add(block);
            }

            _blockchain.RebuildState();
            stopwatch.Stop();

            decimal fastBalance = _blockchain.GetBalance("User9999");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Швидкість пошуку в стані: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Перевірочний Баланс User9999 = {fastBalance}");
            Console.ResetColor();
            Console.WriteLine("=== BENCHMARK END ===\n");
        }

        private static void HandleSimulateHackerAttack()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n=== СИМУЛЯЦІЯ ХАКЕРСЬКОЇ АТАКИ ===");
            Console.ResetColor();

            var targetBlock = _blockchain.Chain.FirstOrDefault(b => b.Transactions.Any(t => t.From != "SYSTEM"));
            if (targetBlock == null)
            {
                Console.WriteLine("Помилка: У ланцюгу немає блоків з реальними користувацькими транзакціями.");
                return;
            }

            var attackTx = targetBlock.Transactions.FirstOrDefault(t => t.From != "SYSTEM");
            if (attackTx == null)
            {
                Console.WriteLine("Немає підходящої транзакції для зламу.");
                return;
            }

            Console.WriteLine($"Злам Блоку №: {targetBlock.Index}");
            Console.WriteLine($"ID транзакції: {attackTx.Id}");
            Console.WriteLine($"Оригінальна сума: {attackTx.Amount}");

            attackTx.Amount = 1_000_000m;
            Console.WriteLine($"Модифіковано на: {attackTx.Amount}");

            File.WriteAllText(
                "blockchain.json",
                JsonSerializer.Serialize(_blockchain.Chain, new JsonSerializerOptions { WriteIndented = true })
            );

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Атака] Модифікований (зламаний) blockchian перезаписано на disk!");
            Console.ResetColor();
        }

        private static void HandleGenerateNetworkPassport()
        {
            Console.Write("Введіть Student ID / Node Identifier: ");
            string id = Console.ReadLine();

            var report = _blockchain.RunFullAudit(_blockchain.Chain);
            var origin = _blockchain.FindAttackOrigin(report, _blockchain.Chain);

            var passport = StorageService.GenerateNetworkPassport(
                _blockchain,
                id,
                origin?.Index ?? -1,
                origin?.Hash ?? "NOT_FOUND"
            );

            StorageService.SavePassport(passport);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Файл passport.json успішно згенеровано.");
            Console.ResetColor();
        }

        private static async Task HandleExecuteSystemDiagnostics()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n=== ЗАПУСК КОМПЛЕКСНОЇ ДІАГНОСТИКИ ТА ВЕРИФІКАЦІЇ СИСТЕМИ ===");
            Console.ResetColor();

            _blockchain.EvictStaleTransactions(0);

            Console.WriteLine("\n1. Тестування VIP-черги (Сортування за сумою Amount)...");
            _blockchain.CreateAndAddTransaction(_wallet.PublicKey, "Recipient_A", 5.0m, 1.0m, _wallet.PrivateKey);
            _blockchain.CreateAndAddTransaction(_wallet.PublicKey, "Recipient_B", 50.0m, 1.0m, _wallet.PrivateKey);

            Console.WriteLine("Поточний Mempool перед майнінгом:");
            foreach (var tx in _blockchain.PendingTransactions) Console.WriteLine($" - {tx}");

            Console.WriteLine("\nМайнимо блок...");
            var result1 = _blockchain.MinePendingTransactions(_wallet.PublicKey);

            Console.WriteLine("\nТранзакції, які потрапили у змайнений block (першою має бути сума 50):");

            if (result1.block == null || result1.block.Transactions == null)
            {
                Console.WriteLine("Помилка: Блок не був створений (можливо, мемпул порожній або транзакції відхилено).");
            }
            else
            {
                foreach (var tx in result1.block.Transactions)
                {
                    Console.WriteLine($" -> {tx}");
                }
            }

            Console.WriteLine("\n2. Тестування очищення застарілих транзакцій (TTL)...");
            _blockchain.CreateAndAddTransaction(_wallet.PublicKey, "Recipient_C", 10.0m, 1.0m, _wallet.PrivateKey);
            _blockchain.CreateAndAddTransaction(_wallet.PublicKey, "Recipient_Expired", 10.0m, 1.0m, _wallet.PrivateKey);

            var expiredTx = _blockchain.PendingTransactions.Last();
            expiredTx.TimeStamp = DateTime.UtcNow.AddMinutes(-10);

            Console.WriteLine("Mempool перед майнінгом (одна транзакція протермінована на 10 хв):");
            foreach (var tx in _blockchain.PendingTransactions) Console.WriteLine($" - {tx}");

            Console.WriteLine("\nМайнимо блок (очікуємо видалення expired транзакції)...");
            var result2 = _blockchain.MinePendingTransactions(_wallet.PublicKey);

            Console.WriteLine("\nТранзакції у новому блоці (сума 10 COIN мала зникнути і не потрапити сюди):");
            foreach (var tx in result2.block.Transactions) Console.WriteLine($" -> {tx}");

            Console.WriteLine("\n3. Тестування відкладених транзакцій (LockTime)...");
            int currentChainHeight = _blockchain.Chain.Count;
            int futureHeight = currentChainHeight + 2;
            Console.WriteLine($"Поточна висота ланцюга: {currentChainHeight}. Загальмуємо транзакцію до висоти: {futureHeight}");

            _blockchain.CreateAndAddTransaction(_wallet.PublicKey, "Recipient_Locked", 5.0m, 1.0m, _wallet.PrivateKey, lockTime: futureHeight);

            Console.WriteLine("Mempool після додавання LockTime транзакції:");
            foreach (var tx in _blockchain.PendingTransactions) Console.WriteLine($" - {tx}");

            Console.WriteLine("\n[Раунд 1] Пробуємо майнити (має видати помилку або ігнорувати Locked транзакцію)...");
            var result3_1 = _blockchain.MinePendingTransactions(_wallet.PublicKey);
            Console.WriteLine($"Результат: success = {result3_1.success}, error = {result3_1.error}");

            Console.WriteLine("\nГенеруємо пусті блоки, щоб наростити довжину ланцюга...");
            _blockchain.MineEmptyBlock(_wallet.PublicKey);
            _blockchain.MineEmptyBlock(_wallet.PublicKey);
            Console.WriteLine($"Нова висота ланцюга: {_blockchain.Chain.Count}");

            Console.WriteLine("\n[Раунд 2] Пробуємо майнити знову (тепер висота дозволяє і транзакція підтвердиться):");
            var result3_2 = _blockchain.MinePendingTransactions(_wallet.PublicKey);
            Console.WriteLine($"Результат: success = {result3_2.success}");
            if (result3_2.success)
            {
                foreach (var tx in result3_2.block.Transactions) Console.WriteLine($" -> {tx}");
            }
        }

        private static void HandleRunForensicAudit()
        {
            var report = _blockchain.RunFullAudit(_blockchain.Chain);
            var origin = _blockchain.FindAttackOrigin(report, _blockchain.Chain);
            var forensic = _blockchain.GenerateForensicReport(report, origin);

            Console.WriteLine();
            Console.WriteLine(forensic);
        }

        private static void HandleResetState()
        {
            File.Delete("blockchain.json");
            File.Delete("state.json");

            Console.WriteLine("Локальні кеш-файли видалено. Стан скинуто.");

            _blockchain.Chain.Clear();
            _blockchain = new BlockChainService(_blockchain.Difficulty);
        }

        private static void HandleShowChainLength()
        {
            Console.WriteLine($"Довжина ланцюга = {_blockchain.Chain.Count} блоків.");
        }

        private static void HandleShutdown()
        {
            _p2pServer.Stop();
            _isRunning = false;
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

        #endregion
    }
}