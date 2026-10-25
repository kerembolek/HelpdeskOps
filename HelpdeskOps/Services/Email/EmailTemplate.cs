using System.Text.Encodings.Web;

namespace HelpdeskOps.Services.Email;

// One simple HTML layout for every mail, with inline styles because mail clients ignore stylesheets
public static class EmailTemplate
{
    // bodyHtml is trusted markup built by the app; user-written text must be encoded before it gets here
    public static string Wrap(string title, string bodyHtml) => $"""
        <!doctype html>
        <html lang="tr">
        <head><meta charset="utf-8"></head>
        <body style="margin:0;padding:24px;background:#f4f6fa;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;">
          <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e3e7ef;border-radius:8px;overflow:hidden;">
            <div style="background:#1d2a48;color:#ffffff;padding:14px 20px;font-weight:600;">HelpdeskOps</div>
            <div style="padding:20px;">
              <h1 style="font-size:18px;margin:0 0 12px;">{HtmlEncoder.Default.Encode(title)}</h1>
              <div style="font-size:14px;line-height:1.5;">{bodyHtml}</div>
            </div>
            <div style="padding:12px 20px;font-size:12px;color:#6b7280;border-top:1px solid #e3e7ef;">
              Bu e-posta HelpdeskOps tarafından otomatik gönderilmiştir.
            </div>
          </div>
        </body>
        </html>
        """;

    // A paragraph and a button; both the text and the link are encoded
    public static string MessageWithButton(string text, string buttonText, string url) => $"""
        <p style="margin:0 0 16px;">{HtmlEncoder.Default.Encode(text)}</p>
        <a href="{HtmlEncoder.Default.Encode(url)}" style="display:inline-block;background:#0d6efd;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px;">{HtmlEncoder.Default.Encode(buttonText)}</a>
        """;
}
