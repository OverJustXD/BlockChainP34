using BlockChainP34.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainP34.Models
{
    public class Wallet
    {
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public Wallet(CryptoService cryptoService)
        {
            var keyPair = cryptoService.GenerateKeyPair();
            PublicKey = keyPair.publicKey;
            PrivateKey = keyPair.privateKey;
        }
    }
}
