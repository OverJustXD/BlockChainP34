using BlockChainP34.Models;
using System;
using System.Collections.Generic;

namespace BlockChainP34.Service
{
    public class BlockchainAnalyzer
    {
        private readonly HashingService _hashingService;

        public bool Enabled { get; set; } = true;

        public BlockchainAnalyzer()
        {
            _hashingService = new HashingService();
        }

        public bool Analyze(List<Block> chain)
        {
            if (!Enabled)
                return true;

            if (chain.Count == 0)
            {
                Console.WriteLine("Blockchain is empty.");
                return false;
            }

            bool valid = true;

            for (int i = 1; i < chain.Count; i++)
            {
                var current = chain[i];
                var prev = chain[i - 1];

                string recalculatedHash =
                    _hashingService.ComputeHash(current);

                if (current.Hash != recalculatedHash)
                {
                    valid = false;

                    Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine(
                        $"Помилка в блоці #{current.Index}: " +
                        $"Хеш не відповідає даним блоку " +
                        $"(Data/Timestamp/Nonce змінено)."
                    );

                    Console.ResetColor();
                }

                if (!current.Hash.StartsWith(
                    new string('0', current.DifficultyAtMining)))
                {
                    valid = false;

                    Console.ForegroundColor = ConsoleColor.Yellow;

                    Console.WriteLine(
                        $"Помилка в блоці #{current.Index}: " +
                        $"Хеш не задовольняє складність (Difficulty)."
                    );

                    Console.ResetColor();
                }

                if (current.PrevHash != prev.Hash)
                {
                    valid = false;

                    Console.ForegroundColor = ConsoleColor.DarkRed;

                    Console.WriteLine(
                        $"Помилка в блоці #{current.Index}: " +
                        $"Розірвано ланцюг " +
                        $"(PreviousHash не збігається)."
                    );

                    Console.ResetColor();
                }
            }

            if (valid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Blockchain is valid.");
                Console.ResetColor();
            }

            return valid;
        }
    }
}