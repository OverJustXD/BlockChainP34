using System.Collections.Generic;

namespace BlockChainP34.Models
{
    public class MerkleProofStep
    {
        public string SiblingHash { get; set; } = string.Empty;
        public bool IsLeftSibling { get; set; }
    }

    public class SpvProofResponse
    {
        public string TxId { get; set; } = string.Empty;
        public string TxHash { get; set; } = string.Empty;
        public string ExpectedRoot { get; set; } = string.Empty;
        public bool Included { get; set; }
        public List<MerkleProofStep> Proof { get; set; } = new();
    }

    public class P2PMessage
    {
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }
}