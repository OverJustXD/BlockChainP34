using BlockChainP34.Service;
using System;

namespace BlockChainP34.Models
{
    public class Wallet
    {
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }

        public Wallet()
        {
        }

        public Wallet(CryptoService cryptoService)
        {
            if (cryptoService == null)
                throw new ArgumentNullException(nameof(cryptoService));

            var keyPair = cryptoService.GenerateKeyPair();
            PublicKey = keyPair.publicKey;
            PrivateKey = keyPair.privateKey;
        }

        public Wallet(string publicKey, string privateKey)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        }
    }
}