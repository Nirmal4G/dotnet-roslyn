﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using RoslynCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.AsyncCompletion
{
    /// <summary>
    /// Contains data need to be tracked over an entire IAsyncCompletionSession completion
    /// session for various operations.
    /// </summary>
    internal sealed class CompletionSessionData
    {
        private const string SessionDataName = "RoslynCompletionSessionData";

        public bool TargetTypeFilterExperimentEnabled { get; set; }
        public bool TargetTypeFilterSelected { get; set; }
        public bool HasSuggestionItemOptions { get; set; }

        public Optional<SnapshotPoint> ExpandedItemTriggerLocation { get; set; }
        public Optional<TextSpan> CompletionListSpan { get; set; }
        public Optional<ImmutableArray<CompletionItem>> CombinedSortedList { get; set; }
        public Optional<Task<(CompletionContext, RoslynCompletionList)>> ExpandedItemsTask { get; set; }
        public Optional<ImmutableArray<char>> ExcludedCommitCharacters { get; set; }

        public static CompletionSessionData GetOrCreateSessionData(IAsyncCompletionSession session)
            => session.Properties.GetOrCreateSingletonProperty(SessionDataName, static () => new CompletionSessionData());
    }
}
