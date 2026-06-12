namespace BlockChainP34.Models
{
    public class Block
    {
        public int Index { get; set; }
        public string MerkleRoot { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<Transaction> Transactions { get; set; } = new();
        public string Hash { get; set; } = string.Empty;
        public string PrevHash { get; set; } = string.Empty;
        public int Nonce { get; set; }
        public int DifficultyAtMining { get; set; }
        public double MiningDurationSecond { get; set; } = 0;
        public decimal BurnedFees { get; set; }
        public decimal TipFees { get; set; }

        public Block(int index, DateTime timestamp, List<Transaction> transactions, string prevHash, string author)
        {
            Index = index;
            Timestamp = timestamp;
            Transactions = transactions ?? new List<Transaction>();
            PrevHash = prevHash ?? string.Empty;
            Hash = string.Empty;
            Nonce = 0;
            MerkleRoot = string.Empty;
        }

        public Block() { }
    }
}