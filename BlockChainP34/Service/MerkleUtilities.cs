using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP34.Service.P2P
{
    public static class MerkleUtilities
    {
        public static string HashString(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string ComputeTransactionHash(Transaction tx)
        {
            if (tx == null) return HashString(string.Empty);

            return HashString(tx.ToRawString());
        }

        public static string ComputeMerkleRoot(IReadOnlyList<Transaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
                return HashString(string.Empty);

            var level = new List<string>();
            foreach (var tx in transactions)
                level.Add(ComputeTransactionHash(tx));

            if (level.Count == 1)
                return level[0];

            while (level.Count > 1)
            {
                if (level.Count % 2 == 1)
                    level.Add(level[^1]);

                var next = new List<string>();
                for (int i = 0; i < level.Count; i += 2)
                    next.Add(HashPair(level[i], level[i + 1]));
                level = next;
            }
            return level[0];
        }

        public static void TestMerkleForBlock(Block block)
        {
            Console.WriteLine("\n=== MERKLE TEST ===");
            Console.WriteLine($"Block #{block.Index}, Transactions: {block.Transactions.Count}");

            string computedRoot = ComputeMerkleRoot(block.Transactions);
            Console.WriteLine($"Computed Root: {computedRoot}");
            Console.WriteLine($"Block Root:     {block.MerkleRoot}");
            Console.WriteLine($"Match: {computedRoot == block.MerkleRoot}");

            if (computedRoot != block.MerkleRoot)
            {
                Console.WriteLine("\nTransaction Hashes:");
                foreach (var tx in block.Transactions)
                {
                    Console.WriteLine($"  {ComputeTransactionHash(tx)} <- {tx.Id}");
                }
            }
        }

        public static List<MerkleProofStep> BuildMerkleProof(IReadOnlyList<Transaction> transactions, string txId)
        {
            var proof = new List<MerkleProofStep>();

            if (transactions == null || transactions.Count == 0 || string.IsNullOrWhiteSpace(txId))
                return proof;

            int txIndex = -1;
            for (int i = 0; i < transactions.Count; i++)
            {
                if (string.Equals(transactions[i]?.Id, txId, StringComparison.OrdinalIgnoreCase))
                {
                    txIndex = i;
                    break;
                }
            }

            if (txIndex < 0)
                return proof;

            var currentLevel = new List<string>();
            foreach (var tx in transactions)
                currentLevel.Add(ComputeTransactionHash(tx));

            int index = txIndex;

            while (currentLevel.Count > 1)
            {
                if (currentLevel.Count % 2 == 1)
                    currentLevel.Add(currentLevel[^1]);

                int siblingIndex = (index % 2 == 0) ? index + 1 : index - 1;

                proof.Add(new MerkleProofStep
                {
                    SiblingHash = currentLevel[siblingIndex],
                    IsLeftSibling = (siblingIndex < index)
                });

                var nextLevel = new List<string>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    nextLevel.Add(HashString(currentLevel[i] + currentLevel[i + 1]));
                }

                index /= 2;
                currentLevel = nextLevel;
            }

            return proof;
        }

        public static bool VerifyMerkleProof(string txHash, List<MerkleProofStep> proof, string expectedRoot)
        {
            if (string.IsNullOrWhiteSpace(txHash) || string.IsNullOrWhiteSpace(expectedRoot))
                return false;

            string current = txHash;

            if (proof == null || proof.Count == 0)
            {
                return string.Equals(txHash, expectedRoot, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var step in proof)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.SiblingHash))
                    return false;

                if (step.IsLeftSibling)
                {
                    current = HashString(step.SiblingHash + current);
                }
                else
                {
                    current = HashString(current + step.SiblingHash);
                }
            }

            return string.Equals(current, expectedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashPair(string left, string right)
        {
            return HashString(left + right);
        }

        public static void DebugMerkleProof(IReadOnlyList<Transaction> transactions, string txId)
        {
            Console.WriteLine("\n=== MERKLE PROOF DEBUG ===");

            var tx = transactions.FirstOrDefault(t => t.Id == txId);
            if (tx == null)
            {
                Console.WriteLine("Transaction not found!");
                return;
            }

            string txHash = ComputeTransactionHash(tx);
            Console.WriteLine($"Tx Hash: {txHash}");

            var proof = BuildMerkleProof(transactions, txId);

            string current = txHash;
            Console.WriteLine($"\nStart: {current}");

            for (int i = 0; i < proof.Count; i++)
            {
                var step = proof[i];
                string before = current;

                if (step.IsLeftSibling)
                {
                    current = HashString(step.SiblingHash + current);
                    Console.WriteLine($"[{i + 1}] {step.SiblingHash} + {before} = {current}");
                }
                else
                {
                    current = HashString(current + step.SiblingHash);
                    Console.WriteLine($"[{i + 1}] {before} + {step.SiblingHash} = {current}");
                }
            }

            string originalRoot = ComputeMerkleRoot(transactions);
            Console.WriteLine($"\nComputed Root: {current}");
            Console.WriteLine($"Original Root: {originalRoot}");
            Console.WriteLine($"Match: {current == originalRoot}");
        }
    }
}