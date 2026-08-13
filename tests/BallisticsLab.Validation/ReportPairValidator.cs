using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

internal static class ReportPairValidator
{
    private const string Header =
        "schema,pluginVersion,sequence,utc,chainId,fireIndex,fragmentIndex,parentDepth,rootRandomSeed,isForwardHit,ammoTemplateId,ammoName,shooter,targetKind,target,material,fixtureId,layer,layerCount,fixtureTemplateId,fixtureName,fixtureArmorClass,fixtureArmorMaterial,layerSpacing,colliderThickness,outcome,angleDegrees,impactSpeed,templateSpeed,fraction,incomingDamage,incomingPenetration,decisionDamage,decisionPenetration,armorRealResistance,armorClassResistance,armorCf,penetrationChancePercent,blockedBy,deflectedBy,fragments,durabilityBefore,durabilityAfter,fixtureMaximumDurability,bodyHealthBefore,bodyHealthAfter,targetAliveBefore,targetAliveAfter,armorChanges,continuationKind,continuationSourceFixtureId,continuationSourceLayer,continuationPenetrationFactor,continuationVelocityFactor,continuationOutcomeFactor,continuationArmorCf,continuationDamageBefore,continuationPenetrationBefore,continuationDamageAfter,continuationPenetrationAfter,hitX,hitY,hitZ";
    private static readonly string[] ParserHeader = { "schema", "name", "notes" };
    private static readonly string[] ParserValues = { "3", "a,b", "line1\r\nline2 \"quoted\"" };

    internal static bool ParserHandlesQuotedFields()
    {
        const string text = "schema,name,notes\r\n3,\"a,b\",\"line1\r\nline2 \"\"quoted\"\"\"\r\n";
        List<IReadOnlyList<string>> rows = ParseCsv(text);
        return rows.Count == 2
            && rows[0].SequenceEqual(ParserHeader, StringComparer.Ordinal)
            && rows[1].SequenceEqual(ParserValues, StringComparer.Ordinal);
    }

    internal static bool ValidatorMatchesSyntheticPair()
    {
        return ValidateSyntheticPair(false);
    }

    internal static bool ValidatorRejectsSyntheticMismatch()
    {
        return ValidateSyntheticPair(true);
    }

    internal static bool ValidatorAcceptsPhysicalOnlySchemaFourPair()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.PhysicalPair." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string jsonPath = Path.Combine(directory, "BallisticsLab-physical.json");
            File.WriteAllText(
                jsonPath,
                "{\"schema\":4,\"pluginVersion\":\"0.2.8\",\"records\":[],"
                    + "\"physicalTransitions\":[{\"transitionId\":\"physical-only\"}]}");
            File.WriteAllText(Path.ChangeExtension(jsonPath, ".csv"), Header + "\r\n");
            return Validate(jsonPath, out _);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool ValidateSyntheticPair(bool corruptCsv)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.Validation." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string jsonPath = Path.Combine(directory, "BallisticsLab-synthetic.json");
            string csvPath = Path.ChangeExtension(jsonPath, ".csv");
            string[] header = Header.Split(',');
            string[] csvValues = new string[header.Length];
            Dictionary<string, object> record = new(StringComparer.Ordinal);
            HashSet<string> stringFields = new(StringComparer.Ordinal)
            {
                "utc", "chainId", "ammoTemplateId", "ammoName", "shooter", "targetKind",
                "target", "material", "fixtureTemplateId", "fixtureName", "fixtureArmorMaterial",
                "outcome", "blockedBy", "deflectedBy", "armorChanges", "continuationKind"
            };
            HashSet<string> booleanFields = new(StringComparer.Ordinal)
            {
                "isForwardHit", "targetAliveBefore", "targetAliveAfter"
            };

            for (int index = 0; index < header.Length; index++)
            {
                string column = header[index];
                if (column == "schema")
                {
                    csvValues[index] = "4";
                }
                else if (column == "pluginVersion")
                {
                    csvValues[index] = "0.2.6";
                }
                else if (column == "fragments")
                {
                    csvValues[index] = "4";
                    record["fragmentCount"] = 4;
                }
                else if (column == "hitX")
                {
                    csvValues[index] = "10";
                }
                else if (column == "hitY")
                {
                    csvValues[index] = "20";
                }
                else if (column == "hitZ")
                {
                    csvValues[index] = "30";
                }
                else if (booleanFields.Contains(column))
                {
                    csvValues[index] = "true";
                    record[column] = true;
                }
                else if (stringFields.Contains(column))
                {
                    string value = column == "ammoName"
                        ? "round, \"test\"\r\nsecond line"
                        : column + " value";
                    csvValues[index] = value;
                    record[column] = value;
                }
                else
                {
                    int value = index + 1;
                    csvValues[index] = value.ToString(CultureInfo.InvariantCulture);
                    record[column] = value;
                }
            }

            record["hitPoint"] = new[] { 10, 20, 30 };
            record["path"] = new[] { new[] { 1, 2, 3 }, new[] { 10, 20, 30 } };
            Dictionary<string, object> root = new(StringComparer.Ordinal)
            {
                ["schema"] = 4,
                ["pluginVersion"] = "0.2.6",
                ["records"] = new[] { record },
                ["physicalTransitions"] = Array.Empty<object>()
            };

            if (corruptCsv)
            {
                csvValues[Array.IndexOf(header, "decisionPenetration")] = "999999";
            }

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(root));
            File.WriteAllText(
                csvPath,
                Header + "\r\n" + string.Join(',', csvValues.Select(Csv)) + "\r\n");
            bool valid = Validate(jsonPath, out string failure);
            return corruptCsv
                ? !valid && failure.Contains("decisionPenetration", StringComparison.Ordinal)
                : valid;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Arbitrary report faults are returned as validation failures by contract.")]
    internal static bool Validate(string jsonPath, out string failure)
    {
        failure = string.Empty;
        try
        {
            string csvPath = Path.ChangeExtension(jsonPath, ".csv");
            if (!File.Exists(csvPath))
            {
                failure = "matching CSV file is missing";
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("schema", out JsonElement schema)
                || !root.TryGetProperty("pluginVersion", out JsonElement pluginVersion)
                || !root.TryGetProperty("records", out JsonElement records)
                || records.ValueKind != JsonValueKind.Array)
            {
                failure = "JSON root is missing schema, pluginVersion, or records";
                return false;
            }
            if (!schema.TryGetInt32(out int schemaNumber))
            {
                failure = "JSON schema is not an integer";
                return false;
            }
            if (schemaNumber >= 4
                && (!root.TryGetProperty("physicalTransitions", out JsonElement physicalTransitions)
                    || physicalTransitions.ValueKind != JsonValueKind.Array))
            {
                failure = "schema 4 JSON is missing physicalTransitions";
                return false;
            }

            List<IReadOnlyList<string>> rows = ParseCsv(File.ReadAllText(csvPath));
            if (rows.Count == 0)
            {
                failure = "CSV is empty";
                return false;
            }

            string[] expectedHeader = Header.Split(',');
            if (!rows[0].SequenceEqual(expectedHeader, StringComparer.Ordinal))
            {
                failure = "CSV header differs from the flat shot-record contract";
                return false;
            }

            if (rows.Count - 1 != records.GetArrayLength())
            {
                failure = "CSV and JSON record counts differ";
                return false;
            }

            string schemaValue = Comparable(schema);
            string pluginVersionValue = Comparable(pluginVersion);
            int rowIndex = 1;
            foreach (JsonElement record in records.EnumerateArray())
            {
                IReadOnlyList<string> row = rows[rowIndex];
                if (row.Count != expectedHeader.Length)
                {
                    failure = "CSV row " + rowIndex.ToString(CultureInfo.InvariantCulture)
                        + " has " + row.Count.ToString(CultureInfo.InvariantCulture)
                        + " fields instead of " + expectedHeader.Length.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                for (int columnIndex = 0; columnIndex < expectedHeader.Length; columnIndex++)
                {
                    string column = expectedHeader[columnIndex];
                    string expected;
                    if (column == "schema")
                    {
                        expected = schemaValue;
                    }
                    else if (column == "pluginVersion")
                    {
                        expected = pluginVersionValue;
                    }
                    else if (column == "fragments")
                    {
                        if (!TryComparable(record, "fragmentCount", out expected))
                        {
                            failure = Missing(rowIndex, "fragmentCount");
                            return false;
                        }
                    }
                    else if (column is "hitX" or "hitY" or "hitZ")
                    {
                        if (!TryHitCoordinate(record, column, out expected))
                        {
                            failure = Missing(rowIndex, "hitPoint");
                            return false;
                        }
                    }
                    else if (!TryComparable(record, column, out expected))
                    {
                        failure = Missing(rowIndex, column);
                        return false;
                    }

                    if (!string.Equals(row[columnIndex], expected, StringComparison.Ordinal))
                    {
                        failure = "row " + rowIndex.ToString(CultureInfo.InvariantCulture)
                            + " column " + column + " differs: CSV=" + Quote(row[columnIndex])
                            + " JSON=" + Quote(expected);
                        return false;
                    }
                }

                if (!record.TryGetProperty("path", out JsonElement path)
                    || path.ValueKind != JsonValueKind.Array)
                {
                    failure = "JSON row " + rowIndex.ToString(CultureInfo.InvariantCulture)
                        + " is missing its trajectory path";
                    return false;
                }

                rowIndex++;
            }

            return true;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        List<IReadOnlyList<string>> rows = new();
        List<string> row = new();
        StringBuilder field = new();
        bool quoted = false;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quoted)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(current);
                }
                continue;
            }

            if (current == '"')
            {
                if (field.Length != 0)
                {
                    throw new FormatException("Quote appeared after unquoted field content.");
                }
                quoted = true;
            }
            else if (current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (current == '\r' || current == '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(current);
            }
        }

        if (quoted)
        {
            throw new FormatException("CSV ended inside a quoted field.");
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static bool TryComparable(JsonElement record, string name, out string value)
    {
        if (record.TryGetProperty(name, out JsonElement element))
        {
            value = Comparable(element);
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryHitCoordinate(JsonElement record, string column, out string value)
    {
        value = string.Empty;
        if (!record.TryGetProperty("hitPoint", out JsonElement point)
            || point.ValueKind != JsonValueKind.Array
            || point.GetArrayLength() != 3)
        {
            return false;
        }

        int coordinate = column == "hitX" ? 0 : column == "hitY" ? 1 : 2;
        value = Comparable(point[coordinate]);
        return true;
    }

    private static string Comparable(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    private static string Missing(int rowIndex, string field)
    {
        return "JSON row " + rowIndex.ToString(CultureInfo.InvariantCulture)
            + " is missing " + field;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }

    private static string Csv(string value)
    {
        foreach (char character in value)
        {
            if (character == ',' || character == '"' || character == '\r' || character == '\n')
            {
                return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            }
        }

        return value;
    }
}
