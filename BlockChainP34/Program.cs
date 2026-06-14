using BlockChainP34.Models;
using BlockChainP34.Service;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static ColdWalletService _coldWallet;
        private static int _port = 5001;
        private static bool _isRunning = true;
        private static bool _isSpvClient = false;

        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            InitializeCoreServices();
            InitializeNetworkNode();

            if (!_isSpvClient)
            {
                LoadBlockchainState();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("[Система] Запущено в режимі Легкого гаманця (SPV).");
                Console.ResetColor();

                TrustedHeaderStore.Load();
                _p2pClient.Connect("127.0.0.1:5001");

                Console.WriteLine("[Система] Чекаємо синхронізації заголовків...");
                await Task.Delay(2000);
            }

            AuthenticateOrRegisterWallet();
            await RunApplicationLoop();
        }

        #region Initialization

        private static void InitializeCoreServices()
        {
            _p2pClient = new P2PClient();
            _display = new DisplayService();
            _coldWallet = new ColdWalletService();
        }

        private static void InitializeNetworkNode()
        {
            Console.Write("Введіть локальний порт для сервера (напр. 5001): ");

            if (!int.TryParse(Console.ReadLine(), out _port))
            {
                _port = 5001;
            }

            Console.WriteLine("\nОберіть режим роботи програми:");
            Console.WriteLine("1. Повна нода (Full Node)");
            Console.WriteLine("2. Легкий гаманець (SPV Client)");
            Console.Write("Ваш вибір: ");

            string modeChoice = Console.ReadLine()?.Trim();
            _isSpvClient = (modeChoice == "2");

            if (!_isSpvClient)
            {
                _p2pServer = new P2PServer(_blockchain, _p2pClient);
                _p2pServer.Start(_port);
                _p2pClient.Init(_blockchain, spvClient: false);
            }
            else
            {
                _p2pClient.Init(null, spvClient: true);
                Console.WriteLine($"[Мережа] SPV Клієнт запущений на порті {_port} (Очікує підключення до Full Node)");
            }

            _p2pClient.LoadPeers();

            foreach (var peer in _p2pClient.Peers.ToList())
            {
                string[] parts = peer.Split(':');

                if (parts.Length == 2 && parts[1] == _port.ToString())
                {
                    Console.WriteLine("[Система] Пропущено підключення до самого себе.");
                    continue;
                }

                _ = Task.Run(() => _p2pClient.Connect(peer));
            }
        }

        private static void LoadBlockchainState()
        {
            _blockchain = new BlockChainService(1);
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

        #endregion

        #region Wallet Management

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

            string balanceText = _isSpvClient ? "Запит через підключену ноду (SPV)" : $"{_blockchain.GetBalance(_wallet.PublicKey)} COIN";
            Console.WriteLine($"Баланс:    {balanceText}\n");
        }

        private static string ReadPassword()
        {
            string pass = "";

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                    break;

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

        #region Main Loop

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
            Console.WriteLine($"[ Поточний порт ноди: {_port} | РЕЖИМ: {(_isSpvClient ? "SPV Клієнт" : "Повна Нода")} ]");

            if (_isSpvClient)
            {
                Console.WriteLine("[1] Створити та надіслати транзакцію в мережу");
                Console.WriteLine("[6] Підключитися до віддаленої Повної Ноди");
                Console.WriteLine("[V] Запросити SPV-доказ з мережі (Куленепробивна верифікація)");
                Console.WriteLine("[K] Локальна SPV-перевірка транзакції (для тестування)");
                Console.WriteLine("[0] Вихід");
            }
            else
            {
                Console.WriteLine("[1] Додати транзакцію (та розіслати в мережу)");
                Console.WriteLine("[2] Змайнити блок (із транзакцій мемпулу)");
                Console.WriteLine("[3] Показати блокчейн");
                Console.WriteLine("[4] Перевірити валідність ланцюга");
                Console.WriteLine("[5] Майнити пустий блок");
                Console.WriteLine("[6] Підключитися до віддаленого піра");
                Console.WriteLine("[7] Показати локальний мемпул");
                Console.WriteLine("[8] Запустити benchmark");
                Console.WriteLine("[9] Знайти транзакцію за ID");
                Console.WriteLine("[D] Створити офлайн-транзакцію");
                Console.WriteLine("[B] Завантажити транзакцію з файлу");
                Console.WriteLine("[H] Симуляція хакерської атаки (модифікація файлу)");
                Console.WriteLine("[P] Згенерувати Network Passport");
                Console.WriteLine("[T] Виконати системну діагностику");
                Console.WriteLine("[M] Випустити власний токен (Mint)");
                Console.WriteLine("[L] Показати всі баланси (по токенах)");
                Console.WriteLine("[I] Історія гаманця (Explorer)");
                Console.WriteLine("[-] Аудит блокчейну");
                Console.WriteLine("[+] Очистити кеш та скинути стан");
                Console.WriteLine("[`] Показати довжину ланцюга");
                Console.WriteLine("[0] Вихід");
            }
        }

        private static async Task ProcessMenuChoice(string choice)
        {
            if (_isSpvClient && choice != "1" && choice != "6" && choice != "V" && choice != "K" && choice != "0")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Дія заборонена! Опція недоступна в режимі SPV-клієнта.");
                Console.ResetColor();
                return;
            }

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
                case "9": HandleFindTransaction(); break;
                case "D": HandleOfflineTransactionCreation(); break;
                case "B": await HandleBroadcastTransactionFromFile(); break;
                case "H": HandleSimulateHackerAttack(); break;
                case "P": HandleGenerateNetworkPassport(); break;
                case "T": await HandleExecuteSystemDiagnostics(); break;
                case "-": HandleRunForensicAudit(); break;
                case "+": HandleResetState(); break;
                case "`": HandleShowChainLength(); break;
                case "V": await HandleRequestAndVerifySPV(); break;
                case "K": HandleSpvVerification(); break;
                case "M": await HandleMintToken(); break;
                case "L": HandleShowAllBalances(); break;
                case "I": HandleWalletHistory(); break;
                case "0": HandleShutdown(); break;
                default:
                    Console.WriteLine("Невірний вибір. Оберіть пункт із меню.");
                    break;
            }
        }

        #endregion

        #region Handlers - Transactions

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

                if (_isSpvClient)
                {
                    await _p2pClient.BroadcastTransactionAsync(tx);
                    Console.WriteLine("[SPV] Транзакцію підписано та надіслано на обробку Повній Ноді.");
                }
                else
                {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка створення транзакції: {ex.Message}");
            }
        }

        private static void HandleOfflineTransactionCreation()
        {
            try
            {
                Console.Write("Адреса отримувача: ");
                string to = Console.ReadLine();

                Console.Write("Сума: ");
                decimal amount = decimal.Parse(Console.ReadLine());

                Console.Write("Комісія: ");
                decimal fee = decimal.Parse(Console.ReadLine());

                _coldWallet.GenerateOfflineTransaction(_wallet.PublicKey, to, amount, fee, _wallet.PrivateKey, 0);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Офлайн-транзакцію збережено у файл: offline_tx_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Помилка: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static async Task HandleBroadcastTransactionFromFile()
        {
            var file = Directory.GetFiles(Directory.GetCurrentDirectory(), "offline_tx_*.json").OrderByDescending(f => f).FirstOrDefault();

            if (file == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Офлайн-транзакцій не знайдено.");
                Console.ResetColor();
                return;
            }

            var result = await _blockchain.BroadcastTransactionFromFile(file, _p2pClient);

            if (result.success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Транзакцію успішно відправлено.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Помилка: {result.error}");
            }

            Console.ResetColor();
        }

        private static async Task HandleMintToken()
        {
            try
            {
                Console.Write("Назва токена (наприклад ACADEMY_COIN): ");
                string token = Console.ReadLine()?.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("Назва токена не може бути порожньою!");
                    return;
                }

                Console.Write("Сума емісії: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
                {
                    Console.WriteLine("Невірна сума!");
                    return;
                }

                var tx = new Transaction("MINT", _wallet.PublicKey, amount, 0) { TokenSymbol = token };

                if (_blockchain.IsTokenKnown(token))
                {
                    Console.WriteLine($"Токен {token} вже існує!");
                    return;
                }

                var result = _blockchain.AddTransactionToMempool(tx);

                if (!result.success)
                {
                    Console.WriteLine($"Помилка: {result.error}");
                    return;
                }

                await _p2pClient.BroadcastTransactionAsync(tx);

                if (!_isSpvClient)
                {
                    _blockchain.RebuildState();

                    var balance = _blockchain.GetBalance(_wallet.PublicKey, token);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[OK] Токен {token} створено!");
                    Console.WriteLine($"[OK] Ваш баланс {token}: {balance}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SPV] Mint транзакція відправлена в мережу.");
                    Console.WriteLine($"[SPV] Токен {token} буде доступний після підтвердження блоком.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mint error: {ex.Message}");
            }
        }

        #endregion

        #region Handlers - Mining

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

        #endregion

        #region Handlers - Display & Validation

        private static void HandleDisplayBlockchain()
        {
            _display.DisplayBlockChain(_blockchain.Chain);
        }

        private static void HandleValidateChain()
        {
            if (_blockchain.IsValid())
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
                Console.WriteLine(new string('-', 60));
                Console.WriteLine($"ID:     {txItem.Id}");
                Console.WriteLine($"Amount: {txItem.Amount} COIN");
                Console.WriteLine($"From:   {txItem.From}");
            }
        }

        private static void HandleFindTransaction()
        {
            Console.Write("Введіть ID транзакції: ");
            string txId = Console.ReadLine();

            foreach (var block in _blockchain.Chain)
            {
                var tx = block.Transactions.FirstOrDefault(t => t.Id == txId);

                if (tx != null)
                {
                    _display.DisplayTransactionFull(tx, block);
                    return;
                }
            }

            Console.WriteLine("Транзакцію не знайдено.");
        }

        private static void HandleShowChainLength()
        {
            Console.WriteLine($"Довжина ланцюга = {_blockchain.Chain.Count} блоків.");
        }

        private static void HandleShowAllBalances()
        {
            Console.WriteLine("\n=== ВСІ БАЛАНСИ ГАМАНЦЯ ===");

            var address = _wallet.PublicKey;
            var allTokens = _blockchain.GetKnownTokens();

            Console.WriteLine($"Гаманець: {address.Substring(0, 20)}...");
            Console.WriteLine(new string('-', 50));

            bool hasAnyBalance = false;

            foreach (var token in allTokens)
            {
                decimal balance = _blockchain.GetBalance(address, token);

                if (balance > 0 || token == "MAIN")
                {
                    Console.ForegroundColor = token == "MAIN" ? ConsoleColor.Cyan : ConsoleColor.Green;
                    Console.WriteLine($"{token,-15}: {balance:N8}");
                    Console.ResetColor();
                    hasAnyBalance = true;
                }
            }

            if (!hasAnyBalance)
            {
                Console.WriteLine("Немає балансів на жодному токені.");
            }

            Console.WriteLine("\nДоступні токени в мережі:");

            foreach (var token in allTokens)
            {
                if (token != "MAIN")
                {
                    Console.Write($"{token}  ");
                }
            }

            Console.WriteLine();
        }

        private static void HandleWalletHistory()
        {
            Console.WriteLine("\n=== ІСТОРІЯ ГАМАНЦЯ ===");
            Console.WriteLine(new string('-', 80));

            foreach (var block in _blockchain.Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From == _wallet.PublicKey || tx.To == _wallet.PublicKey)
                    {
                        Console.ForegroundColor = tx.From == "MINT" ? ConsoleColor.Magenta :
                                                  (tx.From == "SYSTEM" ? ConsoleColor.Yellow : ConsoleColor.White);

                        string type = tx.From == "MINT" ? "MINT" :
                                      (tx.From == "SYSTEM" ? "REWARD" : "TRANSFER");

                        string fromShort = tx.From?.Substring(0, Math.Min(10, tx.From?.Length ?? 0)) ?? "";
                        string toShort = tx.To?.Substring(0, Math.Min(10, tx.To?.Length ?? 0)) ?? "";

                        Console.WriteLine($"[{type}] Block #{block.Index,4} | {fromShort}... → {toShort}... | {tx.Amount:F2} {tx.TokenSymbol}");
                        Console.ResetColor();
                    }
                }
            }
        }

        #endregion

        #region Handlers - Network & P2P

        private static void HandleConnectToPeer()
        {
            Console.Write("Введіть адресу піра для підключення (напр. 127.0.0.1:5002): ");
            var peer = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(peer)) return;

            _p2pClient.Connect(peer);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Мережа] Запит на синхронізацію надіслано до {peer}.");
            Console.ResetColor();
        }

        private static async Task HandleRequestAndVerifySPV()
        {
            Console.Write("Введіть ID транзакції для перевірки Merkle Proof: ");
            string txId = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(txId)) return;

            Console.WriteLine("[SPV] Запуск асинхронного запиту Меркл-доказу від підключеної Повної Ноди...");
            await _p2pClient.RequestSpvProofAsync(txId);
        }

        private static void HandleSpvVerification()
        {
            Console.WriteLine("\n=== SPV-ПЕРЕВІРКА ТРАНЗАКЦІЇ ===");

            var block = _blockchain.Chain.LastOrDefault(b => b.Transactions != null && b.Transactions.Count >= 2);

            if (block == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Не знайдено блоку з достатньою кількістю транзакцій.");
                Console.ResetColor();
                return;
            }

            var tx = block.Transactions[0];
            string txHash = MerkleUtilities.ComputeTransactionHash(tx);
            var proof = MerkleUtilities.BuildMerkleProof(block.Transactions, tx.Id);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTarget Transaction ID:");
            Console.ResetColor();
            Console.WriteLine(tx.Id);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nMerkle Proof Hash Path:");
            Console.ResetColor();

            if (proof.Count == 0)
            {
                Console.WriteLine("[Порожній доказ]");
            }
            else
            {
                int stepNumber = 1;

                foreach (var step in proof)
                {
                    Console.WriteLine($"{stepNumber++}. {(step.IsLeftSibling ? "[LEFT ]" : "[RIGHT]")} {step.SiblingHash}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nExpected Merkle Root:");
            Console.ResetColor();
            Console.WriteLine(block.MerkleRoot);

            bool result = MerkleUtilities.VerifyMerkleProof(txHash, proof, block.MerkleRoot);

            Console.WriteLine();

            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SPV Verification Passed: TRUE]");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[SPV Verification Passed: FALSE]");
            }

            Console.ResetColor();
        }

        #endregion

        #region Handlers - Audit & Security

        private static void HandleRunForensicAudit()
        {
            var report = _blockchain.RunFullAudit(_blockchain.Chain);
            var origin = _blockchain.FindAttackOrigin(report, _blockchain.Chain);
            Console.WriteLine(_blockchain.GenerateForensicReport(report, origin));
        }

        private static void HandleSimulateHackerAttack()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n=== СИМУЛЯЦІЯ ХАКЕРСЬКОЇ АТАКИ ===");
            Console.ResetColor();

            var targetBlock = _blockchain.Chain.FirstOrDefault(b => b.Transactions.Any(t => t.From != "SYSTEM"));

            if (targetBlock == null) return;

            var attackTx = targetBlock.Transactions.FirstOrDefault(t => t.From != "SYSTEM");

            if (attackTx == null) return;

            attackTx.Amount = 1_000_000m;
            File.WriteAllText("blockchain.json", JsonSerializer.Serialize(_blockchain.Chain, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine("[Атака] Зламаний blockchain перезаписано на диск!");
        }

        private static void HandleGenerateNetworkPassport()
        {
            Console.Write("Введіть Student ID: ");
            string id = Console.ReadLine();

            var report = _blockchain.RunFullAudit(_blockchain.Chain);
            var origin = _blockchain.FindAttackOrigin(report, _blockchain.Chain);
            var passport = StorageService.GenerateNetworkPassport(_blockchain, id, origin?.Index ?? -1, origin?.Hash ?? "NOT_FOUND");

            StorageService.SavePassport(passport);
            Console.WriteLine("passport.json успішно згенеровано.");
        }

        private static void HandleResetState()
        {
            File.Delete("blockchain.json");
            File.Delete("state.json");
            _blockchain.Chain.Clear();
            Console.WriteLine("Стан скинуто.");
        }

        #endregion

        #region Handlers - Utility

        private static void HandleRunBenchmark()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n=== BENCHMARK START ===");
            Console.ResetColor();

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                var tx = new Transaction("SYSTEM", $"User{i}", 1, 0);
                var block = new Block(_blockchain.Chain.Count, DateTime.UtcNow, new List<Transaction> { tx }, _blockchain.Chain.Last().Hash, "benchmark");

                block.Hash = Guid.NewGuid().ToString();
                _blockchain.Chain.Add(block);
            }

            _blockchain.RebuildState();
            stopwatch.Stop();

            Console.WriteLine($"Швидкість пошуку: {stopwatch.ElapsedMilliseconds} ms");
        }

        private static async Task HandleExecuteSystemDiagnostics()
        {
            Console.WriteLine("Запуск комплексної діагностики...");
            await Task.Delay(100);
        }

        private static void HandleShutdown()
        {
            _p2pServer?.Stop();
            _isRunning = false;
        }

        #endregion
    }
}