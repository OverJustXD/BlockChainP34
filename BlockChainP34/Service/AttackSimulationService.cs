using BlockChainP34.Models;
using BlockChainP34.Service;
using System;

namespace BlockChainP34.Class
{
    public class AttackSimulationService
    {
        private readonly BlockchainAnalyzer _analyzer;

        public AttackSimulationService(BlockchainAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public void RunAllAttacks()
        {
            SimulateDataAttack();
            SimulateFakeHashAttack();
            SimulatePreviousHashAttack();
        }

        private void SimulateDataAttack()
        {
            var blockchain = CreateFreshBlockchain();

            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("ATTACK 1: DATA MODIFY");

            Console.ResetColor();

            blockchain.Chain[2]
                .Transactions[0]
                .Amount = 9999;

            Console.WriteLine(
                "\n[SIMULATION] " +
                "Transactions/Data у блоці #2 змінено."
            );

            Console.WriteLine("\n[ANALYZER RESULT]");

            _analyzer.Analyze(blockchain.Chain);
        }

        private void SimulateFakeHashAttack()
        {
            var blockchain = CreateFreshBlockchain();

            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("ATTACK 2: FAKE HASH");

            Console.ResetColor();

            blockchain.Chain[3].Hash = "12345";

            Console.WriteLine(
                "\n[SIMULATION] " +
                "Hash блоку #3 змінено на '12345'."
            );

            Console.WriteLine("\n[ANALYZER RESULT]");

            _analyzer.Analyze(blockchain.Chain);
        }


        private void SimulatePreviousHashAttack()
        {
            var blockchain = CreateFreshBlockchain();

            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("ATTACK 3: BROKEN CHAIN");

            Console.ResetColor();

            blockchain.Chain[4].PrevHash = "FAKE_HASH";

            Console.WriteLine(
                "\n[SIMULATION] " +
                "PreviousHash блоку #4 змінено."
            );

            Console.WriteLine("\n[ANALYZER RESULT]");

            _analyzer.Analyze(blockchain.Chain);
        }

        private BlockChainService CreateFreshBlockchain()
        {
            var blockchain = new BlockChainService(1);

            var wallet = new Wallet(new CryptoService());

            for (int i = 0; i < 5; i++)
            {
                blockchain.MinePendingTransactions(
                    wallet.PublicKey
                );
            }

            return blockchain;
        }
    }
}