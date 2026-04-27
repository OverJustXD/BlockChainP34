using System;
using System.Collections.Generic;
using BlockChainP34.Models;

namespace BlockChainP34.Service
{
    public class DisplayService
    {
        public bool Verbose { get; set; } = false;

        public void DisplayBlockChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                if (Verbose)
                {
                    Console.WriteLine($"Index: {block.Index}");
                    Console.WriteLine($"Timestamp: {block.Timestamp}");
                    Console.WriteLine($"Hash: {block.Hash}");
                    Console.WriteLine($"PrevHash: {block.PrevHash}");
                    Console.WriteLine($"Nonce: {block.Nonce}");
                    Console.WriteLine($"Difficulty: {block.DifficultyAtMining}");
                    Console.WriteLine($"Mining Time: {block.MiningDurationSecond:F4} sec");

                    Console.WriteLine("Transactions:");

                    if (block.Transactions != null && block.Transactions.Count > 0)
                    {
                        foreach (var tx in block.Transactions)
                        {
                            Console.WriteLine($"  [{tx.Id}] {tx.From} -> {tx.To} : {tx.Amount}");
                        }
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

                Console.WriteLine(new string('-', 60));
            }
        }
    }
}