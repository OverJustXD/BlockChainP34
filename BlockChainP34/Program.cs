using BlockChainP34.Analytics;
using BlockChainP34.Models;
using BlockChainP34.Service;

var displayService = new DisplayService();
var blockChainService = new BlockChainService(1);
var transactionService = new TransactionService();
var explorer = new BlockchainExplorer(blockChainService);


blockChainService.AddBlock(new List<Transaction>());

blockChainService.AddBlock(new List<Transaction>
{
    transactionService.CreateTransaction("Alice", "Bob", 10)
});

blockChainService.AddBlock(new List<Transaction>
{
    transactionService.CreateTransaction("Alice", "Bob", 10)
});

blockChainService.AddBlock(new List<Transaction>());

blockChainService.AddBlock(new List<Transaction>
{
    transactionService.CreateTransaction("Alice", "Bob", 10),
    transactionService.CreateTransaction("Alice", "Bob", 10)

});


Console.WriteLine($"Total Volume: {explorer.GetTotalVolume()}");

var largestTx = explorer.GetLargestTransaction();
Console.WriteLine($"Largest TX: {largestTx}");

var history = explorer.GetAddressHistory("Alice");
Console.WriteLine("Alice history:");
foreach (var tx in history)
{
    Console.WriteLine(tx);
}

if (largestTx != null)
{
    var (block, tx) = explorer.FindTransactionLocation(largestTx.Id);
    Console.WriteLine($"TX found in block #{block?.Index}");
}

displayService.Verbose = true;
displayService.DisplayBlockChain(blockChainService.Chain);

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

