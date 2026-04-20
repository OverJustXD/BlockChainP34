using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Service
{
    public class BlockChainService
    {
        public List<Block> Chain { get; set; }

        private readonly HashingService _hashingService;
        private readonly MiningService _miningService;

        private readonly double _targetBlockTimeSeconds = 1;
        private readonly int _difficultyAdjustmentInterval = 3;

        public int Difficulty = 1;

        public BlockChainService(int Difficulty)
        {
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            Chain = new List<Block>();
            this.Difficulty = Difficulty;
            AddGenesisBlock();
        }
        private void AddGenesisBlock()
        {
            var block = new Block(0, DateTime.Parse("2024-06-01T00:00:00Z"), "Genesis Block", "0", "Name");
            block.Hash = _hashingService.ComputeHash(block);
            Chain.Add(block);
        }

        public void AddBlock(string data, string author)
        {
            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, data, lastBlock.Hash, author);

            newBlock.Hash = _hashingService.ComputeHash(newBlock);
            _miningService.MineBlock(newBlock, Difficulty);
            Chain.Add(newBlock);

            if (newBlock.Index % _difficultyAdjustmentInterval == 0)
            {
                AddjustDifficulty();
            }
        }

        private const int MinDifficulty = 1;
        private const int MaxDifficulty = 6;

        private void AddjustDifficulty()
        {
            if (Chain.Count < _difficultyAdjustmentInterval + 1)
                return;

            var recentBlocks = Chain
                .Skip(Math.Max(0, Chain.Count - _difficultyAdjustmentInterval))
                .Take(_difficultyAdjustmentInterval)
                .ToList();

            var averageMiningTime = recentBlocks.Average(b => b.MiningDurationSecond);

            int change;

            if (averageMiningTime < _targetBlockTimeSeconds / 5)
            {
                change = 2;
            }
            else if (averageMiningTime > _targetBlockTimeSeconds * 5)
            {
                change = -2;
            }
            else if (averageMiningTime < _targetBlockTimeSeconds)
            {
                change = 1;
            }
            else if (averageMiningTime > _targetBlockTimeSeconds)
            {
                change = -1;
            }
            else
            {
                change = 0;
            }

            var oldDifficulty = Difficulty;
            Difficulty += change;

            if (Difficulty < MinDifficulty)
                Difficulty = MinDifficulty;
            if (Difficulty > MaxDifficulty)
                Difficulty = MaxDifficulty;

            if (Difficulty > oldDifficulty)
            {
                Console.WriteLine($"Difficulty increased to {Difficulty} (average mining time: {averageMiningTime:F2} seconds)");
            }
            else if (Difficulty < oldDifficulty)
            {
                Console.WriteLine($"Difficulty decreased to {Difficulty} (average mining time: {averageMiningTime:F2} seconds)");
            }
            else
            {
                Console.WriteLine($"Difficulty remains at {Difficulty} (average mining time: {averageMiningTime:F2} seconds)");
            }
        }

        public string PrintDifficultyHistoy()
        {
            Console.WriteLine("Block Difficulty History:");
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];

                Console.WriteLine($"Block #{currentBlock.Index} - Difficulty at mining: {currentBlock.DifficultyAtMining} - Mining duration: {currentBlock.MiningDurationSecond:F2} seconds");
            }
            return new string('=', 50);
        }

        public bool IsValid()
        {
            if (Chain.Count == 0) return false;

            var genesis = Chain[0];
            if (genesis.Index != 0) return false;
            if (genesis.PrevHash != "0") return false;
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return false;

            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];
                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock))
                    return false;
                if (currentBlock.PrevHash != previousBlock.Hash)
                    return false;
                if (!currentBlock.Hash.StartsWith(new string('0', currentBlock.DifficultyAtMining)))
                    return false;
            }
            return true;
        }

        public string AnalyzeChain()
        {
            if (Chain.Count == 0) return "Genesis block!";
            var genesis = Chain[0];
            if (genesis.Index != 0) return "Genesis block is invalid.";
            if (genesis.PrevHash != "0") return "Genesis block is invalid.";
            if (genesis.Hash != _hashingService.ComputeHash(genesis)) return "Genesis block is invalid.";


            var hasError = false;
            Console.ForegroundColor = ConsoleColor.Red;
            for (int i = 1; i < Chain.Count; i++)
            {
                var currentBlock = Chain[i];
                var previousBlock = Chain[i - 1];

                if (_hashingService.ComputeHash(currentBlock) != currentBlock.Hash)
                {
                    var error = $"Error at block #{currentBlock.Index}: Hash doesn't match block's data. (Data/timestamp/Nonce tampered)\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
                if (!currentBlock.Hash.StartsWith(new string('0', currentBlock.DifficultyAtMining)))
                {
                    var error = $"Error at block #{currentBlock.Index}: Hash doesn't meet the required difficulty.\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
                if (currentBlock.PrevHash != previousBlock.Hash)
                {
                    var error = $"Error at block #{currentBlock.Index}: Chain link broken. Previous Hash doesn't match the hash of the previous block.\n{new string('-', 50)}";
                    Console.WriteLine(error);
                    hasError = true;
                }
            }
            if (hasError) return $"Chain is invalid. Errors can be seen above.\n";
            Console.ForegroundColor = ConsoleColor.White;
            return $"Chain is valid with {Chain.Count} blocks. Last block hash: {Chain.Last().Hash}";
        }
    }
}

