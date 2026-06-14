using BlockChainP34.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockChainP34.Service
{
    public class BlockChainExplorerService
    {
        private readonly BlockChainService _blockchain;

        public BlockChainExplorerService(BlockChainService blockchain)
        {
            _blockchain = blockchain ?? throw new ArgumentNullException(nameof(blockchain));
        }

        public Transaction FindTransactionById(string txId)
        {
            if (string.IsNullOrWhiteSpace(txId))
                return null;

            return _blockchain.Chain
                .SelectMany(b => b.Transactions)
                .Concat(_blockchain.PendingTransactions)
                .FirstOrDefault(tx => tx.Id == txId);
        }

        public Block FindBlockByTransactionId(string txId)
        {
            if (string.IsNullOrWhiteSpace(txId))
                return null;

            foreach (var block in _blockchain.Chain)
            {
                if (block.Transactions.Any(tx => tx.Id == txId))
                    return block;
            }

            return null;
        }

        public List<Transaction> GetTransactionHistory(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return new List<Transaction>();

            var confirmed = _blockchain.Chain
                .SelectMany(b => b.Transactions)
                .Where(tx => tx.From == address || tx.To == address);

            var pending = _blockchain.PendingTransactions
                .Where(tx => tx.From == address || tx.To == address);

            return confirmed
                .Concat(pending)
                .OrderByDescending(tx => tx.TimeStamp)
                .ToList();
        }

        public decimal GetTotalFeesEarned(string minerAddress)
        {
            if (string.IsNullOrWhiteSpace(minerAddress))
                return 0;

            return _blockchain.Chain
                .Where(b => b.MinerAddress == minerAddress)
                .Sum(b => b.TipFees);
        }
    }
}