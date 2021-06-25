﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.Progression
{
    internal static class ProgressionOptions
    {
        public static readonly Option2<bool> SearchUsingNavigateToEngine = new(
            nameof(ProgressionOptions), nameof(SearchUsingNavigateToEngine), defaultValue: false,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.Specific.ProgressionOptions.SearchUsingNavigateToEngine"));
    }
}
