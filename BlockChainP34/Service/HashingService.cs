using BlockChainP34.Models;
using BlockChainP34.Service.P2P;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP34.Service
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            var transactionsData = string.Concat(block.Transactions.Select(tx => tx.ToRawString()).ToArray());
            string rawData = $"{block.Index}{block.MerkleRoot}{block.Timestamp}{transactionsData}{block.PrevHash}{block.Nonce}";
            return ComputeHash(rawData);
        }

        private string ComputeHash(string rawData)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public string BuildMerkleRoot(List<Transaction> transactions)
        {
            return MerkleUtilities.ComputeMerkleRoot(transactions);
        }
    }
}