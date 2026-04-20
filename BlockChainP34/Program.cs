using BlockChainP34.Service;
using BlockChainP34.Models;

var displayService = new DisplayService();


var Difficulty = 0;
do
{
    Console.WriteLine("Enter missing Difficulty (positive integer): ");
    var input = Console.ReadLine();
    if (!int.TryParse(input, out Difficulty) || Difficulty <= 0)
    {
        Console.WriteLine("Invalid input. Please enter a positive integer.");
    }
} while (Difficulty <= 0);

var blockChainService = new BlockChainService(Difficulty);

for (int i = 0; i < 10; i++)
{
    blockChainService.AddBlock("First Block", "Alex");
    blockChainService.AddBlock("Second Block", "Deny");
    blockChainService.AddBlock("Third Block", "Timur");
    blockChainService.AddBlock("Fourth Block", "Lisa");
    displayService.DisplayBlockChain(blockChainService.Chain);
    Console.WriteLine("Difficulty: " + blockChainService.Difficulty);
}

