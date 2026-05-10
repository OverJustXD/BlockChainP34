using BlockChainP34.Class;
using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockChainP34.Service
{
    public class BlockChainService
    {

        public Dictionary<string, decimal> Balances { get; set; } = new();
        public List<Block> Chain { get; set; }

        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;

        public decimal NetworkBaseFee { get; set; } = 1.0m;
        public List<Transaction> PendingTransactions { get; set; } = new();

        private readonly double _targetBlockTimeSeconds = 1;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty { get; set; } = 1;

        private const int MinDifficulty = 1;
        private const int MaxDifficulty = 6;

        public BlockChainService(int difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);

            Chain = new List<Block>();
            PendingTransactions = new List<Transaction>();

            Difficulty = difficulty;

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

        private decimal GetMiningReward(int blockIndex)
        {
            int halvingInterval = 5;
            int halvings = blockIndex / halvingInterval;

            decimal reward = 50;

            for (int i = 0; i < halvings; i++)
                reward /= 2;

            return reward;
        }


        public void RebuildState()
        {
            Balances.Clear();

            foreach (var block in Chain)
            {
                if (block.Transactions == null) continue;

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


        public void AddTransactionToMempool(Transaction transaction)
        {
            var result = TransactionService.ValidateTransaction(transaction);

            if (!result.IsValid)
                throw new BlockchainException($"Invalid transaction: {result.error}");

            if (transaction.Fee < 0)
                throw new BlockchainException("Transaction fee cannot be negative.");

            int pendingCount = PendingTransactions.Count(tx => tx.From == transaction.From);

            if (pendingCount >= 3)
                throw new BlockchainException("Spam detected: too many pending transactions.");

            RebuildState();

            decimal currentBalance = Balances.GetValueOrDefault(transaction.From, 0);

            decimal pendingSpent = PendingTransactions
                .Where(tx => tx.From == transaction.From)
                .Sum(tx => tx.Amount + tx.Fee);

            decimal totalAfterTx = pendingSpent + transaction.Amount + transaction.Fee;

            if (currentBalance < totalAfterTx)
            {
                throw new BlockchainException(
                    $"Insufficient balance considering pending transactions.\n" +
                    $"Available: {currentBalance}\n" +
                    $"Already pending: {pendingSpent}\n" +
                    $"New tx cost: {transaction.Amount + transaction.Fee}"
                );
            }

            PendingTransactions.Add(transaction);

            Console.WriteLine($"Transaction added. Fee: {transaction.Fee}");
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


        public void MinePendingTransactions(string minerPublicKey)
        {
            PendingTransactions = PendingTransactions
                .Where(tx => (DateTime.UtcNow - tx.TimeStamp).TotalSeconds <= 60)
                .ToList();

            var transactionsToInclude = PendingTransactions
                .OrderByDescending(tx => Math.Max(0, tx.Fee - NetworkBaseFee))
                .Take(10)
                .ToList();

            decimal totalTips = 0;
            decimal totalBurned = 0;

            foreach (var tx in transactionsToInclude)
            {
                if (tx.From == "SYSTEM") continue; 
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

            Console.WriteLine(
                $"Block mined.\nReward: {reward}\nTips: {totalTips}\nBurned: {totalBurned}\nTotal: {totalReward}"
            );

            AdjustDifficulty();
        }


        private void AdjustDifficulty()
        {
            if (Chain.Count < _difficultyAdjustmentInterval + 1)
                return;

            var recent = Chain
                .Skip(Chain.Count - _difficultyAdjustmentInterval)
                .ToList();

            var avg = recent.Average(b => b.MiningDurationSecond);

            int change = 0;
            if (avg < _targetBlockTimeSeconds * 0.5)
                change = 1;
            else if (avg > _targetBlockTimeSeconds * 2)
                change = -1;
            else
                change = 0;

            Difficulty = Math.Clamp(Difficulty + change, MinDifficulty, MaxDifficulty);

            Console.WriteLine($"Difficulty now: {Difficulty} (avg {avg:F2}s)");
        }


        public bool IsValid()
        {
            if (Chain.Count == 0) return false;

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
    }
}