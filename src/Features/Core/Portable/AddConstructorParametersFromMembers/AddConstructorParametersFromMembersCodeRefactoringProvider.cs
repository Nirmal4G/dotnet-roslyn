﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Features.Intents;
using Microsoft.CodeAnalysis.GenerateFromMembers;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.AddConstructorParametersFromMembers
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, LanguageNames.VisualBasic,
        Name = PredefinedCodeRefactoringProviderNames.AddConstructorParametersFromMembers), Shared]
    [ExtensionOrder(After = PredefinedCodeRefactoringProviderNames.GenerateConstructorFromMembers,
                    Before = PredefinedCodeRefactoringProviderNames.GenerateOverrides)]
    [IntentProvider(WellKnownIntents.AddConstructorParameter, LanguageNames.CSharp)]
    internal partial class AddConstructorParametersFromMembersCodeRefactoringProvider : AbstractGenerateFromMembersCodeRefactoringProvider, IIntentProvider
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public AddConstructorParametersFromMembersCodeRefactoringProvider()
        {
        }

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var (document, textSpan, cancellationToken) = context;
            if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
            {
                return;
            }

            var actions = await AddConstructorParametersFromMembersAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
            context.RegisterRefactorings(actions);
        }

        public static async Task<ImmutableArray<CodeAction>> AddConstructorParametersFromMembersAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Refactoring_GenerateFromMembers_AddConstructorParametersFromMembers, cancellationToken))
            {
                var info = await GetSelectedMemberInfoAsync(
                    document,
                    textSpan,
                    allowPartialSelection: true,
                    cancellationToken).ConfigureAwait(false);

                if (info != null)
                {
                    var state = await State.GenerateAsync(info.SelectedMembers, document, cancellationToken).ConfigureAwait(false);
                    if (state?.ConstructorCandidates != null && !state.ConstructorCandidates.IsEmpty)
                    {
                        return CreateCodeActions(document, state);
                    }
                }

                return default;
            }
        }

        private static ImmutableArray<CodeAction> CreateCodeActions(Document document, State state)
        {
            using var _0 = ArrayBuilder<CodeAction>.GetInstance(out var result);
            var containingType = state.ContainingType;
            if (state.ConstructorCandidates.Length == 1)
            {
                // There will be at most 2 suggested code actions, so no need to use sub menus
                var constructorCandidate = state.ConstructorCandidates[0];
                if (CanHaveRequiredParameters(state.ConstructorCandidates[0].MissingParameters))
                {
                    result.Add(new AddConstructorParametersCodeAction(
                        document,
                        constructorCandidate,
                        containingType,
                        constructorCandidate.MissingParameters,
                        useSubMenuName: false));
                }

                result.Add(GetOptionalContructorParametersCodeAction(
                    document,
                    constructorCandidate,
                    containingType,
                    useSubMenuName: false));
            }
            else
            {
                // Create sub menus for suggested actions, one for required parameters and one for optional parameters
                using var _1 = ArrayBuilder<CodeAction>.GetInstance(out var requiredParameterCodeActions);
                using var _2 = ArrayBuilder<CodeAction>.GetInstance(out var optionalParameterCodeActions);
                foreach (var constructorCandidate in state.ConstructorCandidates)
                {
                    if (CanHaveRequiredParameters(constructorCandidate.Constructor.Parameters))
                    {
                        requiredParameterCodeActions.Add(new AddConstructorParametersCodeAction(
                            document,
                            constructorCandidate,
                            containingType,
                            constructorCandidate.MissingParameters,
                            useSubMenuName: true));
                    }

                    optionalParameterCodeActions.Add(GetOptionalContructorParametersCodeAction(
                        document,
                        constructorCandidate,
                        containingType,
                        useSubMenuName: true));
                }

                if (requiredParameterCodeActions.Count > 0)
                {
                    result.Add(new CodeAction.CodeActionWithNestedActions(
                        FeaturesResources.Add_parameter_to_constructor,
                        requiredParameterCodeActions.ToImmutable(),
                        isInlinable: false));
                }

                result.Add(new CodeAction.CodeActionWithNestedActions(
                    FeaturesResources.Add_optional_parameter_to_constructor,
                    optionalParameterCodeActions.ToImmutable(),
                    isInlinable: false));
            }

            return result.ToImmutable();

            // local functions
            static bool CanHaveRequiredParameters(ImmutableArray<IParameterSymbol> parameters)
                   => parameters.Length == 0 || !parameters.Last().IsOptional;

            static CodeAction GetOptionalContructorParametersCodeAction(Document document, ConstructorCandidate constructorCandidate, INamedTypeSymbol containingType, bool useSubMenuName)
            {
                var missingOptionalParameters = constructorCandidate.MissingParameters.SelectAsArray(
                    p => CodeGenerationSymbolFactory.CreateParameterSymbol(
                        attributes: default,
                        refKind: p.RefKind,
                        isParams: p.IsParams,
                        type: p.Type,
                        name: p.Name,
                        isOptional: true,
                        hasDefaultValue: true));

                return new AddConstructorParametersCodeAction(
                    document, constructorCandidate, containingType, missingOptionalParameters, useSubMenuName);
            }
        }

        public async Task<ImmutableArray<IntentProcessorResult>> ComputeIntentAsync(Document priorDocument, TextSpan priorSelection, Document currentDocument, string serializedIntentData, CancellationToken cancellationToken)
        {
            var actions = await AddConstructorParametersFromMembersAsync(priorDocument, priorSelection, cancellationToken).ConfigureAwait(false);
            if (actions.IsEmpty)
            {
                return ImmutableArray<IntentProcessorResult>.Empty;
            }

            // This provider generates nested actions, so we need to flatten them before we compute the edits.
            actions = actions.SelectMany(a => GetNestedActions(a)).ToImmutableArray();

            using var _ = ArrayBuilder<IntentProcessorResult>.GetInstance(out var results);

            foreach (var action in actions)
            {
                var addParamAction = (AddConstructorParametersCodeAction)action;

                // This provider only returns CodeActions with a single ApplyChangesOperation
                var operations = await addParamAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
                var applyChangesOperation = operations.Single() as ApplyChangesOperation;
                Contract.ThrowIfNull(applyChangesOperation);

                var intent = new IntentProcessorResult(applyChangesOperation.ChangedSolution, addParamAction.Title, addParamAction.ActionName);
                results.Add(intent);
            }

            return results.ToImmutable();

            ImmutableArray<CodeAction> GetNestedActions(CodeAction action)
                => action.NestedCodeActions.IsEmpty ? ImmutableArray.Create(action) : action.NestedCodeActions;
        }
    }
}
