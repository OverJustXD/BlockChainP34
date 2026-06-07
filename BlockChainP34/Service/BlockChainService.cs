using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace BlockChainP34.Service
{
    public class BlockChainService
    {
        public Dictionary<string, decimal> BalancesState { get; private set; } = new();
        public List<Block> Chain { get; set; }
        public List<Transaction> PendingTransactions { get; private set; } = new();

        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;

        public decimal NetworkBaseFee { get; set; } = 1.0m;

        private readonly double _targetBlockTimeSeconds = 2;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty { get; private set; } = 6;

        private const int MinDifficulty = 2;
        private const int MaxDifficulty = 10;

        public BlockChainService(int difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);

            Difficulty = difficulty;
            Chain = new List<Block>();

            AddGenesisBlock();
        }


        private void AddGenesisBlock()
        {
            var genesis = new Block(
                0,
                DateTime.Parse("2024-06-01T00:00:00Z"),
                new List<Transaction>(),
                "0",
                "GENESIS"
            );

            genesis.MerkleRoot = _hashingService.BuildMerkleRoot(genesis.Transactions);
            genesis.Hash = _hashingService.ComputeHash(genesis);

            Chain.Add(genesis);
        }

        public (bool success, string error) CreateAndAddTransaction(
            string from,
            string to,
            decimal amount,
            decimal fee,
            string privateKey)
        {
            try
            {
                var tx = TransactionService.CreateTransaction(
                    from,
                    to,
                    amount,
                    privateKey,
                    fee
                );

                var result = AddTransactionToMempool(tx);
                return result;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public (bool success, string error) AddTransactionToMempool(Transaction transaction)
        {
            var validation = TransactionService.ValidateTransaction(transaction);

            if (!validation.IsValid)
                return (false, validation.error);

            if (transaction.Fee < 0)
                return (false, "Transaction fee cannot be negative.");

            if (transaction.From != "SYSTEM")
            {
                decimal senderBalance = GetBalance(transaction.From);

                decimal totalCost = transaction.Amount + transaction.Fee;

                if (senderBalance < totalCost)
                    return (false, $"Insufficient funds. Balance: {senderBalance}, required: {totalCost}");
            }

            int pendingFromUser = PendingTransactions.Count(x => x.From == transaction.From);

            if (pendingFromUser >= 3)
                throw new InvalidOperationException("Spam detected.");

            PendingTransactions.Add(transaction);

            return (true, null);
        }

        private void UpdateBalancesState(Block block)
        {
            foreach (var tx in block.Transactions)
            {
                if (tx.From != "SYSTEM")
                {
                    if (!BalancesState.ContainsKey(tx.From))
                        BalancesState[tx.From] = 0;

                    BalancesState[tx.From] -= (tx.Amount + tx.Fee);
                }

                if (!BalancesState.ContainsKey(tx.To))
                    BalancesState[tx.To] = 0;

                BalancesState[tx.To] += tx.Amount;
            }
        }

        public void SaveStateSnapshot()
        {
            var json = JsonSerializer.Serialize(BalancesState,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText("state.json", json);
        }

        public void LoadStateSnapshot()
        {
            if (File.Exists("state.json"))
            {
                var json = File.ReadAllText("state.json");

                var loaded = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);

                if (loaded == null || loaded.Count == 0)
                {
                    Console.WriteLine("Snapshot invalid → rebuilding...");
                    RebuildState();
                    return;
                }

                BalancesState = loaded;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("State snapshot loaded.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Snapshot not found. Rebuilding state...");
                Console.ResetColor();

                RebuildState();
            }
        }

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

                var coinbaseTx = new Transaction("SYSTEM", minerPublicKey, reward, 0);

                var block = new Block(
                    last.Index + 1,
                    DateTime.UtcNow,
                    new List<Transaction> { coinbaseTx },
                    last.Hash,
                    minerPublicKey
                );

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
            if (PendingTransactions.Count == 0)
                return (false, null, "No transactions to mine.");

            EvictStaleTransactions(60);

            try
            {
                PendingTransactions = PendingTransactions
                    .Where(tx => (DateTime.UtcNow - tx.TimeStamp).TotalSeconds <= 600)
                    .ToList();

                var transactionsToInclude = PendingTransactions
                    .OrderByDescending(tx => Math.Max(0, tx.Fee - NetworkBaseFee))
                    .Take(10)
                    .ToList();

                decimal totalTips = 0;
                decimal totalBurned = 0;

                foreach (var tx in transactionsToInclude)
                {
                    if (tx.From == "SYSTEM")
                        continue;

                    decimal burnRate = 0.5m;

                    decimal tip = tx.Fee * (1 - burnRate);
                    decimal burn = tx.Fee * burnRate;

                    totalTips += tip;
                    totalBurned += burn;
                }

                decimal reward = GetMiningReward(Chain.Count);
                decimal totalReward = reward + totalTips;

                var rewardTx = new Transaction("SYSTEM", minerPublicKey, totalReward, 0);
                transactionsToInclude.Add(rewardTx);

                var last = Chain.Last();

                var block = new Block(
                    last.Index + 1,
                    DateTime.UtcNow,
                    transactionsToInclude,
                    last.Hash,
                    minerPublicKey
                );

                block.MerkleRoot = _hashingService.BuildMerkleRoot(transactionsToInclude);
                block.BurnedFees = totalBurned;
                block.TipFees = totalTips;
                block.DifficultyAtMining = Difficulty;

                _miningService.MineBlock(block, Difficulty);

                Chain.Add(block);
                SaveStateSnapshot();
                RebuildState();
                var includedIds = transactionsToInclude.Select(t => t.Id).ToHashSet();
                PendingTransactions.RemoveAll(tx => includedIds.Contains(tx.Id));

                RebuildState();
                AdjustDifficulty();

                return (true, block, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
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

        public decimal GetBalance(string address)
        {
            decimal balance = BalancesState.GetValueOrDefault(address, 0);

            foreach (var tx in PendingTransactions)
            {
                if (tx.From == address)
                    balance -= (tx.Amount + tx.Fee);

                if (tx.To == address)
                    balance += tx.Amount;
            }

            return balance;
        }

        public decimal GetTotalSupply()
        {
            decimal minted = 0;

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From == "SYSTEM")
                        minted += tx.Amount;
                }
            }

            return minted;
        }

        private void AdjustDifficulty()
        {
            if (Chain.Count < _difficultyAdjustmentInterval + 1)
                return;

            var recentBlocks = Chain
                .Skip(Chain.Count - _difficultyAdjustmentInterval)
                .ToList();

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

        private void LogSecurityIncident(Transaction tx)
        {
            string logLine =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                $"АТАКА ВИЯВЛЕНА! " +
                $"Підроблена транзакція ID: {tx.Id}. " +
                $"Спроба змінити суму на: {tx.Amount}.";

            File.AppendAllText(
                "security_alerts.txt",
                logLine + Environment.NewLine
            );
        }

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

                if (!current.Hash.StartsWith(
                    new string('0', current.DifficultyAtMining)))
                    return false;

                foreach (var tx in current.Transactions)
                {
                    if (tx.From == "SYSTEM")
                        continue;

                    var validation =
                        TransactionService.ValidateTransaction(tx);

                    if (!validation.IsValid)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;

                        Console.WriteLine(
                            $"[CRITICAL] Підроблена транзакція! " +
                            $"ID={tx.Id}"
                        );

                        Console.ResetColor();

                        LogSecurityIncident(tx);

                        return false;
                    }
                }
            }

            return true;
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
                    if (tx.From != "SYSTEM")
                    {
                        orphanTransactions.Add(tx);
                    }
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
            var oldBalances = new Dictionary<string, decimal>(BalancesState);

            int blockGap = newChain.Count - oldChain.Count;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Аудит] Наша нода жила в минулому! Ми відстали від реальності на {blockGap} блоків.");
            Console.ResetColor();

            Chain = newChain.ToList();

            var currentMempool = PendingTransactions.ToList();
            PendingTransactions.Clear();

            foreach (var tx in orphanTransactions)
            {
                if (!newChainTxIds.Contains(tx.Id))
                {
                    PendingTransactions.Add(tx);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Операція Фенікс] Транзакцію {tx.Id} врятовано з відкинутого блоку та повернено в Mempool.");
                    Console.ResetColor();
                }
            }

            foreach (var tx in currentMempool)
            {
                if (!newChainTxIds.Contains(tx.Id) && !PendingTransactions.Any(t => t.Id == tx.Id))
                {
                    PendingTransactions.Add(tx);
                }
            }

            RebuildState();

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

        public bool ValidateAndRebuildState()
        {
            var temp = new Dictionary<string, decimal>();

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From != "SYSTEM")
                    {
                        if (!temp.ContainsKey(tx.From))
                            temp[tx.From] = 0;

                        temp[tx.From] -= (tx.Amount + tx.Fee);

                        if (temp[tx.From] < 0)
                            return false;
                    }

                    if (!temp.ContainsKey(tx.To))
                        temp[tx.To] = 0;

                    temp[tx.To] += tx.Amount;
                }
            }

            BalancesState = temp;
            return true;
        }


        public BlockChainP34.Models.AuditReport RunFullAudit(List<Block> chain)
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
                        violations.Add(
                            $"[Block #{current.Index}] PrevHash mismatch — expected {prev.Hash}, actual {current.PrevHash}"
                        );
                    }
                }

                var expectedMerkleRoot = _hashingService.BuildMerkleRoot(current.Transactions);

                if (current.MerkleRoot != expectedMerkleRoot)
                {
                    violations.Add(
                        $"[Block #{current.Index}] MerkleRoot mismatch — транзакції були підроблені"
                    );
                }

                int difficultyToCheck = current.DifficultyAtMining > 0
                    ? current.DifficultyAtMining
                    : Difficulty;

                string requiredPrefix = new string('0', difficultyToCheck);

                if (string.IsNullOrWhiteSpace(current.Hash) || !current.Hash.StartsWith(requiredPrefix))
                {
                    violations.Add(
                        $"[Block #{current.Index}] Hash does not meet difficulty — блок не перемайнено"
                    );
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

                bool hasNonPrevHashViolation = violations.Any(v =>
                    !v.Contains("PrevHash mismatch", StringComparison.OrdinalIgnoreCase));

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
                sb.AppendLine(
                    $"Attack origin: Block #{attackOrigin.Index} (timestamp: {attackOrigin.Timestamp:O})"
                );
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

        private void PrintBalanceDelta(Dictionary<string, decimal> oldBalances)
        {
            foreach (var pair in oldBalances)
            {
                var address = pair.Key;
                var oldBalance = pair.Value;
                var newBalance = BalancesState.GetValueOrDefault(address, 0);

                var loss = oldBalance - newBalance;

                if (loss > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Аудит Балансу] Увага! Користувач {address} раптово збіднів на {loss} монет через відкат мережі!");
                    Console.ResetColor();
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
            return tx != null
                   && !string.IsNullOrWhiteSpace(tx.From)
                   && tx.From.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase);
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
    }
}