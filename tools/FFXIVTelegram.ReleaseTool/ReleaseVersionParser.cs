using System.Globalization;

public static class ReleaseVersionParser
{
    public static string ParseStableTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var parts = tag.Split('.');
        if (parts.Length != 3 || parts[0].Length < 2 || parts[0][0] != 'v')
        {
            throw new ArgumentException("Stable tags must use the format vX.Y.Z.", nameof(tag));
        }

        var majorText = parts[0][1..];
        var minorText = parts[1];
        var patchText = parts[2];

        if (!int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(minorText, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(patchText, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            throw new ArgumentException("Stable tags must use numeric X.Y.Z components.", nameof(tag));
        }

        return $"{major}.{minor}.{patch}.0";
    }
}
