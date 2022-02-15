﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Completion.Providers.Snippets
{
    internal class SnippetCompletionItem
    {
        public static CompletionItem Create(
            string displayText,
            string displayTextSuffix,
            TextSpan span,
            Glyph glyph)
        {
            var props = ImmutableDictionary<string, string>.Empty
                .Add("TokenSpanStart", span.Start.ToString())
                .Add("TokenSpanEnd", span.End.ToString());

            return CommonCompletionItem.Create(
                displayText: displayText,
                displayTextSuffix: displayTextSuffix,
                glyph: glyph,
                properties: props,
                isComplexTextEdit: true,
                rules: CompletionItemRules.Default);
        }

        public static int GetTokenSpanStart(CompletionItem item)
        {
            if (item.Properties.TryGetValue("TokenSpanStart", out var text)
                && int.TryParse(text, out var number))
            {
                return number;
            }

            return 0;
        }

        public static int GetTokenSpanEnd(CompletionItem item)
        {
            if (item.Properties.TryGetValue("TokenSpanEnd", out var text)
                && int.TryParse(text, out var number))
            {
                return number;
            }

            return 0;
        }
    }
}
