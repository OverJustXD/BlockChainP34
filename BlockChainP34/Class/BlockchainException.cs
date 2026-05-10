using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Class
{
    public class BlockchainException : Exception
    {
        public BlockchainException(string message) : base(message) { }
    }
}
