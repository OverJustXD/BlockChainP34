using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Service
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            var transactionsData = string.Concat(block.Transactions.Select(tx=>tx.ToRawString()).ToArray());
            string rawData = $"{block.Index}{block.Timestamp}{transactionsData}{block.PrevHash}{block.Nonce}";
            return ComputeHash(rawData);
        }

        private string ComputeHash(string rawData)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(rawData);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
