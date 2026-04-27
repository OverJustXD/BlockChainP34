using System;
using System.Collections.Generic;
using System.Linq;
using BlockChainP34.Models;
using BlockChainP34.Service;

namespace BlockChainP34.Analytics
{
    public class BlockchainExplorer
    {
        private readonly BlockChainService _blockchain;

        public BlockchainExplorer(BlockChainService blockchain)
        {
            _blockchain = blockchain;
        }

        public decimal GetTotalVolume()
        {
            return _blockchain.Chain
                .Where(b => b.Transactions != null)
                .SelectMany(b => b.Transactions)
                .Sum(tx => tx.Amount);
        }

        public Transaction? GetLargestTransaction()
        {
            return _blockchain.Chain
                .Where(b => b.Transactions != null && b.Transactions.Any())
                .SelectMany(b => b.Transactions)
                .OrderByDescending(tx => tx.Amount)
                .FirstOrDefault();
        }

        public List<Transaction> GetAddressHistory(string address)
        {
            return _blockchain.Chain
                .Where(b => b.Transactions != null)
                .SelectMany(b => b.Transactions)
                .Where(tx => tx.From == address || tx.To == address)
                .ToList();
        }

        public (Block? block, Transaction? tx) FindTransactionLocation(string txId)
        {
            foreach (var block in _blockchain.Chain)
            {
                if (block.Transactions == null) continue;

                var tx = block.Transactions
                    .FirstOrDefault(t => t.Id == txId);

                if (tx != null)
                {
                    return (block, tx);
                }
            }

            return (null, null);
        }
    }
}