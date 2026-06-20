using Accountrack.SharedKernel.Pdf;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using Microsoft.AspNetCore.Http;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Accountrack.Web.Common.Pdf;

/// <summary>
/// Renders a <see cref="PdfDocument"/> to a styled, modern business-document PDF via QuestPDF
/// (ADR-0031). Clean layout: brand-accent title + table header, generous whitespace, right-aligned
/// tabular money, an emphasised grand total. Currency-/number-formatting is done by the caller.
/// </summary>
public static class PdfRenderer
{
    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Faint = "#9CA3AF";
    private const string Hair = "#E5E7EB";
    private const string Zebra = "#F9FAFB";

    public static byte[] Render(PdfDocument model)
    {
        var accent = model.AccentHex;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink).LineHeight(1.25f));

                page.Header().Element(e => Header(e, model, accent));
                page.Content().PaddingTop(18).Element(e => Body(e, model, accent));
                page.Footer().Element(e => Footer(e, model));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, PdfDocument m, string accent)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(m.Seller.Name).FontSize(16).Bold().FontColor(accent);
                foreach (var line in m.Seller.Lines)
                {
                    col.Item().Text(line).FontSize(9).FontColor(Muted);
                }
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(m.Title.ToUpperInvariant()).FontSize(24).Bold().FontColor(accent);
                col.Item().AlignRight().Text(m.Number).FontSize(11).FontColor(Muted);
            });
        });
    }

    private static void Body(IContainer container, PdfDocument m, string accent)
    {
        container.Column(col =>
        {
            col.Spacing(16);

            // Bill-to + meta
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(m.BillToLabel.ToUpperInvariant()).FontSize(8).Bold().FontColor(Faint).LetterSpacing(0.05f);
                    c.Item().PaddingTop(3).Text(m.BillTo.Name).Bold();
                    foreach (var line in m.BillTo.Lines)
                    {
                        c.Item().Text(line).FontSize(9).FontColor(Muted);
                    }
                });

                row.ConstantItem(220).Column(c =>
                {
                    foreach (var field in m.Meta)
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text(field.Label).FontSize(9).FontColor(Muted);
                            r.RelativeItem().AlignRight().Text(field.Value).FontSize(9).SemiBold();
                        });
                    }
                });
            });

            // Line items
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(2.2f);
                });

                table.Header(header =>
                {
                    void Head(string text, bool right = false)
                    {
                        var cell = header.Cell().Background(accent).PaddingVertical(6).PaddingHorizontal(8);
                        (right ? cell.AlignRight() : cell).Text(text).FontColor("#FFFFFF").FontSize(9).SemiBold();
                    }

                    Head("Description");
                    Head("Qty", right: true);
                    Head("Unit price", right: true);
                    Head("Tax", right: true);
                    Head("Amount", right: true);
                });

                var i = 0;
                foreach (var line in m.Lines)
                {
                    var bg = i++ % 2 == 1 ? Zebra : "#FFFFFF";
                    IContainer Cell(bool right = false)
                    {
                        var cell = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Hair).PaddingVertical(5).PaddingHorizontal(8);
                        return right ? cell.AlignRight() : cell;
                    }

                    Cell().Text(line.Description);
                    Cell(true).Text(line.Quantity);
                    Cell(true).Text(line.UnitPrice);
                    Cell(true).Text(line.Tax).FontColor(Muted);
                    Cell(true).Text(line.Amount);
                }
            });

            // Totals
            col.Item().AlignRight().Width(260).Column(c =>
            {
                foreach (var total in m.Totals)
                {
                    if (total.Emphasis)
                    {
                        c.Item().PaddingTop(4).BorderTop(1).BorderColor(accent).PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text(total.Label).Bold().FontColor(accent);
                            r.RelativeItem().AlignRight().Text(total.Value).FontSize(13).Bold().FontColor(accent);
                        });
                    }
                    else
                    {
                        c.Item().PaddingVertical(2).Row(r =>
                        {
                            r.RelativeItem().Text(total.Label).FontColor(Muted);
                            r.RelativeItem().AlignRight().Text(total.Value).SemiBold();
                        });
                    }
                }
            });

            if (!string.IsNullOrWhiteSpace(m.Notes))
            {
                col.Item().PaddingTop(6).Column(c =>
                {
                    c.Item().Text("NOTES").FontSize(8).Bold().FontColor(Faint).LetterSpacing(0.05f);
                    c.Item().PaddingTop(3).Text(m.Notes!).FontSize(9).FontColor(Muted);
                });
            }
        });
    }

    private static void Footer(IContainer container, PdfDocument m)
    {
        container.BorderTop(0.5f).BorderColor(Hair).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(m.FooterNote ?? string.Empty).FontSize(8).FontColor(Faint);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Faint));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Turns a successful document model into a downloadable PDF response.</summary>
    public static async Task<IResult> File(Task<Result<PdfDocument>> task, string fileName)
    {
        var result = await task;
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.File(Render(result.Value), "application/pdf", $"{fileName}.pdf")
            : result.ToHttpResult();
    }
}
