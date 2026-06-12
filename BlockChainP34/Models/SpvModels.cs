using System.Collections.Generic;

namespace BlockChainP34.Service.P2P
{
    public class MerkleProofStep
    {
        public string SiblingHash { get; set; }
        public bool IsLeftSibling { get; set; }
    }

    public class SpvProofResponse
    {
        public string TxId { get; set; }
        public string TxHash { get; set; }
        public string ExpectedRoot { get; set; }
        public bool Included { get; set; }
        public List<MerkleProofStep> Proof { get; set; } = new();
    }
}