using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlockChainP34.Models;

namespace BlockChainP34.Service
{
    public class TransactionService
    {
        public TransactionService() { }

        public Transaction CreateTransaction(string from, string to, decimal amount)
        {
            var tx = new Transaction(from, to, amount);
            var validation = ValidateTransaction(tx);
            if (!validation.IsValid)
            {
                throw new Exception($"Invalid transaction: {validation.error}");
            }
            return tx;
        }

        public (bool IsValid, string error) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null) return (false, "Transaction is null");
            if (string.IsNullOrEmpty(transaction.From)) return (false, "Sender address is required");
            if (string.IsNullOrEmpty(transaction.To)) return (false, "Recipient address is required");
            if (transaction.Amount <= 0) return (false, "Amount must be greater than zero");
            return (true, null);
        }
    }
}
