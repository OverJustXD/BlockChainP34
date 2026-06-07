using System;
using System.Collections.Generic;
using System.Linq;
using BlockChainP34.Models;

namespace BlockChainP34.Service
{
    public class DisplayService
    {
        public bool Verbose { get; set; } = false;

        public decimal NetworkBaseFee { get; set; } = 1.0m;

        public void PrintChain(List<Block> chain)
        {
            DisplayBlockChain(chain);
        }

        public void DisplayBlockChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine(new string('-', 60));

                if (Verbose)
                {
                    Console.WriteLine($"Index: {block.Index}");
                    Console.WriteLine($"Timestamp: {block.Timestamp}");
                    Console.WriteLine($"Hash: {block.Hash}");
                    Console.WriteLine($"PrevHash: {block.PrevHash}");
                    Console.WriteLine($"Nonce: {block.Nonce}");
                    Console.WriteLine($"Difficulty: {block.DifficultyAtMining}");
                    Console.WriteLine($"Mining Time: {block.MiningDurationSecond:F4} sec");

                    Console.WriteLine("\nTransactions:");

                    if (block.Transactions != null && block.Transactions.Count > 0)
                    {
                        foreach (var tx in block.Transactions)
                        {
                            DisplayTransaction(tx);
                        }

                        DisplayBlockEconomy(block);
                    }
                    else
                    {
                        Console.WriteLine("  No transactions");
                    }
                }
                else
                {
                    Console.WriteLine($"#{block.Index} | {block.Timestamp} | TX: {block.Transactions?.Count ?? 0}");
                }
            }
        }

        private void DisplayTransaction(Transaction tx)
        {
            if (tx.From == "SYSTEM")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [REWARD] -> {tx.To} : {tx.Amount}");
                Console.ResetColor();
                return;
            }

            decimal tip = tx.Fee - NetworkBaseFee;
            if (tip < 0) tip = 0;

            Console.WriteLine(
                $"  [{tx.Id}] {Short(tx.From)} -> {Short(tx.To)} | " +
                $"Amount: {tx.Amount} | Fee: {tx.Fee} | Tip: {tip} | Burn: {NetworkBaseFee}"
            );
        }

        private void DisplayBlockEconomy(Block block)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  [BLOCK ECONOMY]");

            Console.WriteLine($"  Burned: {block.BurnedFees:F2}");
            Console.WriteLine($"  Tips: {block.TipFees:F2}");

            Console.ResetColor();
        }

        private string Short(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "null";

            return value.Length <= 6 ? value : value.Substring(0, 6) + "...";
        }
    }
}