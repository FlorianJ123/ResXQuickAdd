using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using ResXQuickAdd.Utilities;

namespace ResXQuickAdd.Services
{
    public class ResXFileInfo
    {
        public string FilePath { get; set; }
        public string BaseName { get; set; }
        public string Culture { get; set; }
        public bool IsDefault { get; set; }
    }

    public class ResXFileService
    {
        private readonly Project _project;

        public ResXFileService(Project project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public List<ResXFileInfo> FindResXFiles(string baseName)
        {
            var resxFiles = new List<ResXFileInfo>();

            if (_project?.FilePath == null)
                return resxFiles;

            var projectDir = Path.GetDirectoryName(_project.FilePath);
            if (!Directory.Exists(projectDir))
                return resxFiles;

            var pattern = $"{baseName}*.resx";
            var files = Directory.GetFiles(projectDir, pattern, SearchOption.AllDirectories)
                .Where(f => IsValidResXFile(f, baseName))
                .ToArray();

            foreach (var file in files)
            {
                var culture = ResXHelper.GetCultureFromResXFileName(file);
                var fileBaseName = ResXHelper.GetResourceBaseName(file);
                
                if (string.Equals(fileBaseName, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    resxFiles.Add(new ResXFileInfo
                    {
                        FilePath = file,
                        BaseName = fileBaseName,
                        Culture = culture ?? "default",
                        IsDefault = string.IsNullOrEmpty(culture)
                    });
                }
            }

            return resxFiles.OrderBy(f => f.IsDefault ? 0 : 1).ThenBy(f => f.Culture).ToList();
        }

        public ResXFileInfo FindDefaultResXFile(string baseName)
        {
            var files = FindResXFiles(baseName);
            return files.FirstOrDefault(f => f.IsDefault) ?? files.FirstOrDefault();
        }

        public bool ResourceKeyExists(string baseName, string key)
        {
            var files = FindResXFiles(baseName);
            return files.Any(file => ResXHelper.ResourceKeyExists(file.FilePath, key));
        }

        public List<ResXFileInfo> FindResXFilesByDesignerClass(string designerClassName)
        {
            if (_project?.FilePath == null)
                return new List<ResXFileInfo>();

            var projectDir = Path.GetDirectoryName(_project.FilePath);
            if (!Directory.Exists(projectDir))
                return new List<ResXFileInfo>();

            var designerFiles = Directory.GetFiles(projectDir, "*.Designer.cs", SearchOption.AllDirectories);
            
            foreach (var designerFile in designerFiles)
            {
                try
                {
                    var content = File.ReadAllText(designerFile);
                    if (content.Contains($"class {designerClassName}") || 
                        content.Contains($"partial class {designerClassName}"))
                    {
                        var resxFile = Path.ChangeExtension(designerFile.Replace(".Designer.cs", ""), ".resx");
                        if (File.Exists(resxFile))
                        {
                            var baseName = ResXHelper.GetResourceBaseName(resxFile);
                            return FindResXFiles(baseName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading designer file {designerFile}: {ex.Message}");
                }
            }

            return FindResXFiles(designerClassName);
        }

        public bool IsResourceDesignerClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return false;

            var files = FindResXFilesByDesignerClass(className);
            return files.Any();
        }

        public string GetDesignerFilePath(string resxPath)
        {
            var basePath = Path.ChangeExtension(resxPath, null);
            if (basePath.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
                basePath = basePath.Substring(0, basePath.Length - 9);

            var designerPath = basePath + ".Designer.cs";
            return File.Exists(designerPath) ? designerPath : null;
        }

        public List<string> GetExistingResourceKeys(string baseName)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = FindResXFiles(baseName);

            foreach (var file in files)
            {
                var resources = ResXHelper.ReadResourceFile(file.FilePath);
                foreach (var key in resources.Keys)
                {
                    keys.Add(key);
                }
            }

            return keys.ToList();
        }

        private bool IsValidResXFile(string filePath, string baseName)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var fileBaseName = ResXHelper.GetResourceBaseName(filePath);
                
                return string.Equals(fileBaseName, baseName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}