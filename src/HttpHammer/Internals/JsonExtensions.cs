using System.Text.Json;
using System.Text.RegularExpressions;

namespace HttpHammer.Internals;

internal static partial class JsonExtensions
{
    [GeneratedRegex(@"\[(\d+)\]", RegexOptions.Compiled)]
    private static partial Regex ArrayIndexPattern { get; }

    [GeneratedRegex(@"\[(\d+):(\d+)\]", RegexOptions.Compiled)]
    private static partial Regex ArraySlicePattern { get; }

    [GeneratedRegex(@"\[\?\(@\.(.*?)(==|!=|>|<|>=|<=)(.*?)\)\]", RegexOptions.Compiled)]
    private static partial Regex FilterExpressionPattern { get; }

    public static bool TryExtractJsonValue(this JsonElement element, string jsonPath, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            value = element.ToString();
            return true;
        }

        try
        {
            if (jsonPath.StartsWith('$'))
            {
                jsonPath = jsonPath[1..];
                if (jsonPath.StartsWith('.'))
                {
                    jsonPath = jsonPath[1..];
                }

                if (string.IsNullOrWhiteSpace(jsonPath))
                {
                    value = element.ToString();
                    return true;
                }
            }

            if (jsonPath.Contains(".."))
            {
                var parts = jsonPath.Split([".."], 2, StringSplitOptions.None);
                var beforeRecursive = parts[0];
                var afterRecursive = parts[1];

                if (!string.IsNullOrWhiteSpace(beforeRecursive))
                {
                    if (!TryNavigatePath(element, beforeRecursive, out var currentElement))
                    {
                        return false;
                    }

                    var results = FindAllRecursively(currentElement, afterRecursive);
                    if (!results.Any())
                    {
                        return false;
                    }

                    value = results.First().ToString();
                    return true;
                }
                else
                {
                    var results = FindAllRecursively(element, afterRecursive);
                    if (!results.Any())
                    {
                        return false;
                    }

                    value = results.First().ToString();
                    return true;
                }
            }

            if (jsonPath.Contains("[?"))
            {
                var filterResult = ProcessPathWithFilters(element, jsonPath);
                if (filterResult.Success)
                {
                    value = filterResult.Value.ToString();
                    return true;
                }

                return false;
            }

            if (TryNavigatePath(element, jsonPath, out var resultElement))
            {
                value = resultElement.ToString();
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool EvaluateFilterCondition(JsonElement element, string propertyPath, string op, string valueStr)
    {
        if (!TryNavigatePath(element, propertyPath, out var property))
        {
            return false;
        }

        var isNumericProperty = decimal.TryParse(property.ToString(), out var propNum);
        var isNumericValue = decimal.TryParse(valueStr, out var valueNum);
        var bothNumeric = isNumericProperty && isNumericValue;
        var propValue = property.ToString();

        return op switch
        {
            "==" => propValue == valueStr,
            "!=" => propValue != valueStr,
            ">" => bothNumeric && propNum > valueNum,
            "<" => bothNumeric && propNum < valueNum,
            ">=" => bothNumeric && propNum >= valueNum,
            "<=" => bothNumeric && propNum <= valueNum,
            _ => false
        };
    }

    private static List<JsonElement> FindAllRecursively(JsonElement element, string path)
    {
        var results = new List<JsonElement>();
        if (TryNavigatePath(element, path, out var directResult))
        {
            results.Add(directResult);
            return results;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name == path.Split('.').FirstOrDefault())
                {
                    if (path.Contains('.'))
                    {
                        var remainingPath = string.Join(".", path.Split('.').Skip(1));
                        if (TryNavigatePath(property.Value, remainingPath, out var result))
                        {
                            results.Add(result);
                        }
                    }
                    else
                    {
                        results.Add(property.Value);
                    }
                }

                results.AddRange(FindAllRecursively(property.Value, path));
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            for (var i = 0; i < element.GetArrayLength(); i++)
            {
                results.AddRange(FindAllRecursively(element[i], path));
            }
        }

        return results;
    }

    private static (bool Success, JsonElement Value) ProcessPathWithFilters(JsonElement element, string path)
    {
        var filterMatchIndex = path.IndexOf("[?", StringComparison.InvariantCulture);
        var beforeFilter = path.Substring(0, filterMatchIndex);

        JsonElement arrayElement;
        if (string.IsNullOrEmpty(beforeFilter))
        {
            arrayElement = element;
        }
        else
        {
            if (!TryNavigatePath(element, beforeFilter, out arrayElement))
            {
                return (false, default);
            }
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return (false, default);
        }

        var match = FilterExpressionPattern.Match(path.Substring(filterMatchIndex));
        if (!match.Success)
        {
            return (false, default);
        }

        var propertyPath = match.Groups[1].Value;
        var op = match.Groups[2].Value;
        var valueStr = match.Groups[3].Value.Trim('"', '\'');

        for (var i = 0; i < arrayElement.GetArrayLength(); i++)
        {
            var item = arrayElement[i];
            if (EvaluateFilterCondition(item, propertyPath, op, valueStr))
            {
                // Check if there's a path after the filter
                var endOfFilterIndex = filterMatchIndex + match.Length;
                if (endOfFilterIndex < path.Length && path[endOfFilterIndex] == '.')
                {
                    var afterFilter = path.Substring(endOfFilterIndex + 1);
                    if (TryNavigatePath(item, afterFilter, out var nestedResult))
                    {
                        return (true, nestedResult);
                    }
                }
                else
                {
                    // If no path after filter, return the matched item
                    return (true, item);
                }
            }
        }

        return (false, default);
    }

    private static bool TryNavigatePath(JsonElement element, string path, out JsonElement result)
    {
        result = element;

        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var current = element;
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (part.Equals("*"))
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (current.EnumerateObject().Any())
                    {
                        current = current.EnumerateObject().First().Value;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    if (current.GetArrayLength() > 0)
                    {
                        current = current[0];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                continue;
            }

            if (ArraySlicePattern.IsMatch(part))
            {
                var sliceMatch = ArraySlicePattern.Match(part);
                var startStr = sliceMatch.Groups[1].Value;
                var endStr = sliceMatch.Groups[2].Value;

                if (!int.TryParse(startStr, out var start) || !int.TryParse(endStr, out var end))
                {
                    return false;
                }

                var propertyName = part.Split('[')[0];
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (current.ValueKind != JsonValueKind.Object ||
                        !current.TryGetProperty(propertyName, out var property))
                    {
                        return false;
                    }

                    current = property;
                }

                if (current.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var arrayLength = current.GetArrayLength();
                if (start < 0 || start >= arrayLength || end > arrayLength || end <= start)
                {
                    return false;
                }

                current = current[start];
                continue;
            }

            if (part.Contains('['))
            {
                var segments = part.Split('[');
                var propertyName = segments[0];

                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (current.ValueKind != JsonValueKind.Object ||
                        !current.TryGetProperty(propertyName, out var property))
                    {
                        return false;
                    }

                    current = property;
                }

                var indexMatches = ArrayIndexPattern.Matches(part);
                foreach (Match indexMatch in indexMatches)
                {
                    var indexStr = indexMatch.Groups[1].Value;
                    if (!int.TryParse(indexStr, out var index))
                    {
                        return false;
                    }

                    if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                    {
                        return false;
                    }

                    current = current[index];
                }
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else
            {
                return false;
            }
        }

        result = current;
        return true;
    }
}