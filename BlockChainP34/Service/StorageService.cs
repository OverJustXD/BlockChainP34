using BlockChainP34.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlockChainP34.Service
{
    public static class StorageService
    {
        public static NetworkPassport GenerateNetworkPassport(
            BlockChainService blockchain,
            string studentId,
            int compromisedIndex,
            string compromisedHash)
        {
            var audit = blockchain.RunFullAudit(blockchain.Chain);

            var passport = new NetworkPassport
            {
                StudentId = studentId,
                GeneratedAt = DateTime.UtcNow,

                GenesisBlockHash = blockchain.Chain.First().Hash,
                ChainLength = blockchain.Chain.Count,

                CompromisedBlockIndex = compromisedIndex,
                CompromisedBlockHash = compromisedHash,

                AuditResult = audit
            };

            passport.PassportSignature = ComputeSignature(passport);

            return passport;
        }

        public static void SavePassport(NetworkPassport passport, string path = "passport.json")
        {
            var json = JsonSerializer.Serialize(passport, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }

        private static string ComputeSignature(NetworkPassport p)
        {
            var raw =
                $"{p.StudentId}|" +
                $"{p.GeneratedAt:O}|" +
                $"{p.GenesisBlockHash}|" +
                $"{p.ChainLength}|" +
                $"{p.CompromisedBlockIndex}|" +
                $"{p.CompromisedBlockHash}|" +
                $"{p.AuditResult.IsChainValid}|" +
                $"{string.Join(",", p.AuditResult.CompromisedBlockIndexes)}";

            return Sha256(raw);
        }

        private static string Sha256(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}