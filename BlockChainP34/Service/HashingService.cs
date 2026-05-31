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
            if (transactions == null || transactions.Count == 0)
                return string.Empty;

            var hashAllTransactions = transactions.Select(tx => ComputeHash(tx.ToRawString())).ToList();

            while (hashAllTransactions.Count > 1)
            {
                var tempList = new List<string>();
                for (int i = 0; i < hashAllTransactions.Count; i += 2)
                {
                    if (i + 1 < hashAllTransactions.Count)
                    {
                        string combinedHash = hashAllTransactions[i] + hashAllTransactions[i + 1];
                        tempList.Add(ComputeHash(combinedHash));
                    }
                    else
                    {
                        tempList.Add(hashAllTransactions[i]);
                    }
                }
                hashAllTransactions = tempList;
            }
            return hashAllTransactions[0];
        }
    }
}
