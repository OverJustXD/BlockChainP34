using BlockChainP34.Models;
using System;
using System.IO;
using System.Text.Json;

namespace BlockChainP34.Service
{
    public class ColdWalletService
    {
        private readonly CryptoService _cryptoService;

        public ColdWalletService()
        {
            _cryptoService = new CryptoService();
        }

        public string GenerateOfflineTransaction(
     string from,
     string to,
     decimal amount,
     decimal fee,
     string privateKey,
     int lockTime = 0)
        {
            var tx = new Transaction(from, to, amount, fee, lockTime);

            tx.Signature =
                _cryptoService.SignData(
                    tx.ToRawString(),
                    privateKey);

            string fileName =
                $"offline_tx_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            var json = JsonSerializer.Serialize(
                tx,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(fileName, json);

            return fileName;
        }
    }
}