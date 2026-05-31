using System;
using System.Collections.Generic;

namespace BlockChainP34.Models
{
    public class NetworkPassport
    {
        public string StudentId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }

        public string GenesisBlockHash { get; set; } = string.Empty;

        public int ChainLength { get; set; }
        public int CompromisedBlockIndex { get; set; } = -1;
        public string CompromisedBlockHash { get; set; } = string.Empty;

        public AuditReport AuditResult { get; set; } = new();

        public string PassportSignature { get; set; } = string.Empty;
    }

    public class AuditReport
    {
        public bool IsChainValid { get; set; }
        public List<int> CompromisedBlockIndexes { get; set; } = new();
        public Dictionary<int, List<string>> ViolationDetails { get; set; } = new();
    }
}