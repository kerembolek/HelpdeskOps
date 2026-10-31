using System.Text.RegularExpressions;

namespace HelpdeskOps.Services.Suggestions;

// Removes what a pattern can recognize before ticket text leaves the server. Category and priority do not depend on
// these values. Names in free text cannot be recognized this way, so the privacy notice and the install guide say so.
public static partial class PersonalDataMask
{
    public static string Apply(string text)
    {
        text = EmailPattern().Replace(text, "[e-posta]");
        text = IbanPattern().Replace(text, "[iban]");
        text = IpAddressPattern().Replace(text, "[ip]");
        text = NationalIdPattern().Replace(text, "[tckn]");
        return PhonePattern().Replace(text, "[telefon]");
    }

    [GeneratedRegex(@"[\p{L}\p{N}._%+-]+@[\p{L}\p{N}-]+(\.[\p{L}\p{N}-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\bTR\s?\d{2}(\s?\d{4}){5}\s?\d{2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"\b\d{1,3}(\.\d{1,3}){3}\b")]
    private static partial Regex IpAddressPattern();

    // Turkish ID numbers have 11 digits and never start with zero
    [GeneratedRegex(@"\b[1-9]\d{10}\b")]
    private static partial Regex NationalIdPattern();

    // 0532 123 45 67, +90 (532) 123-45-67, 5321234567
    [GeneratedRegex(@"(?<![\d])(\+?90[\s-]?)?\(?0?[2-5]\d{2}\)?[\s-]?\d{3}[\s-]?\d{2}[\s-]?\d{2}(?![\d])")]
    private static partial Regex PhonePattern();
}
