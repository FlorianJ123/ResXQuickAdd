using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ResXQuickAdd.Utilities
{
    public static class ResXHelper
    {
        private static readonly Regex ValidKeyPattern = new Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public static bool IsValidResourceKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && ValidKeyPattern.IsMatch(key);
        }

        public static Dictionary<string, string> ReadResourceFile(string resxPath)
        {
            if (!File.Exists(resxPath))
                return new Dictionary<string, string>();

            var resources = new Dictionary<string, string>();

            try
            {
                var doc = XDocument.Load(resxPath, LoadOptions.PreserveWhitespace);
                foreach (var dataElem in doc.Root?.Elements("data") ?? new List<XElement>())
                {
                    var nameAttr = dataElem.Attribute("name");
                    if (nameAttr == null) continue;

                    var valueElem = dataElem.Element("value");
                    if (valueElem == null) continue;

                    var key = nameAttr.Value;
                    var value = valueElem.Value;
                    if (!string.IsNullOrEmpty(key) && value != null)
                    {
                        resources[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading ResX file {resxPath}: {ex.Message}");
            }

            return resources;
        }

        public static bool AddResourceToFile(string resxPath, string key, string value, string comment = null)
        {
            if (!IsValidResourceKey(key))
                return false;

            try
            {
                XDocument doc;
                if (File.Exists(resxPath))
                {
                    try
                    {
                        doc = XDocument.Load(resxPath, LoadOptions.PreserveWhitespace);
                    }
                    catch
                    {
                        // If file is corrupted or unreadable create a new skeleton
                        doc = CreateEmptyResX();
                    }
                }
                else
                {
                    doc = CreateEmptyResX();
                }

                // Ensure root exists
                var root = doc.Root ?? CreateEmptyResX().Root;
                if (doc.Root == null)
                    doc.Add(root);

                // Check duplicates
                foreach (var existing in root.Elements("data"))
                {
                    if (string.Equals(existing.Attribute("name")?.Value, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // already exists
                    }
                }

                var dataElem = new XElement("data");
                dataElem.SetAttributeValue("name", key);
                dataElem.SetAttributeValue("xml:space", "preserve");
                dataElem.Add(new XElement("value", value ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    dataElem.Add(new XElement("comment", comment));
                }

                root.Add(dataElem);

                using (var fs = File.Open(resxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    doc.Save(fs);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to ResX file {resxPath}: {ex.Message}");
                return false;
            }
        }

        private static XDocument CreateEmptyResX()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("root",
                    new XElement("resheader",
                        new XAttribute("name", "resmimetype"),
                        new XElement("value", "text/microsoft-resx")),
                    new XElement("resheader",
                        new XAttribute("name", "version"),
                        new XElement("value", "2.0")),
                    new XElement("resheader",
                        new XAttribute("name", "reader"),
                        new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                    new XElement("resheader",
                        new XAttribute("name", "writer"),
                        new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"))
                ));
        }

        public static bool ResourceKeyExists(string resxPath, string key)
        {
            if (!File.Exists(resxPath) || string.IsNullOrWhiteSpace(key))
                return false;

            var resources = ReadResourceFile(resxPath);
            return resources.ContainsKey(key);
        }

        public static string GetResourceBaseName(string resxPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(resxPath);
            
            var lastDot = fileName.LastIndexOf('.');
            if (lastDot > 0)
            {
                var potentialCulture = fileName.Substring(lastDot + 1);
                if (IsValidCultureCode(potentialCulture))
                {
                    return fileName.Substring(0, lastDot);
                }
            }
            
            return fileName;
        }

        public static string GetCultureFromResXFileName(string resxPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(resxPath);
            
            var lastDot = fileName.LastIndexOf('.');
            if (lastDot > 0)
            {
                var potentialCulture = fileName.Substring(lastDot + 1);
                if (IsValidCultureCode(potentialCulture))
                {
                    return potentialCulture;
                }
            }
            
            return null;
        }

        private static bool IsValidCultureCode(string culture)
        {
            return culture.Length == 2 || 
                   (culture.Length == 5 && culture[2] == '-') ||
                   culture.Equals("en-US", StringComparison.OrdinalIgnoreCase) ||
                   culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateBackupFile(string resxPath)
        {
            if (!File.Exists(resxPath))
                return null;

            try
            {
                var backupPath = resxPath + ".backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(resxPath, backupPath);
                return backupPath;
            }
            catch
            {
                return null;
            }
        }
    }
}