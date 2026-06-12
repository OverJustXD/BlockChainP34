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
        public void DisplayTransactionFull(Transaction tx, Block block)
        {
            Console.WriteLine("\n====================================================");
            Console.WriteLine("            TRANSACTION DETAILS (SEARCH RESULT)     ");
            Console.WriteLine("====================================================");

            Console.WriteLine($"TX ID      : {tx.Id}");
            Console.WriteLine($"From       : {tx.From}");
            Console.WriteLine($"To         : {tx.To}");
            Console.WriteLine($"Amount     : {tx.Amount}");
            Console.WriteLine($"Fee        : {tx.Fee}");
            Console.WriteLine($"Timestamp  : {tx.TimeStamp}");

            Console.WriteLine("\n--- BLOCK INFO ---");
            Console.WriteLine($"Block Index: {block.Index}");
            Console.WriteLine($"Block Hash : {block.Hash}");
            Console.WriteLine($"Prev Hash  : {block.PrevHash}");
            Console.WriteLine($"Tx count   : {block.Transactions.Count}");

            Console.WriteLine("====================================================\n");
        }

        public void DisplayBlockChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"BLOCK #{block.Index} | Hash: {block.Hash.Substring(0, 10)}...");

                if (block.Transactions != null && block.Transactions.Count > 0)
                {
                    foreach (var tx in block.Transactions)
                    {
                        Console.WriteLine($"  -> TX ID: {tx.Id}");
                        Console.WriteLine($"     Amount: {tx.Amount} | From: {Short(tx.From)} | To: {Short(tx.To)}");
                    }
                }
                else
                {
                    Console.WriteLine("  No transactions in this block.");
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