using QuestPDF.Fluent;
using ProGlassAutomation.Services.Export.Core;

namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// Contract each module implements to render its PDF content.
    /// The engine calls RenderContent(content) with a QuestPDF ColumnDescriptor
    /// that has already been set up with page size, margins, letterhead,
    /// and footer.
    ///
    /// Layouts should:
    /// - Add sections via content.Item().Element(...)
    /// - Use shared renderers (SectionHeaderRenderer, LetterheadRenderer, etc.)
    /// - Never call Page(), Header(), Footer() — engine owns page structure
    /// </summary>
    public interface IPdfLayout : IDocumentLayout
    {
        /// <summary>
        /// Optional — if null, engine renders no letterhead block.
        /// Return a populated LetterheadInfo to show the shared header.
        /// </summary>
        LetterheadInfo? Letterhead { get; }

        /// <summary>
        /// Page orientation. Default A4 portrait.
        /// </summary>
        PageLayout PageLayout { get; }

        /// <summary>
        /// Renders the body content inside the engine-managed page.
        /// This method is called once. Multi-page layouts should let
        /// QuestPDF's natural flow handle pagination.
        /// </summary>
        void RenderContent(ColumnDescriptor content);
    }
}