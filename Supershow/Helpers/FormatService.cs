using System.Globalization;

namespace Supershow.Helpers;

public class FormatService
{
    public static string ToRMFormat(decimal price, bool AddCurrencySymbol = true, bool AddComma = true, bool reportFormat = false)
    {
        price = Math.Round(price, 2);

        bool isNegative = price < 0;
        if (reportFormat && isNegative)
            price = -price;

        string result = price.ToString(AddComma ? "N" : "G", CultureInfo.InvariantCulture);
        result = (AddCurrencySymbol ? "RM " : "") + result;

        return reportFormat && isNegative ? $"({result})" : result;
    }

    public static string ToPercentage(decimal amount, bool report = true, bool AddPercentSign = true, bool AddComma = true)
    {
        amount = Math.Round(amount * 100, 2);

        bool AddBracket = report && amount < 0;
        if (AddBracket)
        {
            amount = -amount;
        }

        string result = amount.ToString(AddComma ? "N" : "G", CultureInfo.InvariantCulture);
        result += AddPercentSign ? "%" : "";
        if (AddBracket)
        {
            result = $"({result})";
        }

        return result;
    }

    public static string ToDateTimeFormat(DateTime dateTime, string format = "yyyy-MM-dd")
    {
        return dateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string GetRelativeTime(DateTime past)
    {
        var now = DateTime.Now;
        var diff = now - past;

        if (diff.Days >= 365)
        {
            int years = diff.Days / 365;
            return $"{years} year{(years > 1 ? "s" : "")} ago";
        }
        if (diff.Days >= 30)
        {
            int months = diff.Days / 30;
            return $"{months} month{(months > 1 ? "s" : "")} ago";
        }
        if (diff.Days >= 7)
        {
            int weeks = diff.Days / 7;
            return $"{weeks} week{(weeks > 1 ? "s" : "")} ago";
        }
        if (diff.Days > 0)
        {
            return $"{diff.Days} day{(diff.Days > 1 ? "s" : "")} ago";
        }
        if (diff.Hours > 0)
        {
            return $"{diff.Hours} hour{(diff.Hours > 1 ? "s" : "")} ago";
        }
        if (diff.Minutes > 0)
        {
            return $"{diff.Minutes} minute{(diff.Minutes > 1 ? "s" : "")} ago";
        }
        if (diff.Seconds > 0)
        {
            return $"{diff.Seconds} second{(diff.Seconds > 1 ? "s" : "")} ago";
        }

        return "just now";
    }

    public static string ToDurationFormat(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (hours > 0 && minutes > 0)
            return $"{hours} hr {minutes} min";
        if (hours > 0)
            return $"{hours} hr";
        return $"{minutes} min";
    }
}