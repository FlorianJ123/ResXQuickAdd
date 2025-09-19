using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ResXQuickAdd.Utilities;

namespace ResXQuickAdd.Services
{
    public class LanguageConfiguration
    {
        public string PrimaryLanguage { get; set; }
        public string PrimaryLanguageDisplayName { get; set; }
        public string SecondaryLanguage { get; set; }
        public string SecondaryLanguageDisplayName { get; set; }
        public ResXFileInfo PrimaryFile { get; set; }
        public ResXFileInfo SecondaryFile { get; set; }
        public bool HasMultipleLanguages => SecondaryFile != null;
    }

    public class LanguageDetectionService
    {
        private readonly ResXFileService _resxFileService;

        public LanguageDetectionService(ResXFileService resxFileService)
        {
            _resxFileService = resxFileService ?? throw new ArgumentNullException(nameof(resxFileService));
        }

        public LanguageConfiguration DetectLanguageConfiguration(string baseName)
        {
            var resxFiles = _resxFileService.FindResXFiles(baseName);
            
            if (!resxFiles.Any())
            {
                return CreateEmptyConfiguration(baseName);
            }

            if (resxFiles.Count == 1)
            {
                return CreateSingleFileConfiguration(resxFiles.First());
            }

            return CreateMultiFileConfiguration(resxFiles);
        }

        private LanguageConfiguration CreateEmptyConfiguration(string baseName)
        {
            return new LanguageConfiguration
            {
                PrimaryLanguage = "en",
                PrimaryLanguageDisplayName = "English",
                SecondaryLanguage = "de",
                SecondaryLanguageDisplayName = "German",
                PrimaryFile = null,
                SecondaryFile = null
            };
        }

        private LanguageConfiguration CreateSingleFileConfiguration(ResXFileInfo singleFile)
        {
            var detectedLanguage = DetectLanguageFromCulture(singleFile.Culture);
            var otherLanguage = detectedLanguage == "de" ? "en" : "de";
            
            return new LanguageConfiguration
            {
                PrimaryLanguage = detectedLanguage,
                PrimaryLanguageDisplayName = GetLanguageDisplayName(detectedLanguage),
                SecondaryLanguage = otherLanguage,
                SecondaryLanguageDisplayName = GetLanguageDisplayName(otherLanguage),
                PrimaryFile = singleFile,
                SecondaryFile = null
            };
        }

        private LanguageConfiguration CreateMultiFileConfiguration(List<ResXFileInfo> resxFiles)
        {
            var defaultFile = resxFiles.FirstOrDefault(f => f.IsDefault);
            var germanFile = resxFiles.FirstOrDefault(f => IsGermanCulture(f.Culture));
            var englishFile = resxFiles.FirstOrDefault(f => IsEnglishCulture(f.Culture));

            if (defaultFile != null && germanFile != null)
            {
                var defaultLanguage = DetectDefaultFileLanguage(defaultFile, resxFiles);
                if (defaultLanguage == "de")
                {
                    return new LanguageConfiguration
                    {
                        PrimaryLanguage = "de",
                        PrimaryLanguageDisplayName = "German",
                        SecondaryLanguage = "en",
                        SecondaryLanguageDisplayName = "English",
                        PrimaryFile = defaultFile,
                        SecondaryFile = englishFile ?? germanFile
                    };
                }
                else
                {
                    return new LanguageConfiguration
                    {
                        PrimaryLanguage = "en",
                        PrimaryLanguageDisplayName = "English",
                        SecondaryLanguage = "de",
                        SecondaryLanguageDisplayName = "German",
                        PrimaryFile = defaultFile,
                        SecondaryFile = germanFile
                    };
                }
            }

            if (defaultFile != null)
            {
                var secondaryFile = resxFiles.FirstOrDefault(f => !f.IsDefault);
                var secondaryLanguage = secondaryFile != null 
                    ? DetectLanguageFromCulture(secondaryFile.Culture)
                    : "de";

                return new LanguageConfiguration
                {
                    PrimaryLanguage = "en",
                    PrimaryLanguageDisplayName = "English",
                    SecondaryLanguage = secondaryLanguage,
                    SecondaryLanguageDisplayName = GetLanguageDisplayName(secondaryLanguage),
                    PrimaryFile = defaultFile,
                    SecondaryFile = secondaryFile
                };
            }

            var firstFile = resxFiles.First();
            var secondFile = resxFiles.Skip(1).FirstOrDefault();

            return new LanguageConfiguration
            {
                PrimaryLanguage = DetectLanguageFromCulture(firstFile.Culture),
                PrimaryLanguageDisplayName = GetLanguageDisplayName(DetectLanguageFromCulture(firstFile.Culture)),
                SecondaryLanguage = secondFile != null ? DetectLanguageFromCulture(secondFile.Culture) : "en",
                SecondaryLanguageDisplayName = secondFile != null ? GetLanguageDisplayName(DetectLanguageFromCulture(secondFile.Culture)) : "English",
                PrimaryFile = firstFile,
                SecondaryFile = secondFile
            };
        }

        private string DetectDefaultFileLanguage(ResXFileInfo defaultFile, List<ResXFileInfo> allFiles)
        {
            var hasGermanFile = allFiles.Any(f => IsGermanCulture(f.Culture));
            var hasEnglishFile = allFiles.Any(f => IsEnglishCulture(f.Culture));

            if (hasGermanFile && !hasEnglishFile)
            {
                return "en";
            }

            if (hasEnglishFile && !hasGermanFile)
            {
                return "de";
            }

            return "en";
        }

        private string DetectLanguageFromCulture(string culture)
        {
            if (string.IsNullOrEmpty(culture) || culture == "default")
            {
                return "en";
            }

            culture = culture.ToLowerInvariant();

            if (culture.StartsWith("de"))
                return "de";
            
            if (culture.StartsWith("en"))
                return "en";

            if (culture.StartsWith("fr"))
                return "fr";

            if (culture.StartsWith("es"))
                return "es";

            if (culture.StartsWith("it"))
                return "it";

            return "en";
        }

        private bool IsGermanCulture(string culture)
        {
            if (string.IsNullOrEmpty(culture))
                return false;

            culture = culture.ToLowerInvariant();
            return culture.StartsWith("de");
        }

        private bool IsEnglishCulture(string culture)
        {
            if (string.IsNullOrEmpty(culture))
                return false;

            culture = culture.ToLowerInvariant();
            return culture.StartsWith("en");
        }

        private string GetLanguageDisplayName(string languageCode)
        {
            switch (languageCode?.ToLowerInvariant())
            {
                case "de":
                    return "German";
                case "en":
                    return "English";
                case "fr":
                    return "French";
                case "es":
                    return "Spanish";
                case "it":
                    return "Italian";
                default:
                    try
                    {
                        var culture = new CultureInfo(languageCode);
                        return culture.EnglishName;
                    }
                    catch
                    {
                        return languageCode?.ToUpperInvariant() ?? "Unknown";
                    }
            }
        }

        public string GetTranslationInputLabel(string languageCode)
        {
            var displayName = GetLanguageDisplayName(languageCode);
            return $"{displayName} Translation";
        }
    }
}