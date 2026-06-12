using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

            var raw = string.Join("|", new[]
            {
                tx.Id ?? string.Empty,
                tx.From ?? string.Empty,
                tx.To ?? string.Empty,
                tx.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                tx.Fee.ToString(System.Globalization.CultureInfo.InvariantCulture),
                tx.TimeStamp.ToUniversalTime().ToString("O"),
                tx.LockTime.ToString()
            });

            return HashString(raw);
        }

        public static string ComputeMerkleRoot(IReadOnlyList<Transaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
                return HashString(string.Empty);

            var level = transactions.Select(ComputeTransactionHash).ToList();

            while (level.Count > 1)
            {
                if (level.Count % 2 == 1)
                    level.Add(level[^1]);

                var next = new List<string>(level.Count / 2);

                for (int i = 0; i < level.Count; i += 2)
                    next.Add(HashPair(level[i], level[i + 1]));

                level = next;
            }

            return level[0];
        }

        public static List<MerkleProofStep> BuildMerkleProof(IReadOnlyList<Transaction> transactions, string txId)
        {
            var proof = new List<MerkleProofStep>();

            if (transactions == null || transactions.Count == 0 || string.IsNullOrWhiteSpace(txId))
                return proof;

            var txIndex = -1;
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

            var currentLevel = transactions.Select(ComputeTransactionHash).ToList();
            int index = txIndex;

            while (currentLevel.Count > 1)
            {
                if (currentLevel.Count % 2 == 1)
                    currentLevel.Add(currentLevel[^1]);

                int siblingIndex = (index % 2 == 0) ? index + 1 : index - 1;

                proof.Add(new MerkleProofStep
                {
                    SiblingHash = currentLevel[siblingIndex],
                    IsLeftSibling = siblingIndex < index
                });

                var nextLevel = new List<string>(currentLevel.Count / 2);

                for (int i = 0; i < currentLevel.Count; i += 2)
                    nextLevel.Add(HashPair(currentLevel[i], currentLevel[i + 1]));

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

            if (proof != null)
            {
                foreach (var step in proof)
                {
                    if (step == null || string.IsNullOrWhiteSpace(step.SiblingHash))
                        return false;

                    current = step.IsLeftSibling
                        ? HashPair(step.SiblingHash, current)
                        : HashPair(current, step.SiblingHash);
                }
            }

            return string.Equals(current, expectedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashPair(string left, string right)
        {
            return HashString($"{left}|{right}");
        }
    }
}