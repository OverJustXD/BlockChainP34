using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Transaction> Transactions { get; set; }
        public string Hash { get; set; }
        public string PrevHash { get; set; }
        public int Nonce { get; set; }
        //public string Author { get; set; }

        public int DifficultyAtMining { get; set; }
        public double MiningDurationSecond { get; set; } = 0;

        public Block(int index, DateTime timestamp, List<Transaction> transactions, string prevHash, string author)
        {
            Index = index;
            Timestamp = timestamp;
            Transactions = transactions;
            PrevHash = prevHash;
            Hash = "";
           // Author = author;
            Nonce = 0;
        }

        public Block() { }
    }
}