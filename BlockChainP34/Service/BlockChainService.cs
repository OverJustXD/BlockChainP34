using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockChainP34.Service
{
    public class BlockChainService
    {
        public Dictionary<string, decimal> Balances { get; private set; } = new();
        public List<Block> Chain { get; private set; }
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

            PendingTransactions.Add(transaction);
            return (true, null);
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

                var coinbaseTx = new Transaction(
                    "SYSTEM",
                    minerPublicKey,
                    reward,
                    0
                );

                var block = new Block(
                    last.Index + 1,
                    DateTime.UtcNow,
                    new List<Transaction> { coinbaseTx },
                    last.Hash,
                    minerPublicKey
                );

                block.BurnedFees = 0;
                block.TipFees = 0;
                block.DifficultyAtMining = Difficulty;

                _miningService.MineBlock(block, Difficulty);

                Chain.Add(block);

                RebuildState();
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

                block.BurnedFees = totalBurned;
                block.TipFees = totalTips;
                block.DifficultyAtMining = Difficulty;

                _miningService.MineBlock(block, Difficulty);

                Chain.Add(block);

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
            Balances.Clear();

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From != "SYSTEM")
                    {
                        if (!Balances.ContainsKey(tx.From))
                            Balances[tx.From] = 0;

                        Balances[tx.From] -= (tx.Amount + tx.Fee);
                    }

                    if (!Balances.ContainsKey(tx.To))
                        Balances[tx.To] = 0;

                    Balances[tx.To] += tx.Amount;
                }
            }
        }

        public decimal GetBalance(string address)
        {
            RebuildState();
            return Balances.GetValueOrDefault(address, 0);
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

            var oldChain = Chain.ToList();
            var oldBalances = new Dictionary<string, decimal>(Balances);

            int blockGap = newChain.Count - oldChain.Count;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Аудит] Наша нода жила в минулому! Ми відстали від реальності на {blockGap} блоків.");
            Console.ResetColor();

            Chain = newChain.ToList();
            PendingTransactions.Clear();

            RebuildState();

            PrintBalanceDelta(oldBalances);
            PrintErasedTransactions(oldChain, newChain);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Виявлено довший ланцюг. Наш ланцюг успішно замінено.");
            Console.ResetColor();

            return true;
        }

        private void PrintBalanceDelta(Dictionary<string, decimal> oldBalances)
        {
            foreach (var pair in oldBalances)
            {
                var address = pair.Key;
                var oldBalance = pair.Value;
                var newBalance = Balances.GetValueOrDefault(address, 0);

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