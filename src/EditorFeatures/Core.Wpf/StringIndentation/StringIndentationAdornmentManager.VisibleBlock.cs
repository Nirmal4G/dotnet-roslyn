﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Microsoft.CodeAnalysis.Editor.StringIndentation
{
    internal partial class StringIndentationAdornmentManager
    {
        /// <summary>
        /// Represents the X position of the vertical line we're drawing, and the chunks of that vertical line if we
        /// need to break it up (for example if we need to jump past interpolation holes).
        /// 
        /// Forked from https://devdiv.visualstudio.com/DevDiv/_git/VS-Platform?path=/src/Editor/Text/Impl/Structure/VisibleBlock.cs
        /// </summary>
        private readonly struct VisibleBlock
        {
            public readonly double X;
            public readonly ImmutableArray<(double start, double end)> YSegments;

            private VisibleBlock(double x, ImmutableArray<(double start, double end)> ySegments)
            {
                X = x;
                YSegments = ySegments;
            }

            public static VisibleBlock? CreateVisibleBlock(
                SnapshotSpan span, ImmutableArray<SnapshotSpan> orderedHoleSpans, IWpfTextView view)
            {
                // This method assumes that we've already been mapped to the view's snapshot.
                Debug.Assert(span.Snapshot == view.TextSnapshot);

                // We want to draw the line right before the quote character.  So -1 to get that character's position.
                // Horizontally position the adornment in the center of the character.
                var bufferPosition = span.End - 1;
                if (bufferPosition < 0)
                    return null;

                var anchorPointLine = view.GetTextViewLineContainingBufferPosition(bufferPosition);
                var bounds = anchorPointLine.GetCharacterBounds(bufferPosition);
                var x = Math.Floor((bounds.Left + bounds.Right) * 0.5);

                var firstLine = view.TextViewLines.FirstVisibleLine;
                var lastLine = view.TextViewLines.LastVisibleLine;

                // Bug #557472: When performing a layout while scrolling downwards, the editor can occasionally
                // invalidate spans that are not visible. When we ask for the start and end point for adornments,
                // if the TextViewLinesCollection doesn't contain the start or end point of the GuideLineSpan, we
                // usually assume that it's the top of the first visible line, or the bottom of the last visible
                // line, respectively. If the editor invalidates an invisible span, this can result in an erroneous
                // top to bottom adornment.
                var guideLineSpanStart = span.Start;
                var guideLineSpanEnd = span.End;

                if ((guideLineSpanStart > lastLine.End) ||
                    (guideLineSpanEnd < firstLine.Start))
                {
                    return null;
                }

                var guideLineTopLine = view.TextViewLines.GetTextViewLineContainingBufferPosition(guideLineSpanStart);
                var yTop = guideLineTopLine == null ? firstLine.Top : guideLineTopLine.Bottom;

                var guideLineBottomLine = view.TextViewLines.GetTextViewLineContainingBufferPosition(guideLineSpanEnd);
                var yBottom = guideLineBottomLine == null ? lastLine.Bottom : guideLineBottomLine.Top;

                var visibleSegments = CreateVisibleSegments(view.TextViewLines, span, orderedHoleSpans, x, yTop, yBottom);

                return visibleSegments.Length == 0 ? null : new VisibleBlock(x, visibleSegments);
            }

            /// <summary>
            /// Given the horizontal position <paramref name="x"/> and the <paramref name="extent"/> to draw the
            /// vertical line through, create a set of vertical chunks to actually draw.  Multiple chunks happen when we
            /// have interpolation holes we have to skip over.
            /// </summary>
            private static ImmutableArray<(double start, double end)> CreateVisibleSegments(
                ITextViewLineCollection linesCollection,
                SnapshotSpan extent,
                ImmutableArray<SnapshotSpan> orderedHoleSpans,
                double x,
                double yTop,
                double yBottom)
            {
                using var _ = ArrayBuilder<(double start, double end)>.GetInstance(out var segments);

                // MinLineHeight must always be larger than ContinuationPadding so that no segments
                // are created for vertical spans between lines.
                const double MinLineHeight = 2.1;
                const double ContinuationPadding = 2.0;

                // Find the lines in Block's extent that are currently visible.
                // TODO: can we eliminate or reuse the allocation for this list?
                var visibleSpanTextViewLinesCollection = linesCollection.GetTextViewLinesIntersectingSpan(extent);

                var currentSegmentTop = yTop;
                var currentSegmentBottom = 0.0;

                // Iterate the visible lines of the Block's extent.
                for (var i = 0; i < visibleSpanTextViewLinesCollection.Count; i++)
                {
                    var line = visibleSpanTextViewLinesCollection[i];
                    var intersectingCharSnapshotPoint = line.GetBufferPositionFromXCoordinate(x);

                    // Three main cases for IntersectsNonWhitespaceChar:
                    // A) SV intersects a non-whitespace character. In this case we terminate
                    //    the current segment and start the next segment at the top of the following
                    //    line so that the segment does not intersect with text.
                    // B) Current line is the last visible line and is not a non-whitespace character
                    //    so we terminate the current segment at the bottom of the last visible line
                    //    to ensure that lines with an end-point that is not visible are still drawn.
                    // C) Line is not last line and does not have non-whitespace intersecting the SV
                    //    so we continue the current segment.
                    //
                    // Also, if the line would go through an interpolation hole we want to skip it as well.
                    if (IntersectsNonWhitespaceChar(intersectingCharSnapshotPoint) || IsInHole(orderedHoleSpans, line))
                    {
                        currentSegmentBottom = line.Top;

                        // Only add the structure visualizer adornment line segment if it spans at least
                        // a few pixels in height so we don't have artifacts between lines of intersecting
                        // text.
                        if ((currentSegmentBottom - currentSegmentTop) >= MinLineHeight)
                            segments.Add((currentSegmentTop, currentSegmentBottom));

                        currentSegmentTop = line.Bottom + ContinuationPadding;
                    }
                }

                // Due to mapping between versions of the Snapshots, visibleSpanTextViewLinesCollection
                // may include more lines than are actually in the block, so end at yBottom.
                currentSegmentBottom = yBottom;

                // Only add the structure visualizer adornment line segment if it spans at least
                // an entire line in height so we don't have 1 to 3 pixel artifacts between lines
                // of intersecting text.
                if ((currentSegmentBottom - currentSegmentTop) >= MinLineHeight)
                    segments.Add((currentSegmentTop, currentSegmentBottom));

                return segments.ToImmutable();
            }

            private static bool IsInHole(ImmutableArray<SnapshotSpan> orderedHoleSpans, ITextViewLine line)
                => orderedHoleSpans.BinarySearch(
                    line.Start.Position,
                    (ss, pos) => pos < ss.Start ? 1 : ss.Span.Contains(pos) ? 0 : 1) >= 0;

            private static bool IntersectsNonWhitespaceChar(SnapshotPoint? intersectingCharSnapshotPoint)
                => intersectingCharSnapshotPoint != null &&
                   !char.IsWhiteSpace(intersectingCharSnapshotPoint.Value.GetChar());
        }
    }
}
