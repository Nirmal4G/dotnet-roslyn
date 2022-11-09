﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.Editor.Implementation.Adornments;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InlineDiagnostics
{
    internal class InlineDiagnosticsAdornmentManager : AbstractAdornmentManager<InlineDiagnosticsTag>
    {
        private readonly IClassificationTypeRegistryService _classificationRegistryService;
        private readonly IClassificationFormatMap _formatMap;
        private readonly ITagAggregator<IEndOfLineAdornmentTag> _endLineTagAggregator;
        private readonly IGlobalOptionService _globalOptions;

        public InlineDiagnosticsAdornmentManager(
            IThreadingContext threadingContext,
            IWpfTextView textView,
            IViewTagAggregatorFactoryService tagAggregatorFactoryService,
            IAsynchronousOperationListener asyncListener,
            string adornmentLayerName,
            IClassificationFormatMapService classificationFormatMapService,
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IGlobalOptionService globalOptions)
            : base(threadingContext, textView, tagAggregatorFactoryService, asyncListener, adornmentLayerName)
        {
            _classificationRegistryService = classificationTypeRegistryService;
            _formatMap = classificationFormatMapService.GetClassificationFormatMap(textView);
            _formatMap.ClassificationFormatMappingChanged += OnClassificationFormatMappingChanged;
            _globalOptions = globalOptions;
            TextView.ViewportWidthChanged += TextView_ViewportWidthChanged;

            _endLineTagAggregator = tagAggregatorFactoryService.CreateTagAggregator<IEndOfLineAdornmentTag>(textView);
            _endLineTagAggregator.BatchedTagsChanged += EndLineTagAggregator_BatchedTagsChanged;
        }

        /// <summary>
        /// Getting all tags changed events and removing all inline diagnostics to be redrawn
        /// based on if they intersect with any IEndOfLineAdornmentTags after the layout change
        /// has completed.
        /// </summary>
        private void EndLineTagAggregator_BatchedTagsChanged(object sender, BatchedTagsChangedEventArgs e)
        {
            TextView.QueuePostLayoutAction(() =>
            {
                var allSpans = e.Spans.SelectMany(span => span.GetSpans(TextView.TextBuffer));
                UpdateSpans_CallOnlyOnUIThread(new NormalizedSnapshotSpanCollection(allSpans), removeOldTags: true);
            });
        }

        /// <summary>
        /// Need to remove the tags if they intersect with the editor view, but only if the option
        /// to place the tags at the end of the editor is selected.
        /// </summary>
        private void TextView_ViewportWidthChanged(object sender, EventArgs e)
        {
            // this method should only run on UI thread as we do WPF here.
            Contract.ThrowIfFalse(TextView.VisualElement.Dispatcher.CheckAccess());

            if (AdornmentLayer is null)
            {
                return;
            }

            var document = TextView.TextBuffer.AsTextContainer()?.GetOpenDocumentInCurrentContext();
            if (document is null)
            {
                AdornmentLayer.RemoveAllAdornments();
                return;
            }

            var option = _globalOptions.GetOption(InlineDiagnosticsOptions.Location, document.Project.Language);
            if (option == InlineDiagnosticsLocations.PlacedAtEndOfEditor)
            {
                var normalizedCollectionSpan = new NormalizedSnapshotSpanCollection(TextView.TextViewLines.FormattedSpan);
                UpdateSpans_CallOnlyOnUIThread(normalizedCollectionSpan, removeOldTags: true);
            }
        }

        private void OnClassificationFormatMappingChanged(object sender, EventArgs e)
        {
            // this method should only run on UI thread as we do WPF here.
            Contract.ThrowIfFalse(TextView.VisualElement.Dispatcher.CheckAccess());

            if (AdornmentLayer is not null)
            {
                foreach (var element in AdornmentLayer.Elements)
                {
                    var tag = (InlineDiagnosticsTag)element.Tag;
                    var classificationType = _classificationRegistryService.GetClassificationType(InlineDiagnosticsTag.GetClassificationId(tag.ErrorType));
                    var format = GetFormat(classificationType);
                    InlineDiagnosticsTag.UpdateColor(format, element.Adornment);
                }
            }
        }

        private TextFormattingRunProperties GetFormat(IClassificationType classificationType)
        {
            return _formatMap.GetTextProperties(classificationType);
        }

        /// <summary>
        /// Iterates through the mapping of line number to span and draws the diagnostic in the appropriate position on the screen,
        /// as well as adding the tag to the adornment layer.
        /// </summary>
        protected override void AddAdornmentsToAdornmentLayer_CallOnlyOnUIThread(NormalizedSnapshotSpanCollection changedSpanCollection)
        {
            // this method should only run on UI thread as we do WPF here.
            Contract.ThrowIfFalse(TextView.VisualElement.Dispatcher.CheckAccess());
            if (changedSpanCollection.IsEmpty())
            {
                return;
            }

            var viewLines = TextView.TextViewLines;
            using var _ = PooledDictionary<IWpfTextViewLine, IMappingTagSpan<InlineDiagnosticsTag>>.GetInstance(out var map);

            // First loop iterates through the snap collection and determines if an inline diagnostic can be drawn.
            // Creates a mapping of the view line to the IMappingTagSpan with getting the first error that appears
            // on the line if there are multiple.
            foreach (var changedSpan in changedSpanCollection)
            {
                if (!viewLines.IntersectsBufferSpan(changedSpan))
                {
                    continue;
                }

                var tagSpans = TagAggregator.GetTags(changedSpan);
                foreach (var tagMappingSpan in tagSpans)
                {
                    if (!ShouldDrawTag(changedSpan, tagMappingSpan, out var viewLine))
                    {
                        continue;
                    }

                    // If the line does not have an associated tagMappingSpan and changedSpan, then add the first one.
                    if (!map.TryGetValue(viewLine, out var value))
                    {
                        map.Add(viewLine, tagMappingSpan);
                    }
                    else if (value.Tag.ErrorType is not PredefinedErrorTypeNames.SyntaxError && tagMappingSpan.Tag.ErrorType is PredefinedErrorTypeNames.SyntaxError)
                    {
                        // Draw the first instance of an error, if what is stored in the map at a specific line is
                        // not an error, then replace it. Otherwise, just get the first warning on the line.
                        map[viewLine] = tagMappingSpan;
                    }
                }
            }

            // Second loop iterates through the map to go through and create the graphics that is being drawn
            // on the canvas as well adding the tag to the Inline Diagnostics adornment layer.
            foreach (var (lineView, tagMappingSpan) in map)
            {
                // Looking for IEndOfLineTags and seeing if they exist on the same line as where the
                // diagnostic would be drawn. If they are the same, then we do not want to draw
                // the diagnostic.
                var obstructingTags = _endLineTagAggregator.GetTags(lineView.Extent);
                if (obstructingTags.Where(tag => tag.Tag.Type is not "Inline Diagnostics").Any())
                {
                    continue;
                }

                var tag = tagMappingSpan.Tag;
                var classificationType = _classificationRegistryService.GetClassificationType(InlineDiagnosticsTag.GetClassificationId(tag.ErrorType));

                // Pass in null! because the geometry is unused for drawing anything for Inline Diagnostics
                var graphicsResult = tag.GetGraphics(TextView, unused: null!, GetFormat(classificationType));

                var visualElement = graphicsResult.VisualElement;

                // Only place the diagnostics if the diagnostic would not intersect with the editor window
                if (lineView.Right >= TextView.ViewportWidth - visualElement.DesiredSize.Width)
                {
                    graphicsResult.Dispose();
                    continue;
                }

                Canvas.SetLeft(visualElement,
                    tag.Location == InlineDiagnosticsLocations.PlacedAtEndOfCode ? lineView.Right :
                    tag.Location == InlineDiagnosticsLocations.PlacedAtEndOfEditor ? TextView.ViewportRight - visualElement.DesiredSize.Width :
                    throw ExceptionUtilities.UnexpectedValue(tag.Location));

                Canvas.SetTop(visualElement, lineView.Bottom - visualElement.DesiredSize.Height);

                AdornmentLayer.AddAdornment(
                    behavior: AdornmentPositioningBehavior.TextRelative,
                    visualSpan: lineView.Extent,
                    tag: tag,
                    adornment: visualElement,
                    removedCallback: delegate { graphicsResult.Dispose(); });
            }
        }
    }
}
