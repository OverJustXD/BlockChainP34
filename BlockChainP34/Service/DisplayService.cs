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
                    Console.WriteLine($"Data: {block.Data}");
                    Console.WriteLine($"Hash: {block.Hash}");
                    Console.WriteLine($"PrevHash: {block.PrevHash}");
                    Console.WriteLine($"Author: {block.Author}");
                }
                else
                {
                    Console.WriteLine($"#{block.Index} | {block.Timestamp} | {block.Author}");
                }

                Console.WriteLine(new string('-', 50));
            }
        }
    }
}