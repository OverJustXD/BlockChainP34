using BlockChainP34.Class;
using BlockChainP34.Models;
using BlockChainP34.Service;
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;
var displayService = new DisplayService();
var blockChainService = new BlockChainService(1);

var walletAlice = new Wallet(new CryptoService());
var walletBob = new Wallet(new CryptoService());
var analyzer = new BlockchainAnalyzer();

var attackService =
    new AttackSimulationService(analyzer);

//attackService.RunAllAttacks();

try
{
    Console.WriteLine("\nMINING PHASE");

    for (int i = 0; i < 5; i++)
    {
        blockChainService.MinePendingTransactions(
            walletAlice.PublicKey
        );
    }

    blockChainService.RebuildState();

    Console.WriteLine("\nTRANSACTION PHASE");

    var tx = TransactionService.CreateTransaction(
        walletAlice.PublicKey,
        walletBob.PublicKey,
        10,
        walletAlice.PrivateKey,
        2m
    );

    blockChainService.AddTransactionToMempool(tx);

    blockChainService.MinePendingTransactions(
        walletAlice.PublicKey
    );

    Console.WriteLine("Transaction included in block successfully!");

    displayService.Verbose = true;
    displayService.DisplayBlockChain(blockChainService.Chain);

    Console.WriteLine($"\nTotal Supply: {blockChainService.GetTotalSupply()}");

    blockChainService.RebuildState();

    Console.WriteLine("\nFINAL BALANCES");

    foreach (var wallet in blockChainService.Balances)
    {
        Console.WriteLine($"{wallet.Key.Substring(0, 10)}... : {wallet.Value}");
    }
}
catch (BlockchainException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\nTRANSACTION FAILED");
    Console.WriteLine(ex.Message);
    Console.ResetColor();

    Console.WriteLine("\nHint: wallet needs mining rewards before spending.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.DarkRed;
    Console.WriteLine("\nUNEXPECTED ERROR");
    Console.WriteLine(ex.Message);
    Console.ResetColor();
}


//do
//{
//    Console.WriteLine("Enter missing Difficulty (positive integer): ");
//    var input = Console.ReadLine();
//    if (!int.TryParse(input, out Difficulty) || Difficulty <= 0)
//    {
//        Console.WriteLine("Invalid input. Please enter a positive integer.");
//    }
//} while (Difficulty <= 0);

//for (int i = 0; i < 10; i++)
//{

//    blockChainService.AddBlock("First Block", "Alex");
//  blockChainService.AddBlock("Second Block", "Deny");
//blockChainService.AddBlock("Third Block", "Timur");
// blockChainService.AddBlock("Fourth Block", "Lisa");
//displayService.DisplayBlockChain(blockChainService.Chain);
//Console.WriteLine("Difficulty: " + blockChainService.Difficulty);
//}

