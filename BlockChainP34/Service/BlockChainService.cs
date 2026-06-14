using BlockChainP34.Models;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockChainP34.Service
{
    public class BlockChainService
    {
        private const string MainToken = "MAIN";
        private const string MintAddress = "MINT";
        private const string SystemAddress = "SYSTEM";

        private const int MinDifficulty = 2;
        private const int MaxDifficulty = 10;

        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;
        private readonly HashSet<string> _knownTokens = new() { MainToken, "ACADEMY_COIN" };
        private readonly double _targetBlockTimeSeconds = 2;
        private readonly int _difficultyAdjustmentInterval = 3;

        public TimeSpan TransactionTtl { get; set; } = TimeSpan.FromMinutes(5);
        public Dictionary<string, Dictionary<string, decimal>> BalancesState { get; private set; } = new();
        public List<Block> Chain { get; set; }
        public List<Transaction> PendingTransactions { get; private set; } = new();
        public decimal NetworkBaseFee { get; set; } = 1.0m;
        public int Difficulty { get; private set; } = 6;

        public BlockChainService(int difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);

            Difficulty = difficulty;
            Chain = new List<Block>();

            AddGenesisBlock();
        }

        #region Private Helpers

        private static string NormalizeTokenSymbol(string? tokenSymbol)
        {
            return string.IsNullOrWhiteSpace(tokenSymbol) ? MainToken : tokenSymbol.Trim().ToUpperInvariant();
        }

        private Dictionary<string, Dictionary<string, decimal>> CloneBalancesState(Dictionary<string, Dictionary<string, decimal>> source)
        {
            var clone = new Dictionary<string, Dictionary<string, decimal>>();

            foreach (var addressPair in source)
            {
                clone[addressPair.Key] = new Dictionary<string, decimal>(addressPair.Value);
            }

            return clone;
        }

        private static decimal GetBalanceFromState(Dictionary<string, Dictionary<string, decimal>> state, string address, string token)
        {
            if (state.TryGetValue(address, out var tokens) && tokens.TryGetValue(token, out var balance))
            {
                return balance;
            }

            return 0m;
        }

        private static void EnsureBalanceBucket(Dictionary<string, Dictionary<string, decimal>> state, string address, string token)
        {
            if (!state.ContainsKey(address))
                state[address] = new Dictionary<string, decimal>();

            if (!state[address].ContainsKey(token))
                state[address][token] = 0m;
        }

        private static void AddBalance(Dictionary<string, Dictionary<string, decimal>> state, string address, string token, decimal amount)
        {
            EnsureBalanceBucket(state, address, token);
            state[address][token] += amount;
        }

        #endregion

        #region Genesis & State Management

        private void AddGenesisBlock()
        {
            var genesis = new Block(0, DateTime.Parse("2024-06-01T00:00:00Z"), new List<Transaction>(), "0", "GENESIS");

            genesis.MerkleRoot = _hashingService.BuildMerkleRoot(genesis.Transactions);
            genesis.Hash = _hashingService.ComputeHash(genesis);

            Chain.Add(genesis);
        }

        private void UpdateBalancesState(Block block)
        {
            foreach (var tx in block.Transactions)
            {
                tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);
                ApplyTransactionToState(BalancesState, tx, validate: false, out _);
            }
        }

        public void RebuildState()
        {
            BalancesState.Clear();

            foreach (var block in Chain)
            {
                UpdateBalancesState(block);
            }
        }

        public bool ValidateAndRebuildState()
        {
            var temp = new Dictionary<string, Dictionary<string, decimal>>();

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (!ApplyTransactionToState(temp, tx, validate: true, out _))
                        return false;
                }
            }

            BalancesState = temp;
            return true;
        }

        public void SaveStateSnapshot()
        {
            var json = JsonSerializer.Serialize(BalancesState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("state.json", json);
        }

        public void LoadStateSnapshot()
        {
            if (File.Exists("state.json"))
            {
                var json = File.ReadAllText("state.json");
                var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(json);

                if (loaded == null)
                {
                    RebuildState();
                    return;
                }

                BalancesState = loaded;
            }
        }

        #endregion

        #region Transaction Processing

        private bool ApplyTransactionToState(Dictionary<string, Dictionary<string, decimal>> state, Transaction tx, bool validate, out string error)
        {
            error = string.Empty;

            if (tx == null)
            {
                error = "Transaction is null.";
                return false;
            }

            tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);
            string token = tx.TokenSymbol;

            if (tx.Amount <= 0)
            {
                error = "Transaction amount must be greater than zero.";
                return false;
            }

            if (tx.Fee < 0)
            {
                error = "Transaction fee cannot be negative.";
                return false;
            }

            if (tx.From == MintAddress)
            {
                if (tx.Fee != 0)
                {
                    error = "Mint transactions must have zero fee.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(token))
                    token = "ACADEMY_COIN";

                _knownTokens.Add(token);
                AddBalance(state, tx.To, token, tx.Amount);
                return true;
            }

            if (tx.From == SystemAddress)
            {
                AddBalance(state, tx.To, token, tx.Amount);
                return true;
            }

            EnsureBalanceBucket(state, tx.From, token);
            EnsureBalanceBucket(state, tx.From, MainToken);
            EnsureBalanceBucket(state, tx.To, token);

            if (token == MainToken)
            {
                decimal requiredMain = tx.Amount + tx.Fee;

                if (validate && GetBalanceFromState(state, tx.From, MainToken) < requiredMain)
                {
                    error = $"Insufficient MAIN balance. Required: {requiredMain}, available: {GetBalanceFromState(state, tx.From, MainToken)}";
                    return false;
                }

                AddBalance(state, tx.From, MainToken, -requiredMain);
                AddBalance(state, tx.To, MainToken, tx.Amount);
                return true;
            }

            if (validate && GetBalanceFromState(state, tx.From, token) < tx.Amount)
            {
                error = $"Insufficient {token} balance. Required: {tx.Amount}, available: {GetBalanceFromState(state, tx.From, token)}";
                return false;
            }

            if (validate && GetBalanceFromState(state, tx.From, MainToken) < tx.Fee)
            {
                error = $"Insufficient MAIN balance for fee. Required: {tx.Fee}, available: {GetBalanceFromState(state, tx.From, MainToken)}";
                return false;
            }

            AddBalance(state, tx.From, token, -tx.Amount);
            AddBalance(state, tx.To, token, tx.Amount);

            if (tx.Fee > 0)
                AddBalance(state, tx.From, MainToken, -tx.Fee);

            return true;
        }

        private Transaction CreateSignedTransaction(string from, string to, decimal amount, decimal fee, string privateKey, string tokenSymbol)
        {
            tokenSymbol = NormalizeTokenSymbol(tokenSymbol);

            if (from == MintAddress)
            {
                return new Transaction(MintAddress, to, amount, fee) { TokenSymbol = tokenSymbol };
            }

            if (tokenSymbol == MainToken)
            {
                var txMain = TransactionService.CreateTransaction(from, to, amount, privateKey, fee);
                txMain.TokenSymbol = MainToken;
                return txMain;
            }

            var overload = typeof(TransactionService)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "CreateTransaction")
                        return false;

                    var p = m.GetParameters();
                    return p.Length == 6 &&
                           p[0].ParameterType == typeof(string) &&
                           p[1].ParameterType == typeof(string) &&
                           p[2].ParameterType == typeof(decimal) &&
                           p[3].ParameterType == typeof(string) &&
                           p[4].ParameterType == typeof(decimal) &&
                           p[5].ParameterType == typeof(string);
                });

            if (overload == null)
            {
                throw new InvalidOperationException(
                    "To create custom-token transactions, TransactionService.CreateTransaction overload with tokenSymbol is required.");
            }

            var result = overload.Invoke(null, new object[] { from, to, amount, privateKey, fee, tokenSymbol });

            if (result is not Transaction tx)
            {
                throw new InvalidOperationException("TransactionService.CreateTransaction returned an invalid result.");
            }

            tx.TokenSymbol = tokenSymbol;
            return tx;
        }

        public (bool success, string error) CreateAndAddTransaction(string from, string to, decimal amount, decimal fee, string privateKey, int lockTime = 0)
        {
            return CreateAndAddTransaction(from, to, amount, fee, privateKey, MainToken, lockTime);
        }

        public (bool success, string error) CreateAndAddTransaction(string from, string to, decimal amount, decimal fee, string privateKey, string tokenSymbol, int lockTime = 0)
        {
            try
            {
                var tx = CreateSignedTransaction(from, to, amount, fee, privateKey, tokenSymbol);
                tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);
                tx.LockTime = lockTime;

                return AddTransactionToMempool(tx);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public (bool success, string error) AddTransactionToMempool(Transaction transaction)
        {
            if (transaction == null)
                return (false, "Transaction is null.");

            transaction.TokenSymbol = NormalizeTokenSymbol(transaction.TokenSymbol);
            string token = transaction.TokenSymbol;

            if (transaction.From == MintAddress)
            {
                if (!HandleMint(transaction))
                    return (false, "Invalid mint transaction");

                PendingTransactions.Add(transaction);
                return (true, null);
            }

            if (!_knownTokens.Contains(token))
                return (false, $"Unknown token: {token}");

            var validation = TransactionService.ValidateTransaction(transaction);

            if (!validation.IsValid)
                return (false, validation.error);

            if (transaction.Fee < 0)
                return (false, "Transaction fee cannot be negative.");

            if (transaction.From != SystemAddress)
            {
                decimal tokenBalance = GetBalance(transaction.From, token);
                decimal mainBalance = GetBalance(transaction.From, MainToken);

                if (token == MainToken)
                {
                    if (tokenBalance < transaction.Amount + transaction.Fee)
                        return (false, $"Insufficient MAIN balance. Balance: {tokenBalance}, required: {transaction.Amount + transaction.Fee}");
                }
                else
                {
                    if (tokenBalance < transaction.Amount)
                        return (false, $"Insufficient {token} balance");

                    if (mainBalance < transaction.Fee)
                        return (false, "Insufficient MAIN balance for fee");
                }
            }

            int pendingFromUser = PendingTransactions.Count(x => x.From == transaction.From);

            if (pendingFromUser >= 3)
                throw new InvalidOperationException("Spam detected.");

            PendingTransactions.Add(transaction);
            return (true, null);
        }

        private bool HandleMint(Transaction tx)
        {
            if (tx.From != MintAddress)
                return false;

            tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);

            if (string.IsNullOrWhiteSpace(tx.TokenSymbol))
                tx.TokenSymbol = "ACADEMY_COIN";

            if (tx.Fee != 0)
                return false;

            if (!_knownTokens.Contains(tx.TokenSymbol))
                _knownTokens.Add(tx.TokenSymbol);

            return tx.Amount > 0;
        }

        public async Task<(bool success, string error)> BroadcastTransactionFromFile(string filePath, P2PClient p2pClient)
        {
            try
            {
                if (!File.Exists(filePath))
                    return (false, "Файл не знайдено.");

                string json = File.ReadAllText(filePath);
                var tx = JsonSerializer.Deserialize<Transaction>(json);

                if (tx == null)
                    return (false, "Некоректний JSON.");

                tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);

                if (tx.From != MintAddress)
                {
                    var validation = TransactionService.ValidateTransaction(tx);

                    if (!validation.IsValid)
                        return (false, $"Підпис невалідний: {validation.error}");
                }
                else
                {
                    if (!HandleMint(tx))
                        return (false, "Invalid mint transaction");
                }

                var result = AddTransactionToMempool(tx);

                if (!result.success)
                    return result;

                await p2pClient.BroadcastTransactionAsync(tx);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion

        #region Mining

        private decimal GetMiningReward(int blockIndex)
        {
            int halvingInterval = 5;
            int halvings = blockIndex / halvingInterval;

            decimal reward = 50;

            for (int i = 0; i < halvings; i++)
                reward /= 2;

            return reward;
        }

        public (bool success, Block block, string error) MineEmptyBlock(string minerPublicKey)
        {
            try
            {
                var last = Chain.Last();
                var reward = GetMiningReward(Chain.Count);

                var coinbaseTx = new Transaction(SystemAddress, minerPublicKey, reward, 0) { TokenSymbol = MainToken };

                var block = new Block(last.Index + 1, DateTime.UtcNow, new List<Transaction> { coinbaseTx }, last.Hash, minerPublicKey);

                block.MerkleRoot = _hashingService.BuildMerkleRoot(block.Transactions);
                block.BurnedFees = 0;
                block.TipFees = 0;
                block.DifficultyAtMining = Difficulty;

                _miningService.MineBlock(block, Difficulty);

                Chain.Add(block);
                RebuildState();
                SaveStateSnapshot();
                AdjustDifficulty();

                return (true, block, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public (bool success, Block block, string error) MinePendingTransactions(string minerPublicKey)
        {
            int evictedCount = PendingTransactions.RemoveAll(tx => (DateTime.UtcNow - tx.TimeStamp) > TransactionTtl);

            if (evictedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Mempool] Видалено {evictedCount} застарілих транзакцій за TTL.");
                Console.ResetColor();
            }

            if (PendingTransactions.Count == 0)
                return (false, null, "No transactions to mine.");

            try
            {
                var allowedTransactions = PendingTransactions.Where(tx => tx.LockTime <= Chain.Count).ToList();

                if (allowedTransactions.Count == 0)
                    return (false, null, "Всі транзакції в Mempool заблоковані за LockTime.");

                var transactionsToInclude = allowedTransactions.OrderByDescending(tx => tx.Amount).Take(10).ToList();

                decimal totalTips = 0;
                decimal totalBurned = 0;

                foreach (var tx in transactionsToInclude)
                {
                    if (tx.From == SystemAddress)
                        continue;

                    if (tx.From == MintAddress && tx.Fee > 0)
                        continue;

                    decimal burnRate = 0.5m;
                    decimal tip = tx.Fee * (1 - burnRate);
                    decimal burn = tx.Fee * burnRate;

                    totalTips += tip;
                    totalBurned += burn;
                }

                decimal reward = GetMiningReward(Chain.Count);
                decimal totalReward = reward + totalTips;

                var rewardTx = new Transaction(SystemAddress, minerPublicKey, totalReward, 0) { TokenSymbol = MainToken };
                transactionsToInclude.Add(rewardTx);

                var last = Chain.Last();

                var block = new Block(last.Index + 1, DateTime.UtcNow, transactionsToInclude, last.Hash, minerPublicKey);

                block.MerkleRoot = _hashingService.BuildMerkleRoot(transactionsToInclude);
                block.BurnedFees = totalBurned;
                block.TipFees = totalTips;
                block.DifficultyAtMining = Difficulty;

                _miningService.MineBlock(block, Difficulty);

                Chain.Add(block);
                RebuildState();
                SaveStateSnapshot();

                var includedIds = transactionsToInclude.Select(t => t.Id).ToHashSet();
                PendingTransactions.RemoveAll(tx => includedIds.Contains(tx.Id));

                AdjustDifficulty();

                return (true, block, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private void AdjustDifficulty()
        {
            if (Chain.Count < _difficultyAdjustmentInterval + 1)
                return;

            var recentBlocks = Chain.Skip(Chain.Count - _difficultyAdjustmentInterval).ToList();

            double avgTime = recentBlocks.Average(b => b.MiningDurationSecond);
            double targetTime = _targetBlockTimeSeconds;

            if (avgTime <= 0)
                avgTime = 0.001;

            double ratio = targetTime / avgTime;

            if (ratio > 1.3)
                Difficulty++;
            else if (ratio < 0.7)
                Difficulty--;

            Difficulty = Math.Clamp(Difficulty, MinDifficulty, MaxDifficulty);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Difficulty adjusted → {Difficulty} | avg={avgTime:F4}s");
            Console.ResetColor();
        }

        #endregion

        #region Balance & Queries

        public decimal GetBalance(string address, string token = MainToken)
        {
            token = NormalizeTokenSymbol(token);

            decimal chainBalance = GetBalanceFromState(BalancesState, address, token);
            decimal mempoolDelta = 0m;

            foreach (var tx in PendingTransactions)
            {
                tx.TokenSymbol = NormalizeTokenSymbol(tx.TokenSymbol);

                if (tx.TokenSymbol == token)
                {
                    if (tx.From == address)
                        mempoolDelta -= tx.Amount;

                    if (tx.To == address)
                        mempoolDelta += tx.Amount;
                }

                if (token == MainToken && tx.From == address && tx.From != SystemAddress && tx.From != MintAddress && tx.Fee > 0)
                {
                    mempoolDelta -= tx.Fee;
                }

                if (token == MainToken && tx.TokenSymbol == MainToken && tx.From == address)
                {
                    mempoolDelta -= tx.Amount;
                }
            }

            return chainBalance + mempoolDelta;
        }

        public decimal GetTotalSupply()
        {
            decimal mintedMain = 0m;
            decimal burned = 0m;

            foreach (var block in Chain)
            {
                burned += block.BurnedFees;

                foreach (var tx in block.Transactions)
                {
                    if (tx.From == SystemAddress && NormalizeTokenSymbol(tx.TokenSymbol) == MainToken)
                    {
                        mintedMain += tx.Amount;
                    }
                }
            }

            return mintedMain - burned;
        }

        public bool IsTokenKnown(string tokenSymbol)
        {
            tokenSymbol = NormalizeTokenSymbol(tokenSymbol);
            return _knownTokens.Contains(tokenSymbol);
        }

        public HashSet<string> GetKnownTokens()
        {
            return new HashSet<string>(_knownTokens);
        }

        #endregion

        #region Chain Validation & Audit

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var current = Chain[i];
                var prev = Chain[i - 1];

                if (current.PrevHash != prev.Hash)
                    return false;

                if (current.Hash != _hashingService.ComputeHash(current))
                    return false;

                if (!current.Hash.StartsWith(new string('0', current.DifficultyAtMining)))
                    return false;

                foreach (var tx in current.Transactions)
                {
                    if (tx.From == SystemAddress || tx.From == MintAddress)
                        continue;

                    var validation = TransactionService.ValidateTransaction(tx);

                    if (!validation.IsValid)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL] Підроблена транзакція! ID={tx.Id}");
                        Console.ResetColor();

                        LogSecurityIncident(tx);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsChainValid(List<Block> chain)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                var current = chain[i];

                if (current.Hash != _hashingService.ComputeHash(current))
                    return false;

                if (!current.Hash.StartsWith(new string('0', current.DifficultyAtMining)))
                    return false;

                if (i > 0)
                {
                    var prev = chain[i - 1];

                    if (current.PrevHash != prev.Hash)
                        return false;
                }
            }

            return true;
        }

        private void LogSecurityIncident(Transaction tx)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] АТАКА ВИЯВЛЕНА! Підроблена транзакція ID: {tx.Id}. Спроба змінити суму на: {tx.Amount}.";

            File.AppendAllText("security_alerts.txt", logLine + Environment.NewLine);
        }

        public bool ReplaceChain(List<Block> newChain)
        {
            if (newChain == null || newChain.Count == 0)
                return false;

            if (newChain.Count <= Chain.Count)
                return false;

            if (!IsChainValid(newChain))
                return false;

            int forkPoint = 0;
            while (forkPoint < Chain.Count && forkPoint < newChain.Count && Chain[forkPoint].Hash == newChain[forkPoint].Hash)
            {
                forkPoint++;
            }

            var orphanTransactions = new List<Transaction>();

            for (int i = forkPoint; i < Chain.Count; i++)
            {
                foreach (var tx in Chain[i].Transactions)
                {
                    if (tx.From != SystemAddress)
                        orphanTransactions.Add(tx);
                }
            }

            var newChainTxIds = new HashSet<string>();

            for (int i = forkPoint; i < newChain.Count; i++)
            {
                foreach (var tx in newChain[i].Transactions)
                {
                    newChainTxIds.Add(tx.Id);
                }
            }

            var oldChain = Chain.ToList();
            var oldBalances = CloneBalancesState(BalancesState);
            var oldMempool = PendingTransactions.ToList();

            int blockGap = newChain.Count - oldChain.Count;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Аудит] Наша нода жила в минулому! Ми відстали від реальності на {blockGap} блоків.");
            Console.ResetColor();

            Chain = newChain.ToList();

            var newMempool = new List<Transaction>();

            foreach (var tx in orphanTransactions)
            {
                if (!newChainTxIds.Contains(tx.Id) && !newMempool.Any(t => t.Id == tx.Id))
                {
                    newMempool.Add(tx);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Операція Фенікс] Транзакцію {tx.Id} врятовано з відкинутого блоку та повернено в Mempool.");
                    Console.ResetColor();
                }
            }

            foreach (var tx in oldMempool)
            {
                if (!newChainTxIds.Contains(tx.Id) && !newMempool.Any(t => t.Id == tx.Id))
                {
                    newMempool.Add(tx);
                }
            }

            PendingTransactions = newMempool;

            if (!ValidateAndRebuildState())
            {
                Chain = oldChain;
                PendingTransactions = oldMempool;
                BalancesState = oldBalances;
                return false;
            }

            PrintBalanceDelta(oldBalances);
            PrintErasedTransactions(oldChain, newChain);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Виявлено довший ланцюг. Наш ланцюг успішно замінено.");
            Console.ResetColor();

            return true;
        }

        public int EvictStaleTransactions(int maxAgeSeconds)
        {
            int before = PendingTransactions.Count;

            PendingTransactions = PendingTransactions
                .Where(tx => (DateTime.UtcNow - tx.TimeStamp).TotalSeconds <= maxAgeSeconds)
                .ToList();

            return before - PendingTransactions.Count;
        }

        public AuditReport RunFullAudit(List<Block> chain)
        {
            var report = new AuditReport();

            if (chain == null || chain.Count == 0)
            {
                report.IsChainValid = false;
                return report;
            }

            for (int i = 0; i < chain.Count; i++)
            {
                var current = chain[i];
                var violations = new List<string>();

                if (i > 0)
                {
                    var prev = chain[i - 1];

                    if (current.PrevHash != prev.Hash)
                    {
                        violations.Add($"[Block #{current.Index}] PrevHash mismatch — expected {prev.Hash}, actual {current.PrevHash}");
                    }
                }

                var expectedMerkleRoot = _hashingService.BuildMerkleRoot(current.Transactions);

                if (current.MerkleRoot != expectedMerkleRoot)
                {
                    violations.Add($"[Block #{current.Index}] MerkleRoot mismatch — транзакції були підроблені");
                }

                int difficultyToCheck = current.DifficultyAtMining > 0 ? current.DifficultyAtMining : Difficulty;
                string requiredPrefix = new string('0', difficultyToCheck);

                if (string.IsNullOrWhiteSpace(current.Hash) || !current.Hash.StartsWith(requiredPrefix))
                {
                    violations.Add($"[Block #{current.Index}] Hash does not meet difficulty — блок не перемайнено");
                }

                if (violations.Count > 0)
                {
                    report.CompromisedBlockIndexes.Add(current.Index);
                    report.ViolationDetails[current.Index] = violations;
                }
            }

            report.IsChainValid = report.CompromisedBlockIndexes.Count == 0;
            return report;
        }

        public Block FindAttackOrigin(AuditReport report, List<Block> chain)
        {
            if (report == null || chain == null || chain.Count == 0)
                return null;

            foreach (var block in chain.OrderBy(b => b.Index))
            {
                if (block.Index == 0)
                    continue;

                if (!report.ViolationDetails.TryGetValue(block.Index, out var violations))
                    continue;

                bool hasNonPrevHashViolation = violations.Any(v => !v.Contains("PrevHash mismatch", StringComparison.OrdinalIgnoreCase));

                if (hasNonPrevHashViolation)
                    return block;
            }

            return null;
        }

        public string GenerateForensicReport(AuditReport report, Block attackOrigin)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== FORENSIC AUDIT REPORT ===");
            sb.AppendLine($"Chain status: {(report.IsChainValid ? "VALID" : "COMPROMISED")}");

            if (attackOrigin != null)
            {
                sb.AppendLine($"Attack origin: Block #{attackOrigin.Index} (timestamp: {attackOrigin.Timestamp:O})");
            }
            else
            {
                sb.AppendLine("Attack origin: NOT FOUND");
            }

            int affectedBlocks = attackOrigin != null
                ? report.CompromisedBlockIndexes.Count(i => i >= attackOrigin.Index)
                : report.CompromisedBlockIndexes.Count;

            sb.AppendLine($"Total affected blocks: {affectedBlocks}");
            sb.AppendLine();
            sb.AppendLine("VIOLATION LOG:");

            foreach (var blockIndex in report.CompromisedBlockIndexes.OrderBy(x => x))
            {
                if (!report.ViolationDetails.TryGetValue(blockIndex, out var violations))
                    continue;

                foreach (var violation in violations)
                {
                    sb.AppendLine(violation);
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Private Reporting Methods

        private void PrintBalanceDelta(Dictionary<string, Dictionary<string, decimal>> oldBalances)
        {
            var addresses = oldBalances.Keys.Union(BalancesState.Keys).ToList();

            foreach (var address in addresses)
            {
                oldBalances.TryGetValue(address, out var oldTokens);
                BalancesState.TryGetValue(address, out var newTokens);

                oldTokens ??= new Dictionary<string, decimal>();
                newTokens ??= new Dictionary<string, decimal>();

                var allTokens = oldTokens.Keys.Union(newTokens.Keys).ToList();

                foreach (var token in allTokens)
                {
                    decimal oldBalance = oldTokens.TryGetValue(token, out var oldValue) ? oldValue : 0m;
                    decimal newBalance = newTokens.TryGetValue(token, out var newValue) ? newValue : 0m;

                    var loss = oldBalance - newBalance;

                    if (loss > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[Аудит Балансу] Увага! Користувач {address} раптово збіднів на {loss} {token} через відкат мережі!");
                        Console.ResetColor();
                    }
                }
            }
        }

        private void PrintErasedTransactions(List<Block> oldChain, List<Block> newChain)
        {
            var oldTxIds = CollectUserTransactionIds(oldChain);
            var newTxIds = CollectUserTransactionIds(newChain);

            var erasedTxIds = oldTxIds.Except(newTxIds).ToList();

            foreach (var txId in erasedTxIds)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[АЛАРМ] Транзакцію {txId} стерто з історії всесвіту! Переказ скасовано.");
                Console.ResetColor();
            }
        }

        private HashSet<string> CollectUserTransactionIds(List<Block> chain)
        {
            var ids = new HashSet<string>();

            foreach (var block in chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (IsMiningRewardTransaction(tx))
                        continue;

                    ids.Add(tx.Id);
                }
            }

            return ids;
        }

        private bool IsMiningRewardTransaction(Transaction tx)
        {
            return tx != null && !string.IsNullOrWhiteSpace(tx.From) && tx.From.Equals(SystemAddress, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}