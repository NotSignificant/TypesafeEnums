using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TypesafeEnums.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DuplicateDefaultAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor RuleDuplicateDefault = new DiagnosticDescriptor(
            id: "TS0001",
            title: "Doppelter Default-Fall im Switch",
            messageFormat: "Ein Switch-Block darf nur maximal einen 'Default' enthalten",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor RuleDuplicateCase = new DiagnosticDescriptor(
            id: "TS0002",
            title: "Doppelter Case-Zustand im Switch",
            messageFormat: "Der Zustand '{0}' wurde in diesem Switch-Block bereits abgedeckt",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor RuleInvalidCaseExpression = new DiagnosticDescriptor(
            id: "TS0003",
            title: "Ungültiger Ausdruck im Case",
            messageFormat: "Im Case-Block sind nur direkt benannte Enum-Zustände erlaubt. Der Ausdruck '{0}' ist unzulässig.",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor RuleMissingCases = new DiagnosticDescriptor(
            id: "TS0004",
            title: "Fehlende Zustände bei Switch-Block",
            messageFormat: "Es wurden nicht alle Zustände abgedeckt",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor RuleEmptySwitch = new DiagnosticDescriptor(
            id: "TS0005",
            title: "Leerer Switch-Block",
            messageFormat: "Der Switch-Block ist leer",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        // For dwbug purposes only
        private static readonly DiagnosticDescriptor RuleElementInfo = new DiagnosticDescriptor(
            id: "TS0099",
            title: "Info",
            messageFormat: "{0}",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RuleDuplicateDefault, RuleDuplicateCase, RuleInvalidCaseExpression, RuleMissingCases, RuleEmptySwitch, RuleElementInfo);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
            string calledMethodName = GetMethodNameText(invocation.Expression);
            if (!(context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol))
            {
                return;
            }

            if (methodSymbol.Name != "Switch" ||
                methodSymbol.TypeArguments.Length == 0 ||
                !(methodSymbol.TypeArguments[0] is INamedTypeSymbol enumType))
            {
                return;
            }
            //            context.ReportDiagnostic(Diagnostic.Create(RuleElementInfo, invocation.GetLocation(), enumType.ToString()));
            HashSet<ISymbol> possibleCases = GetPossibleCases(enumType);

            CollectionExpressionSyntax collectionExpression = invocation.ArgumentList.Arguments
                .Select(a => a.Expression)
                .OfType<CollectionExpressionSyntax>()
                .FirstOrDefault();

            if (collectionExpression == null)
                return;

            bool defaultCase = false;
            HashSet<ISymbol> seenCaseSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            foreach (CollectionElementSyntax element in collectionExpression.Elements)
            {
                if (!(element is ExpressionElementSyntax exprElement) ||
                    !(exprElement.Expression is InvocationExpressionSyntax subInvocation))
                {
                    continue;
                }

                string subMethodName = GetMethodNameText(subInvocation.Expression);

                // Doppelter Default
                if (subMethodName == "Default")
                {
                    if (defaultCase)
                        context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateDefault, subInvocation.GetLocation()));

                    defaultCase = true;
                }

                // Case-Zustände
                if (subMethodName == "Case" && subInvocation.ArgumentList.Arguments.Count > 0)
                {
                    ExpressionSyntax firstArgExpression = subInvocation.ArgumentList.Arguments[0].Expression;

                    if (!(firstArgExpression is CollectionExpressionSyntax multiCaseCollection))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(RuleInvalidCaseExpression,
                                firstArgExpression.GetLocation(), firstArgExpression.ToString().Trim()));
                        continue;
                    }

                    foreach (CollectionElementSyntax collectionElement in multiCaseCollection.Elements)
                    {
                        if (!(collectionElement is ExpressionElementSyntax e))
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(RuleInvalidCaseExpression,
                                    collectionElement.GetLocation(), collectionElement.ToString().Trim()));
                            continue;
                        }

                        SymbolInfo memberSymbolInfo = context.SemanticModel.GetSymbolInfo(e.Expression);

                        if (memberSymbolInfo.Symbol is IFieldSymbol fieldSymbol &&
                            possibleCases.Contains(fieldSymbol))
                        {
                            if (!seenCaseSymbols.Add(fieldSymbol))
                            {
                                context.ReportDiagnostic(
                                    Diagnostic.Create(RuleDuplicateCase, e.GetLocation(), fieldSymbol.Name));
                            }
                        }
                        else
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(RuleInvalidCaseExpression,
                                    e.GetLocation(), e.Expression.ToString()));
                        }
                    }
                }
            }

            // Erst NACH der vollständigen Auswertung aller Elemente prüfen:

            if (seenCaseSymbols.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(RuleEmptySwitch, invocation.GetLocation()));
            }
            else if (!defaultCase && seenCaseSymbols.Count < possibleCases.Count)
            {
                context.ReportDiagnostic(Diagnostic.Create(RuleMissingCases, invocation.GetLocation()));
            }
        }

        private string GetMethodNameText(ExpressionSyntax expression)
        {
            if (expression is IdentifierNameSyntax idName)
            {
                return idName.Identifier.Text;
            }
            if (expression is GenericNameSyntax genericName)
            {
                return genericName.Identifier.Text;
            }
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return GetMethodNameText(memberAccess.Name);
            }
            return null;
        }

        private static HashSet<ISymbol> GetPossibleCases(INamedTypeSymbol enumType)
        {
            HashSet<ISymbol> result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            foreach (ISymbol member in enumType.GetMembers())
            {
                if (member is IFieldSymbol field &&
                    field.DeclaredAccessibility == Accessibility.Public &&
                    field.IsStatic &&
                    field.IsReadOnly)
                {
                    result.Add(field);
                }
            }

            return result;
        }
    }
}
