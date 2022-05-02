﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.AspNetCore.EmbeddedLanguages;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.ExternalAccess.AspNetCore.Internal.EmbeddedLanguages
{
    [ExportEmbeddedLanguageClassifierInternal(
        nameof(AspNetCoreEmbeddedLanguageClassifier), LanguageNames.CSharp, supportsUnannotatedAPIs: false, "Route"), Shared]
    internal class AspNetCoreEmbeddedLanguageClassifier : IEmbeddedLanguageClassifier
    {
        // Following CWTs are used to cache the  providers from projects' references,
        // so we can avoid the slow path unless there's any change to the references.
        private readonly ConditionalWeakTable<IReadOnlyList<AnalyzerReference>, StrongBox<ImmutableArray<IAspNetCoreEmbeddedLanguageClassifier>>> _analyzerReferencesToClassifiersMap = new();
        private readonly ConditionalWeakTable<AnalyzerReference, ClassifierExtensionProvider> _analyzerReferenceToProviderMap = new();

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public AspNetCoreEmbeddedLanguageClassifier()
        {
        }

        public void RegisterClassifications(EmbeddedLanguageClassificationContext context)
        {
            if (context.Project is null)
                return;

            var classifiers = GetClassifiers(context.Project);
            var aspContext = new AspNetCoreEmbeddedLanguageClassificationContext(context);
            foreach (var classifier in classifiers)
                classifier.RegisterClassifications(aspContext);
        }

        private ImmutableArray<IAspNetCoreEmbeddedLanguageClassifier> GetClassifiers(Project project)
        {
            if (_analyzerReferencesToClassifiersMap.TryGetValue(project.AnalyzerReferences, out var classifiers))
                return classifiers.Value;

            return GetClassifiersSlow(project);

            ImmutableArray<IAspNetCoreEmbeddedLanguageClassifier> GetClassifiersSlow(Project project)
                => _analyzerReferencesToClassifiersMap.GetValue(project.AnalyzerReferences, _ => new(ComputeClassifiers(project))).Value;

            ImmutableArray<IAspNetCoreEmbeddedLanguageClassifier> ComputeClassifiers(Project project)
            {
                using var _ = ArrayBuilder<IAspNetCoreEmbeddedLanguageClassifier>.GetInstance(out var result);

                foreach (var reference in project.AnalyzerReferences)
                {
                    var provider = _analyzerReferenceToProviderMap.GetValue(reference, static r => new ClassifierExtensionProvider(r));
                    result.AddRange(provider.GetExtensions(project.Language));
                }

                return result.ToImmutable();
            }
        }
    }
}
