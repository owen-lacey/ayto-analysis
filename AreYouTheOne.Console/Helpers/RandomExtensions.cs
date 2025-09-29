namespace AreYouTheOne.Console.Helpers;

public static class RandomExtensions
{
    public static List<int> ListOfNumbers(this Random random, int max)
    {
        // Create a list of all possible numbers in the range
        var allNumbers = Enumerable.Range(0, max).ToList();

        // Use a Fisher-Yates shuffle to randomize the order
        var rng = random;
        var n = allNumbers.Count;
        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (allNumbers[k], allNumbers[n]) = (allNumbers[n], allNumbers[k]);
        }

        return allNumbers;
    }
    public static T[] Shuffle<T>(this T[] list, Random random)
    {
        var rng = random;
        var n = list.Length;
        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }

        return list;
    }
}