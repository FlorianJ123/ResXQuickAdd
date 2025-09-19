using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using ResXQuickAdd.Analyzers;
using ResXQuickAdd.Dialogs;
using ResXQuickAdd.Services;
using ResXQuickAdd.Utilities;
using Task = System.Threading.Tasks.Task;

namespace ResXQuickAdd.Actions
{
    public class AddMissingResourceAction
    {
        private readonly MissingResourceInfo _resourceInfo;
        private readonly Project _project;
        private readonly ResXFileService _resxFileService;
        private readonly LanguageDetectionService _languageDetectionService;
        private readonly DesignerFileService _designerFileService;

        public AddMissingResourceAction(
            MissingResourceInfo resourceInfo, 
            Project project,
            ResXFileService resxFileService,
            LanguageDetectionService languageDetectionService,
            DesignerFileService designerFileService)
        {
            _resourceInfo = resourceInfo ?? throw new ArgumentNullException(nameof(resourceInfo));
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _resxFileService = resxFileService ?? throw new ArgumentNullException(nameof(resxFileService));
            _languageDetectionService = languageDetectionService ?? throw new ArgumentNullException(nameof(languageDetectionService));
            _designerFileService = designerFileService ?? throw new ArgumentNullException(nameof(designerFileService));
        }

        public string Title => "Add missing string resource";

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var languageConfig = _languageDetectionService.DetectLanguageConfiguration(_resourceInfo.BaseName);
                
                var dialog = new AddResourceDialog(languageConfig, _resourceInfo.PropertyName);
                var result = dialog.ShowDialog();

                if (result != true)
                    return false;

                var success = await AddResourceToFiles(dialog, languageConfig, cancellationToken);
                
                if (success)
                {
                    await RegenerateDesignerFiles(languageConfig, cancellationToken);
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing AddMissingResourceAction: {ex.Message}");
                
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                MessageBox.Show(
                    $"Failed to add resource: {ex.Message}", 
                    "ResX Quick Add", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                return false;
            }
        }

        private async Task<bool> AddResourceToFiles(AddResourceDialog dialog, LanguageConfiguration languageConfig, CancellationToken cancellationToken)
        {
            bool success = true;

            if (languageConfig.HasMultipleLanguages)
            {
                success &= await AddResourceToFile(
                    languageConfig.PrimaryFile.FilePath,
                    _resourceInfo.PropertyName,
                    dialog.PrimaryTranslation,
                    null,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(dialog.SecondaryTranslation) && languageConfig.SecondaryFile != null)
                {
                    success &= await AddResourceToFile(
                        languageConfig.SecondaryFile.FilePath,
                        _resourceInfo.PropertyName,
                        dialog.SecondaryTranslation,
                        null,
                        cancellationToken);
                }
            }
            else if (languageConfig.PrimaryFile != null)
            {
                var comment = !string.IsNullOrWhiteSpace(dialog.SecondaryTranslation) 
                    ? $"{languageConfig.SecondaryLanguageDisplayName}: {dialog.SecondaryTranslation}"
                    : null;

                success &= await AddResourceToFile(
                    languageConfig.PrimaryFile.FilePath,
                    _resourceInfo.PropertyName,
                    dialog.PrimaryTranslation,
                    comment,
                    cancellationToken);
            }

            return success;
        }

        private async Task<bool> AddResourceToFile(string filePath, string key, string value, string comment, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var backupPath = ResXHelper.CreateBackupFile(filePath);
                    var success = ResXHelper.AddResourceToFile(filePath, key, value, comment);
                    
                    if (!success && backupPath != null)
                    {
                        try
                        {
                            System.IO.File.Copy(backupPath, filePath, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error restoring backup: {ex.Message}");
                        }
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding resource to file {filePath}: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        private async Task RegenerateDesignerFiles(LanguageConfiguration languageConfig, CancellationToken cancellationToken)
        {
            try
            {
                if (languageConfig.PrimaryFile != null)
                {
                    await _designerFileService.RegenerateDesignerFileAsync(languageConfig.PrimaryFile.FilePath);
                    await _designerFileService.NotifyFileChangedAsync(languageConfig.PrimaryFile.FilePath);
                }

                if (languageConfig.SecondaryFile != null)
                {
                    await _designerFileService.RegenerateDesignerFileAsync(languageConfig.SecondaryFile.FilePath);
                    await _designerFileService.NotifyFileChangedAsync(languageConfig.SecondaryFile.FilePath);
                }

                await Task.Delay(100, cancellationToken);
                await _designerFileService.InvalidateIntelliSenseAsync(_project);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error regenerating designer files: {ex.Message}");
            }
        }

        public bool CanExecute()
        {
            if (_resourceInfo?.IsValidMissingResource != true)
                return false;

            if (string.IsNullOrWhiteSpace(_resourceInfo.PropertyName) || 
                string.IsNullOrWhiteSpace(_resourceInfo.BaseName))
                return false;

            if (!ResXHelper.IsValidResourceKey(_resourceInfo.PropertyName))
                return false;

            var languageConfig = _languageDetectionService.DetectLanguageConfiguration(_resourceInfo.BaseName);
            return languageConfig != null;
        }
    }
}