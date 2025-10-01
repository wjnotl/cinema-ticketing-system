namespace Supershow.Services;

public class GeneratorService
{
    public static string RandomString(int length, string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz")
    {
        string tmpStr = "";
        Random r = new();

        for (int i = 0; i < length; i++)
        {
            tmpStr += chars[r.Next(chars.Length)];
        }

        return tmpStr;
    }

    public static int RandomInteger(int min, int max)
    {
        return new Random().Next(min, max);
    }

    public static void Shuffle<T>(List<T> list)
    {
        Random r = new();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = r.Next(i + 1); // random integer from 0 to count
            (list[i], list[j]) = (list[j], list[i]); // swapping randomly
        }
    }
}