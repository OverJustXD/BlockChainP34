using BlockChainP34.Models;

namespace BlockChainP34.Service
{
    public static class TransactionService
    {
        private static readonly CryptoService cryptoService;

        static TransactionService()
        {
            cryptoService = new CryptoService();
        }

        public static Transaction CreateTransaction(
    string from,
    string to,
    decimal amount,
    string privateKey,
    decimal fee = 0)
        {
            var tx = new Transaction(from, to, amount, fee);

            SignTransaction(tx, privateKey);

            var validation = ValidateTransaction(tx);
            if (!validation.IsValid)
            {
                throw new Exception($"Invalid transaction: {validation.error}");
            }

            return tx;
        }

        public static (bool IsValid, string error) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null)
                return (false, "Transaction is null");

            if (string.IsNullOrWhiteSpace(transaction.From))
                return (false, "Sender address is required");

            if (string.IsNullOrWhiteSpace(transaction.To))
                return (false, "Recipient address is required");

            if (transaction.Amount <= 0)
                return (false, "Amount must be greater than zero");

            if (transaction.Fee < 0)
                return (false, "Transaction fee cannot be negative");

            if (transaction.Signature == null || transaction.Signature.Length == 0)
                return (false, "Signature is required");

            var raw = transaction.ToRawString();

            if (!cryptoService.VerifySignature(raw, transaction.Signature, transaction.From))
                return (false, "Invalid signature");

            return (true, null);
        }

        public static void SignTransaction(Transaction transaction, string privateKey)
        {
            var signature = cryptoService.SignData(transaction.ToRawString(), privateKey);
            transaction.Signature = signature;
        }
    }
}