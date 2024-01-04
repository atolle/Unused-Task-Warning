using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Lindhart.Analyser.MissingAwaitWarning
{
    [DiagnosticAnalyzer( LanguageNames.CSharp )]
    public class LindhartAnalyserMissingAwaitWarningAnalyzer : DiagnosticAnalyzer
    {
        public const string StrictRuleId = "LindhartAnalyserMissingAwaitWarningStrict";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString StrictTitle = new LocalizableResourceString( nameof( Resources.StandardRuleTitle ), Resources.ResourceManager, typeof( Resources ) );
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString( nameof( Resources.AnalyzerMessageFormat ), Resources.ResourceManager, typeof( Resources ) );
        private static readonly LocalizableString Description = new LocalizableResourceString( nameof( Resources.AnalyzerDescription ), Resources.ResourceManager, typeof( Resources ) );
        private const string Category = "UnintentionalUsage";

        private static readonly DiagnosticDescriptor StrictRule = new DiagnosticDescriptor( StrictRuleId, StrictTitle, MessageFormat, Category, DiagnosticSeverity.Error, true, Description );

        private static readonly string[] AwaitableTypes = new[]
        {
            typeof(Task).FullName,
            typeof(Task<>).FullName,
            typeof(ConfiguredTaskAwaitable).FullName,
            typeof(ConfiguredTaskAwaitable<>).FullName,
            "System.Threading.Tasks.ValueTask", // Type not available in .net standard 1.3
            typeof(ValueTask<>).FullName,
            "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable", // Type not available in .net standard
            typeof(ConfiguredValueTaskAwaitable<>).FullName
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create( StrictRule );

        public override void Initialize( AnalysisContext context )
        {
            context.RegisterSyntaxNodeAction( AnalyseSymbolNode, SyntaxKind.InvocationExpression );
         }

        private void AnalyseSymbolNode( SyntaxNodeAnalysisContext syntaxNodeAnalysisContext )
        {
            if ( syntaxNodeAnalysisContext.Node is InvocationExpressionSyntax node )
            {
                var symbolInfo = syntaxNodeAnalysisContext
                    .SemanticModel
                    .GetSymbolInfo( node.Expression, syntaxNodeAnalysisContext.CancellationToken );

                if ( ( symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() )
                    is IMethodSymbol methodSymbol )
                {
                    AnalyseParentNode( syntaxNodeAnalysisContext, node, methodSymbol );
                }
            }
        }

        private static void AnalyseParentNode( SyntaxNodeAnalysisContext syntaxNodeAnalysisContext, SyntaxNode node, IMethodSymbol methodSymbol )
        {
            switch ( node.Parent )
            {
                //// Checks if a task is not awaited when the task itself is not assigned to a variable.
                //case ExpressionStatementSyntax _:
                //    // Check the method return type against all the known awaitable types.
                //    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                //    {
                //        var diagnostic = Diagnostic.Create(StandardRule, node.GetLocation(), methodSymbol.ToDisplayString());

                //        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                //    }

                //    break;

                //// Checks if a task is not awaited in lambdas.
                //// The following two lines have been commented out, since they gave problems
                //// case AnonymousFunctionExpressionSyntax _:
                //// case ArrowExpressionClauseSyntax _:
                //// Checks if a task is not awaited when the task itself is assigned to a variable.
                //case EqualsValueClauseSyntax _:

                //    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                //    {
                //        var diagnostic = Diagnostic.Create(StrictRule, node.GetLocation(), methodSymbol.ToDisplayString());

                //        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                //    }

                //    break;

                //case AssignmentExpressionSyntax _:

                //    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                //    {
                //        var diagnostic = Diagnostic.Create(StrictRule, node.GetLocation(), methodSymbol.ToDisplayString());

                //        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                //    }

                //    break;

                //case ReturnStatementSyntax _:

                //    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                //    {
                //        var diagnostic = Diagnostic.Create(StrictRule, node.GetLocation(), methodSymbol.ToDisplayString());

                //        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                //    }

                //    break;

                //case ParameterListSyntax _:

                //    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                //    {
                //        var diagnostic = Diagnostic.Create(StrictRule, node.GetLocation(), methodSymbol.ToDisplayString());

                //        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                //    }

                //    break;

                // If the conditional expression, we check recursively
                case ConditionalAccessExpressionSyntax _:

                    AnalyseParentNode( syntaxNodeAnalysisContext, node.Parent, methodSymbol );

                    break;

                // Awaited expressions don't interest us
                case AwaitExpressionSyntax _:

                    break;

                default:
                    if (EqualsType(methodSymbol.ReturnType, syntaxNodeAnalysisContext.SemanticModel, AwaitableTypes))
                    {
                        var diagnostic = Diagnostic.Create(StrictRule, node.GetLocation(), methodSymbol.ToDisplayString());

                        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                    }

                    break;
            }
        }

        /// <summary>
        /// Checks if the <paramref name="typeSymbol"/> is one of the types specified
        /// </summary>
        /// <param name="typeSymbol"></param>
        /// <param name="semanticModel">Semantic Model of the current context</param>
        /// <param name="types">List of parameters that should match the symbol's type</param>
        /// <returns></returns>
        private static bool EqualsType( ITypeSymbol typeSymbol, SemanticModel semanticModel, params string[] types )
        {
            var namedTypeSymbols = types.Select( x => semanticModel.Compilation.GetTypeByMetadataName( x ) );

            var namedSymbol = typeSymbol as INamedTypeSymbol;
            if ( namedSymbol == null )
                return false;

            if ( namedSymbol.IsGenericType )
                return namedTypeSymbols.Any( t => namedSymbol.ConstructedFrom.Equals( t ) );

            return namedTypeSymbols.Any( t => typeSymbol.Equals( t ) );
        }
    }
}
