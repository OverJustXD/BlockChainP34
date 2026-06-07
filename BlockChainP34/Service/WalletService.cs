using BlockChainP34.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlockChainP34.Service
{
    public static class WalletService
    {
        public class EncryptedWalletDto
        {
            public string PublicKey { get; set; }
            public string EncryptedPrivateKey { get; set; }
            public string Iv { get; set; }
        }

        public static void SaveWallet(string publicKey, string privateKey, string password, string filePath)
        {
            byte[] key = DeriveKey(password);
            byte[] iv = new byte[16];
            RandomNumberGenerator.Fill(iv);

            byte[] encryptedPrivateBytes;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(privateKey);
                    }
                    encryptedPrivateBytes = ms.ToArray();
                }
            }

            var dto = new EncryptedWalletDto
            {
                PublicKey = publicKey,
                EncryptedPrivateKey = Convert.ToBase64String(encryptedPrivateBytes),
                Iv = Convert.ToBase64String(iv)
            };

            string jsonResult = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, jsonResult);
        }

        public static (string publicKey, string privateKey) LoadWallet(string password, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл гаманця не знайдено.");

            string json = File.ReadAllText(filePath);
            var dto = JsonSerializer.Deserialize<EncryptedWalletDto>(json);

            if (dto == null || string.IsNullOrEmpty(dto.PublicKey) || string.IsNullOrEmpty(dto.EncryptedPrivateKey) || string.IsNullOrEmpty(dto.Iv))
                throw new CryptographicException("Невалідний формат файлу гаманця.");

            byte[] key = DeriveKey(password);
            byte[] iv = Convert.FromBase64String(dto.Iv);
            byte[] encryptedPrivateBytes = Convert.FromBase64String(dto.EncryptedPrivateKey);
            string decryptedPrivateKey = string.Empty;

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(encryptedPrivateBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    decryptedPrivateKey = sr.ReadToEnd();
                }
            }

            return (dto.PublicKey, decryptedPrivateKey);
        }

        private static byte[] DeriveKey(string password)
        {
            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            using (var rfc2898 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
            {
                return rfc2898.GetBytes(32);
            }
        }
    }
}