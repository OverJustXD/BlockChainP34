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
            var target = new String('0', difficulty);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                block.Nonce++;
                block.Hash = _hashingService.ComputeHash(block);

                if (block.Nonce % 100000 == 0)
                {
                    Console.Write(".");
                }

                if (block.Hash.StartsWith(target))
                {
                    Console.WriteLine($"\nBlock mined with nonce: {block.Nonce}, hash: {block.Hash}");
                    stopwatch.Stop();
                    block.MiningDurationSecond = stopwatch.Elapsed.TotalSeconds;
                    block.DifficultyAtMining = difficulty;
                    return block.Nonce;
                }
            }
        }
    }
}
