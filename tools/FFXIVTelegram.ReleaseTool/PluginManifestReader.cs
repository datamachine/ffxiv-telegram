using System.Text.Json;

public static class PluginManifestReader
{
    public static PluginManifestModel ReadFromJson(string manifestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);

        using var document = JsonDocument.Parse(manifestJson);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Plugin manifest JSON must be an object.", nameof(manifestJson));
        }

        return new PluginManifestModel
        {
            Author = GetRequiredString(root, "Author"),
            Name = GetRequiredString(root, "Name"),
            InternalName = GetRequiredString(root, "InternalName"),
            Description = GetRequiredString(root, "Description"),
            Punchline = GetRequiredString(root, "Punchline"),
            ApplicableVersion = GetRequiredString(root, "ApplicableVersion"),
            RepoUrl = GetRequiredString(root, "RepoUrl"),
            DalamudApiLevel = GetRequiredInt32(root, "DalamudApiLevel"),
            LoadPriority = GetRequiredInt32(root, "LoadPriority"),
            Tags = GetRequiredTags(root),
        };
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            throw new ArgumentException($"Plugin manifest is missing required property '{propertyName}'.", propertyName);
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Plugin manifest property '{propertyName}' must be a string.", propertyName);
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Plugin manifest property '{propertyName}' must be a non-empty string.", propertyName);
        }

        return value;
    }

    private static int GetRequiredInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            throw new ArgumentException($"Plugin manifest is missing required property '{propertyName}'.", propertyName);
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new ArgumentException($"Plugin manifest property '{propertyName}' must be an integer.", propertyName);
        }

        return value;
    }

    private static IReadOnlyList<string> GetRequiredTags(JsonElement root)
    {
        const string propertyName = "Tags";

        if (!root.TryGetProperty(propertyName, out var property))
        {
            throw new ArgumentException($"Plugin manifest is missing required property '{propertyName}'.", propertyName);
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Plugin manifest property 'Tags' must be an array.", propertyName);
        }

        var tags = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Plugin manifest property 'Tags' must contain only strings.", propertyName);
            }

            var value = element.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Plugin manifest property 'Tags' must not contain empty values.", propertyName);
            }

            tags.Add(value);
        }

        if (tags.Count == 0)
        {
            throw new ArgumentException("Plugin manifest property 'Tags' must include at least one tag.", propertyName);
        }

        return tags;
    }
}
