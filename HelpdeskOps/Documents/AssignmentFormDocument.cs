using HelpdeskOps.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HelpdeskOps.Documents;

// A4 zimmet handover form built from an AssignmentForm record; the record's copied details are used as they are
public class AssignmentFormDocument : IDocument
{
    private const string DateFormat = "dd.MM.yyyy HH:mm";

    private readonly AssignmentForm _form;

    public AssignmentFormDocument(AssignmentForm form)
    {
        _form = form;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Zimmet Formu {_form.FormNumber}",
        Author = "HelpdeskOps"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(text => text.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(16).Element(ComposeContent);
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                text.Span($"Bu belge HelpdeskOps tarafından {TurkeyTime.Now.ToString(DateFormat)} tarihinde oluşturulmuştur. Sayfa ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("HelpdeskOps · Bilgi Teknolojileri").FontSize(9).FontColor(Colors.Grey.Darken1);
                column.Item().Text("BT Zimmet Teslim Formu").FontSize(18).Bold();
            });
            row.ConstantItem(150).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text($"Form No: {_form.FormNumber}").Bold();
                column.Item().AlignRight().Text($"Tarih: {_form.IssuedAt.ToTurkeyTime().ToString(DateFormat)}");
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(14);

            column.Item().Element(c => Section(c, "Teslim Alan", new[]
            {
                ("Ad Soyad", _form.EmployeeName),
                ("Departman", _form.Department ?? "-")
            }));

            column.Item().Element(c => Section(c, "Cihaz", new[]
            {
                ("Cihaz Adı", _form.Hostname),
                ("Cihaz Türü", _form.DeviceTypeName),
                ("Marka / Model", _form.BrandModel ?? "-"),
                ("Seri Numarası", _form.SerialNumber)
            }));

            column.Item().Element(c => Section(c, "Teslim Bilgileri", new[]
            {
                ("Teslim Tarihi", _form.IssuedAt.ToTurkeyTime().ToString(DateFormat)),
                ("Teslim Eden", _form.IssuedByName ?? "-"),
                ("Dijital Onay", _form.AcceptedAt is DateTime accepted
                    ? $"Teslim alındı ({accepted.ToTurkeyTime().ToString(DateFormat)})"
                    : "Onay bekliyor"),
                ("İade Tarihi", _form.ReturnedAt?.ToTurkeyTime().ToString(DateFormat) ?? "-")
            }));

            column.Item().Text(
                "Yukarıda bilgileri yazılı cihazı çalışır durumda teslim aldım. Cihazı yalnızca iş amaçlı kullanacağımı, " +
                "özenle koruyacağımı, kaybolması veya arızalanması durumunda BT birimine derhal haber vereceğimi ve görev " +
                "değişikliği ya da işten ayrılma durumunda eksiksiz iade edeceğimi kabul ederim.")
                .LineHeight(1.4f);

            column.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Element(c => SignatureBox(c, "Teslim Eden", _form.IssuedByName));
                row.ConstantItem(40);
                row.RelativeItem().Element(c => SignatureBox(c, "Teslim Alan", _form.EmployeeName));
            });
        });
    }

    private static void Section(IContainer container, string title, (string Label, string Value)[] rows)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(title).FontSize(12).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });

                foreach (var (label, value) in rows)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
                        .Text(label).FontColor(Colors.Grey.Darken2);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
                        .Text(value);
                }
            });
        });
    }

    private static void SignatureBox(IContainer container, string title, string? name)
    {
        container.Column(column =>
        {
            column.Item().Text(title).Bold();
            column.Item().Text(name ?? "");
            column.Item().PaddingTop(40).BorderTop(0.5f).BorderColor(Colors.Grey.Darken1).PaddingTop(4)
                .Text("İmza").FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }
}
