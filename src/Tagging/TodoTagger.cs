using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AsyncToolWindowSample.Tagging
{
    // ====================================================================== //
    //  TodoTaggerProvider — MEF factory                                       //
    // ====================================================================== //

    /// <summary>
    /// MEF provider that creates a <see cref="TodoTagger"/> for every text buffer.
    /// Highlights every occurrence of "TODO" (case-insensitive) with the built-in
    /// "MarkerFormatDefinition/HighlightedReference" marker style.
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]                   // applies to all text-based files
    [TagType(typeof(TextMarkerTag))]
    [Name("AsyncToolWindowSample.TodoTagger")]
    internal sealed class TodoTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            // Buffer.Properties is used to share a single tagger per buffer
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new TodoTagger(buffer)) as ITagger<T>;
        }
    }

    // ====================================================================== //
    //  TodoTagger                                                              //
    // ====================================================================== //

    /// <summary>
    /// Scans the buffer for "TODO" tokens and emits <see cref="TextMarkerTag"/>
    /// spans so the editor highlights them.
    /// </summary>
    internal sealed class TodoTagger : ITagger<TextMarkerTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private bool _disposed;

        // The keyword to highlight (case-insensitive)
        private const string Keyword = "TODO";

        public TodoTagger(ITextBuffer buffer)
        {
            _buffer         = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _buffer.Changed += OnBufferChanged;
        }

        // ------------------------------------------------------------------ //
        //  ITagger<TextMarkerTag>                                              //
        // ------------------------------------------------------------------ //

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(
            NormalizedSnapshotSpanCollection spans)
        {
            if (_disposed || spans.Count == 0) yield break;

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            string        fullText = snapshot.GetText();

            int start = 0;
            while (true)
            {
                int idx = fullText.IndexOf(Keyword, start,
                              StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;

                var span = new SnapshotSpan(snapshot, idx, Keyword.Length);

                // Only emit tags that overlap the requested spans
                if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(span)))
                {
                    yield return new TagSpan<TextMarkerTag>(span,
                        new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));
                }

                start = idx + Keyword.Length;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        // ------------------------------------------------------------------ //
        //  Buffer change → invalidate                                          //
        // ------------------------------------------------------------------ //

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_disposed) return;

            // Invalidate the whole file so GetTags is re-run
            var snapshot  = _buffer.CurrentSnapshot;
            var wholeFile = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(wholeFile));
        }

        // ------------------------------------------------------------------ //
        //  IDisposable                                                         //
        // ------------------------------------------------------------------ //

        public void Dispose()
        {
            if (_disposed) return;
            _disposed        = true;
            _buffer.Changed -= OnBufferChanged;
        }
    }
}
