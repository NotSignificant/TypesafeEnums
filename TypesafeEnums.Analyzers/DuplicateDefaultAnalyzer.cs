using System;
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
            messageFormat: "Ein Switch-Block darf nur maximal einen 'Default'-Fall enthalten",
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
        private static readonly DiagnosticDescriptor RuleElementInfo = new DiagnosticDescriptor(
            id: "LI0003",
            title: "Info",
            messageFormat: "{0}",
            category: "Logic",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RuleDuplicateDefault, RuleDuplicateCase, RuleInvalidCaseExpression, RuleElementInfo);

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

            if (calledMethodName == "Switch")
            {
                CollectionExpressionSyntax collectionExpression = invocation.ArgumentList.Arguments
                    .Select(a => a.Expression)
                    .OfType<CollectionExpressionSyntax>()
                    .FirstOrDefault();

                if (collectionExpression != null)
                {
                    int defaultCount = 0;
                    HashSet<ISymbol> seenCaseSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

                    foreach (CollectionElementSyntax element in collectionExpression.Elements)
                    {
                        if (element is ExpressionElementSyntax exprElement &&
                            exprElement.Expression is InvocationExpressionSyntax subInvocation)
                        {
                            string subMethodName = GetMethodNameText(subInvocation.Expression);

                            // --- CHECK 1: DOPPELTER DEFAULT-FALL ---
                            if (subMethodName == "Default")
                            {
                                defaultCount++;
                                if (defaultCount > 1)
                                {
                                    context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateDefault, subInvocation.GetLocation()));
                                }
                            }

                            // --- CHECK 2: DOPPELTER CASE-ZUSTAND ---
                            if (subMethodName == "Case" && subInvocation.ArgumentList.Arguments.Count > 0)
                            {
                                // Korrekter Index-Zugriff auf das erste Argument des Case-Aufrufs
                                ExpressionSyntax firstArgExpression = subInvocation.ArgumentList.Arguments[0].Expression;

                                if (firstArgExpression is CollectionExpressionSyntax multiCaseCollection)
                                {
                                    foreach (CollectionElementSyntax collectionElement in multiCaseCollection.Elements)
                                    {
                                        if (collectionElement is ExpressionElementSyntax e)
                                        {
                                            SymbolInfo memberSymbolInfo = context.SemanticModel.GetSymbolInfo(e.Expression);
//                                            context.ReportDiagnostic(Diagnostic.Create(RuleElementInfo, e.GetLocation(), memberSymbolInfo.Symbol?.ToDisplayString()));

                                            // DIE ULTIMATIVE WHITELIST: Es MUSS ein Feld sein, das public, static und readonly ist!
                                            if (memberSymbolInfo.Symbol is IFieldSymbol fieldSymbol &&
                                                fieldSymbol.DeclaredAccessibility == Accessibility.Public &&
                                                fieldSymbol.IsStatic &&
                                                fieldSymbol.IsReadOnly)
                                            {
                                                if (!seenCaseSymbols.Add(fieldSymbol))
                                                {
                                                    context.ReportDiagnostic(Diagnostic.Create(RuleDuplicateCase, e.GetLocation(), fieldSymbol.Name));
                                                }
                                            }
                                            else
                                            {
                                                // Alles, was kein public static readonly Feld ist (z.B. Methoden wie .Values()), erzeugt TS0003
                                                context.ReportDiagnostic(Diagnostic.Create(RuleInvalidCaseExpression, collectionElement.GetLocation(), e.Expression.ToString()));
                                            }
                                        }
                                        else
                                        {
                                            context.ReportDiagnostic(Diagnostic.Create(RuleInvalidCaseExpression, collectionElement.GetLocation(), collectionElement.ToString().Trim()));
                                        }
                                    }
                                }
                                else
                                {
                                    // Falls im Case überhaupt kein [...] Array steht, sondern direkt Enum2.Values() aufgerufen wird
                                    context.ReportDiagnostic(Diagnostic.Create(RuleInvalidCaseExpression, firstArgExpression.GetLocation(), firstArgExpression.ToString().Trim()));
                                }
                            }
                        }
                    }
                }
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
    }
}
