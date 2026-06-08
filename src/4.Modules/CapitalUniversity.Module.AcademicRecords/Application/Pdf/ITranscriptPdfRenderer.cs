using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

namespace CapitalUniversity.Modules.AcademicRecords.Application.Pdf;

/// <summary>
/// Renders a fully-built <see cref="TranscriptDto"/> into a PDF document. Kept
/// behind an interface so the transcript service stays free of any PDF-engine
/// dependency and the renderer can be swapped / faked in tests.
/// </summary>
public interface ITranscriptPdfRenderer
{
    /// <summary>Render the transcript to a complete PDF document's bytes.</summary>
    byte[] Render(TranscriptDto transcript);
}
