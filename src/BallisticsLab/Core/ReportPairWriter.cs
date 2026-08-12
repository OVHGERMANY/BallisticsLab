using System;
using System.IO;
using System.Text;

namespace BallisticsLab.Core
{
    public readonly struct ReportPairPaths
    {
        public ReportPairPaths(string csvPath, string jsonPath)
        {
            CsvPath = csvPath;
            JsonPath = jsonPath;
        }

        public string CsvPath { get; }
        public string JsonPath { get; }

        public override string ToString()
        {
            return CsvPath + " | " + JsonPath;
        }
    }

    public static class ReportPairWriter
    {
        public static ReportPairPaths Write(
            string reportDirectory,
            string stem,
            string csv,
            string json)
        {
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                throw new ArgumentException("Report directory is required.", nameof(reportDirectory));
            }
            if (string.IsNullOrWhiteSpace(stem)
                || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Report stem is invalid.", nameof(stem));
            }

            Directory.CreateDirectory(reportDirectory);
            string csvPath = Path.Combine(reportDirectory, stem + ".csv");
            string jsonPath = Path.Combine(reportDirectory, stem + ".json");
            if (File.Exists(csvPath) || File.Exists(jsonPath))
            {
                throw new IOException("A report already exists for stem " + stem + ".");
            }

            string token = Guid.NewGuid().ToString("N");
            string csvTemporaryPath = csvPath + "." + token + ".tmp";
            string jsonTemporaryPath = jsonPath + "." + token + ".tmp";
            try
            {
                File.WriteAllText(csvTemporaryPath, csv ?? string.Empty, new UTF8Encoding(false));
                File.WriteAllText(jsonTemporaryPath, json ?? string.Empty, new UTF8Encoding(false));
                File.Move(csvTemporaryPath, csvPath);
                File.Move(jsonTemporaryPath, jsonPath);
                return new ReportPairPaths(csvPath, jsonPath);
            }
            catch
            {
                DeleteIfPresent(csvPath);
                DeleteIfPresent(jsonPath);
                throw;
            }
            finally
            {
                DeleteIfPresent(csvTemporaryPath);
                DeleteIfPresent(jsonTemporaryPath);
            }
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
