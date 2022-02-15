﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Completion.Providers.Snippets
{
    internal interface ISnippetService : ILanguageService
    {
        Task<ImmutableArray<SnippetData?>> GetSnippetsAsync(Document document, int position, CancellationToken cancellationToken);
        ISnippetProvider? GetSnippetProvider(SnippetData data);
        Task<TextSpan> GetInvocationSpanAsync(Document document, int position, CancellationToken cancellationToken);
    }
}
