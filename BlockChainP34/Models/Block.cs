namespace BlockChainP34.Models
{
    public class Block
    {
        public int Index { get; set; }

        public string MerkleRoot { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Transaction> Transactions { get; set; }
        public string Hash { get; set; }
        public string PrevHash { get; set; }
        public int Nonce { get; set; }
        public int DifficultyAtMining { get; set; }
        public double MiningDurationSecond { get; set; } = 0;

        public decimal BurnedFees { get; set; }
        public decimal TipFees { get; set; }

        public Block(int index, DateTime timestamp, List<Transaction> transactions, string prevHash, string author)
        {
            Index = index;
            Timestamp = timestamp;
            Transactions = transactions;
            PrevHash = prevHash;
            Hash = "";
            Nonce = 0;
        }

        public Block() { }
    }
}