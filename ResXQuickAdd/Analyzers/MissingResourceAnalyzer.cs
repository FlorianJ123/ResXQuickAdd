using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ResXQuickAdd.Services;

namespace ResXQuickAdd.Analyzers
{
    public class MissingResourceInfo
    {
        public string ClassName { get; set; }
        public string PropertyName { get; set; }
        public string BaseName { get; set; }
        public TextSpan TextSpan { get; set; }
        public SyntaxNode Node { get; set; }
        public bool IsValidMissingResource { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class MissingResourceAnalyzer
    {
        private readonly ResXFileService _resxFileService;
        private readonly LanguageDetectionService _languageDetectionService;

        public MissingResourceAnalyzer(ResXFileService resxFileService, LanguageDetectionService languageDetectionService)
        {
            _resxFileService = resxFileService ?? throw new ArgumentNullException(nameof(resxFileService));
            _languageDetectionService = languageDetectionService ?? throw new ArgumentNullException(nameof(languageDetectionService));
        }

        public List<MissingResourceInfo> AnalyzeDocument(Document document, SyntaxNode root, SemanticModel semanticModel)
        {
            var missingResources = new List<MissingResourceInfo>();

            if (root == null || semanticModel == null)
                return missingResources;

            var memberAccessNodes = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(IsResourceAccess);

            foreach (var memberAccess in memberAccessNodes)
            {
                var resourceInfo = AnalyzeMemberAccess(memberAccess, semanticModel);
                if (resourceInfo != null)
                {
                    missingResources.Add(resourceInfo);
                }
            }

            return missingResources;
        }

        public MissingResourceInfo AnalyzeAtPosition(Document document, SyntaxNode root, SemanticModel semanticModel, int position)
        {
            if (root == null || semanticModel == null)
                return null;

            var node = root.FindNode(TextSpan.FromBounds(position, position));
            
            var memberAccess = node.AncestorsAndSelf()
                .OfType<MemberAccessExpressionSyntax>()
                .FirstOrDefault();

            if (memberAccess != null && IsResourceAccess(memberAccess))
            {
                return AnalyzeMemberAccess(memberAccess, semanticModel);
            }

            return null;
        }

        private bool IsResourceAccess(MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess?.Expression == null || memberAccess.Name == null)
                return false;

            if (memberAccess.Expression is IdentifierNameSyntax identifierName)
            {
                var className = identifierName.Identifier.ValueText;
                return IsLikelyResourceClass(className);
            }

            return false;
        }

        private bool IsLikelyResourceClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return false;

            var commonResourceClassNames = new[]
            {
                "Resources", "Strings", "Messages", "Text", "Localization",
                "Res", "Lang", "Properties", "Content"
            };

            return commonResourceClassNames.Contains(className, StringComparer.OrdinalIgnoreCase) ||
                   className.EndsWith("Resources", StringComparison.OrdinalIgnoreCase) ||
                   className.EndsWith("Strings", StringComparison.OrdinalIgnoreCase);
        }

        private MissingResourceInfo AnalyzeMemberAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            try
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
                // Roslyn version in VS 2019 / .NET Framework 4.7.2 might not expose GetContainingTypeOrThis()
                // so we manually emulate it.
                var symbol = symbolInfo.Symbol;
                INamedTypeSymbol typeSymbol = symbol as INamedTypeSymbol ?? symbol?.ContainingType as INamedTypeSymbol;

                if (typeSymbol == null)
                    return null;

                var className = typeSymbol.Name;
                var propertyName = memberAccess.Name.Identifier.ValueText;

                if (!IsResourceDesignerClass(typeSymbol))
                    return null;

                var baseName = GetResourceBaseName(typeSymbol);
                if (string.IsNullOrEmpty(baseName))
                    return null;

                var memberInfo = AnalyzePropertyAccess(memberAccess, semanticModel);
                if (memberInfo != null && memberInfo.IsError)
                {
                    return new MissingResourceInfo
                    {
                        ClassName = className,
                        PropertyName = propertyName,
                        BaseName = baseName,
                        TextSpan = memberAccess.Span,
                        Node = memberAccess,
                        IsValidMissingResource = !_resxFileService.ResourceKeyExists(baseName, propertyName),
                        ErrorMessage = memberInfo.ErrorMessage
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing member access: {ex.Message}");
                return null;
            }
        }

        private bool IsResourceDesignerClass(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;

            var hasGeneratedCodeAttribute = typeSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "GeneratedCodeAttribute");

            if (!hasGeneratedCodeAttribute)
                return false;

            return _resxFileService.IsResourceDesignerClass(typeSymbol.Name);
        }

        private string GetResourceBaseName(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return null;

            var className = typeSymbol.Name;
            var files = _resxFileService.FindResXFilesByDesignerClass(className);
            
            return files.FirstOrDefault()?.BaseName ?? className;
        }

        private PropertyAccessInfo AnalyzePropertyAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            var diagnostics = semanticModel.GetDiagnostics(memberAccess.Span);
            var relevantDiagnostic = diagnostics.FirstOrDefault(d => 
                d.Id == "CS0117" || 
                d.Severity == DiagnosticSeverity.Error);

            if (relevantDiagnostic != null)
            {
                return new PropertyAccessInfo
                {
                    IsError = true,
                    ErrorMessage = relevantDiagnostic.GetMessage()
                };
            }

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol == null && symbolInfo.CandidateReason != CandidateReason.None)
            {
                return new PropertyAccessInfo
                {
                    IsError = true,
                    ErrorMessage = $"Property '{memberAccess.Name.Identifier.ValueText}' does not exist"
                };
            }

            return new PropertyAccessInfo { IsError = false };
        }

        public bool IsMissingResourceError(Diagnostic diagnostic, SyntaxNode root)
        {
            if (diagnostic.Id != "CS0117" || root == null)
                return false;

            var location = diagnostic.Location;
            if (!location.IsInSource)
                return false;

            var node = root.FindNode(location.SourceSpan);
            var memberAccess = node.AncestorsAndSelf()
                .OfType<MemberAccessExpressionSyntax>()
                .FirstOrDefault();

            return memberAccess != null && IsResourceAccess(memberAccess);
        }
    }

    internal class PropertyAccessInfo
    {
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
    }
}