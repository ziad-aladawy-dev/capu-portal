using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CapitalUniversity.Modules.AcademicRecords.Application.Pdf;

/// <summary>
/// QuestPDF-backed transcript renderer. A single A4 document: a header, the
/// synchronized academic-summary block, then one section per requirement
/// category with Compulsory / Elective course tables.
///
/// <para>
/// QuestPDF's Community license is configured once in the static constructor (the
/// project is well under the revenue threshold the license requires). The
/// renderer is stateless and registered as a singleton.
/// </para>
/// </summary>
public sealed class QuestPdfTranscriptRenderer : ITranscriptPdfRenderer
{
    static QuestPdfTranscriptRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(TranscriptDto transcript)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Text("Academic Transcript").FontSize(20).Bold();

                    var name = string.IsNullOrWhiteSpace(transcript.StudentName)
                        ? transcript.StudentId.ToString()
                        : transcript.StudentName;
                    header.Item().Text($"Student: {name}").FontSize(9).FontColor(Colors.Grey.Darken1);

                    if (!string.IsNullOrWhiteSpace(transcript.StudentCode))
                    {
                        header.Item().Text($"Student Code: {transcript.StudentCode}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(14);

                    if (transcript.Summary is not null)
                    {
                        content.Item().Element(c => ComposeSummary(c, transcript.Summary));
                    }

                    foreach (var category in transcript.Categories)
                    {
                        content.Item().Element(c => ComposeCategory(c, category));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeSummary(IContainer container, AcademicSummaryDto summary)
    {
        container.Background(Colors.Grey.Lighten4).Padding(8).Column(col =>
        {
            col.Item().Text("Academic Summary").FontSize(13).Bold();
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"GPA: {summary.Gpa:0.00}");
                row.RelativeItem().Text($"CGPA: {summary.Cgpa:0.00}");
                row.RelativeItem().Text($"Standing: {summary.AcademicStanding}");
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Earned: {summary.EarnedCredits}");
                row.RelativeItem().Text($"Remaining: {summary.RemainingCredits}");
                row.RelativeItem().Text($"Passed Hrs: {summary.PassedHours}");
                row.RelativeItem().Text($"Failed Hrs: {summary.FailedHours}");
            });
        });
    }

    private static void ComposeCategory(IContainer container, TranscriptCategoryDto category)
    {
        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium)
                .PaddingBottom(2).Text(category.DisplayName).FontSize(13).Bold();

            ComposeCourseGroup(col, "Compulsory", category.Compulsory);
            ComposeCourseGroup(col, "Elective", category.Elective);
        });
    }

    private static void ComposeCourseGroup(ColumnDescriptor col, string title, IReadOnlyList<TranscriptCourseDto> courses)
    {
        col.Item().Text(title).Bold().FontColor(Colors.Grey.Darken2);

        if (courses.Count == 0)
        {
            col.Item().PaddingLeft(6).Text("—").FontColor(Colors.Grey.Medium);
            return;
        }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);  // code
                columns.RelativeColumn();     // title
                columns.ConstantColumn(50);   // credits
                columns.ConstantColumn(50);   // grade
                columns.ConstantColumn(80);   // status
            });

            table.Header(h =>
            {
                HeaderCell(h, "Code");
                HeaderCell(h, "Title");
                HeaderCell(h, "Credits");
                HeaderCell(h, "Grade");
                HeaderCell(h, "Status");
            });

            foreach (var course in courses)
            {
                BodyCell(table, course.CourseCode);
                BodyCell(table, course.CourseTitle);
                BodyCell(table, course.CreditHours.ToString());
                BodyCell(table, course.Grade);
                BodyCell(table, course.Status.ToString());
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(text).Bold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(text).FontSize(9);
}
