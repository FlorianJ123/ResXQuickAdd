using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using ResXQuickAdd.Actions;
using ResXQuickAdd.Analyzers;
using ResXQuickAdd.Services;

namespace ResXQuickAdd.Providers
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("ResX Resource Quick Actions")]
    [ContentType("CSharp")]
    internal class ResXCodeActionProvider : ISuggestedActionsSourceProvider
    {
        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null || textView == null)
                return null;

            return new ResXSuggestedActionsSource(textView, textBuffer);
        }
    }

    internal class ResXSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly ITextView _textView;
        private readonly ITextBuffer _textBuffer;

        public ResXSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            _textView = textView ?? throw new ArgumentNullException(nameof(textView));
            _textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose()
        {
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            if (!requestedActionCategories.Contains(PredefinedSuggestedActionCategoryNames.CodeFix))
                return Enumerable.Empty<SuggestedActionSet>();

            var document = range.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document?.Project == null)
                return Enumerable.Empty<SuggestedActionSet>();

            var resourceInfo = GetMissingResourceInfo(document, range, cancellationToken);
            if (resourceInfo == null || !resourceInfo.IsValidMissingResource)
                return Enumerable.Empty<SuggestedActionSet>();

            var actions = CreateSuggestedActions(resourceInfo, document.Project);
            if (actions?.Any() == true)
            {
                return new[]
                {
                    new SuggestedActionSet(
                        categoryName: PredefinedSuggestedActionCategoryNames.CodeFix,
                        actions: actions,
                        title: "ResX Resources",
                        priority: SuggestedActionSetPriority.Medium,
                        applicableToSpan: range)
                };
            }

            return Enumerable.Empty<SuggestedActionSet>();
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            if (!requestedActionCategories.Contains(PredefinedSuggestedActionCategoryNames.CodeFix))
                return false;

            var document = range.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document?.Project == null)
                return false;

            return await Task.Run(() =>
            {
                var resourceInfo = GetMissingResourceInfo(document, range, cancellationToken);
                return resourceInfo?.IsValidMissingResource == true;
            }, cancellationToken);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Parse("8cca33bd-7ab9-46dd-b034-c9be7bd81cd0");
            return true;
        }

        private MissingResourceInfo GetMissingResourceInfo(Document document, SnapshotSpan range, CancellationToken cancellationToken)
        {
            try
            {
                var syntaxRoot = document.GetSyntaxRootAsync(cancellationToken).Result;
                var semanticModel = document.GetSemanticModelAsync(cancellationToken).Result;

                if (syntaxRoot == null || semanticModel == null)
                    return null;

                var resxFileService = new ResXFileService(document.Project);
                var languageDetectionService = new LanguageDetectionService(resxFileService);
                var analyzer = new MissingResourceAnalyzer(resxFileService, languageDetectionService);

                return analyzer.AnalyzeAtPosition(document, syntaxRoot, semanticModel, range.Start);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing missing resource: {ex.Message}");
                return null;
            }
        }

        private IEnumerable<ISuggestedAction> CreateSuggestedActions(MissingResourceInfo resourceInfo, Project project)
        {
            try
            {
                var resxFileService = new ResXFileService(project);
                var languageDetectionService = new LanguageDetectionService(resxFileService);
                var designerFileService = new DesignerFileService();

                var action = new AddMissingResourceAction(
                    resourceInfo,
                    project,
                    resxFileService,
                    languageDetectionService,
                    designerFileService);

                if (action.CanExecute())
                {
                    return new[] { new ResXSuggestedAction(action) };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating suggested actions: {ex.Message}");
            }

            return Enumerable.Empty<ISuggestedAction>();
        }
    }

    internal class ResXSuggestedAction : ISuggestedAction
    {
        private readonly AddMissingResourceAction _action;

        public ResXSuggestedAction(AddMissingResourceAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public string DisplayText => _action.Title;

        public string IconAutomationText => _action.Title;

        public string InputGestureText => null;

        public bool HasActionSets => false;

        public bool HasPreview => false;

        public ImageMoniker IconMoniker => default(ImageMoniker);

        ImageMoniker ISuggestedAction.IconMoniker => IconMoniker;

        public void Dispose()
        {
        }

        public IEnumerable<SuggestedActionSet> GetActionSets()
        {
            return Enumerable.Empty<SuggestedActionSet>();
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(null);
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            try
            {
                var task = _action.ExecuteAsync(cancellationToken);
                task.Wait(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error invoking ResX action: {ex.Message}");
            }
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Parse("8cca33bd-7ab9-46dd-b034-c9be7bd81cd0");
            return true;
        }
    }
}