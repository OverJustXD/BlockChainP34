using System;

namespace BlockChainP34.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public byte[] Signature { get; set; }
        public decimal Amount { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public byte[] SenderPublicKey { get; set; } 
        public decimal Fee { get; set; }
        public int LockTime { get; set; } = 0;

        public Transaction(string from, string to, decimal amount, decimal fee, int lockTime = 0)
        {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            Fee = fee;
            LockTime = lockTime;
        }

        public string ToRawString()
        {
            return $"{From}{To}{Amount}{TimeStamp:O}{Fee}";
        }

        public override string ToString()
        {
            string shortId = Id != null && Id.Length > 8 ? Id.Substring(0, 8) + "..." : Id;
            string shortFrom = From != null && From.Length > 10 ? From.Substring(0, 10) + "..." : From;
            string shortTo = To != null && To.Length > 10 ? To.Substring(0, 10) + "..." : To;

            return $"Transaction ID: {shortId}, From: {shortFrom}, To: {shortTo}, Amount: {Amount} COIN, Fee: {Fee}, LockTime: {LockTime}, TimeStamp: {TimeStamp}";
        }
    }
}