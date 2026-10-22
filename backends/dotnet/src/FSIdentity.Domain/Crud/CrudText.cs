using System.Text;

namespace FSIdentity.Domain.Crud;

internal enum TextCase { Keep, Upper, Lower }

internal static class CrudText
{
    public static string? Normalize(string? value, TextCase textCase)
    {
        var trimmed = value?.Trim();
        return textCase switch
        {
            TextCase.Upper => trimmed?.ToUpperInvariant(),
            TextCase.Lower => trimmed?.ToLowerInvariant(),
            _ => trimmed,
        };
    }

    public static string Label(Type entity) => Words(entity.Name, firstUpper: true);

    public static string PluralWords(Type entity)
    {
        var words = Words(entity.Name, firstUpper: false);
        return words.EndsWith('y') && words.Length > 1 && !"aeiou".Contains(words[^2])
            ? words[..^1] + "ies"
            : words + "s";
    }

    public static string PropertyWords(string property) => Words(property, firstUpper: false);

    private static string Words(string name, bool firstUpper)
    {
        var text = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c))
                text.Append(' ');
            text.Append(i == 0 && firstUpper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
        }
        return text.ToString();
    }
}
