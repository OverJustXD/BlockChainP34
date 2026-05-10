using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlockChainP34.Models;

namespace BlockChainP34.Service
{
    public class MiningService
    {
        private readonly HashingService _hashingService;

        public MiningService(HashingService hashingService)
        {
            _hashingService = hashingService;
        }

        public long MineBlock(Block block, int difficulty)
        {
            var target = new string('0', difficulty);
            var stopwatch = Stopwatch.StartNew();

            long maxNonce = long.MaxValue;

            while (block.Nonce < maxNonce)
            {
                block.Nonce++;
                block.Hash = _hashingService.ComputeHash(block);

                if (block.Nonce % 100000 == 0)
                    Console.Write(".");

                if (block.Hash.StartsWith(target))
                {
                    stopwatch.Stop();

                    Console.WriteLine($"\nBlock mined with nonce: {block.Nonce}, hash: {block.Hash}");

                    block.MiningDurationSecond = stopwatch.Elapsed.TotalSeconds;
                    block.DifficultyAtMining = difficulty;

                    return block.Nonce;
                }
            }

            throw new Exception("Mining failed (nonce limit reached)");
        }
    }
}
