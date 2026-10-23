using HelpdeskOps.Models;

namespace HelpdeskOps.Services;

// Stores ticket attachments in App_Data/attachments, outside wwwroot, so files are only reachable through the download action
public class AttachmentStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const int MaxFilesPerUpload = 5;

    // Extension -> content type sent on download; the type is never taken from the browser
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public static string AcceptList => string.Join(",", AllowedTypes.Keys);

    private readonly string _root;

    public AttachmentStorage(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.ContentRootPath, "App_Data", "attachments");
        Directory.CreateDirectory(_root);
    }

    // Returns a Turkish error message, or null when the file can be stored
    public static string? Validate(IFormFile file)
    {
        var name = CleanFileName(file.FileName);
        if (file.Length == 0)
            return $"{name}: dosya boş.";
        if (file.Length > MaxFileSize)
            return $"{name}: dosya en fazla {MaxFileSize / (1024 * 1024)} MB olabilir.";
        if (!AllowedTypes.ContainsKey(Path.GetExtension(name)))
            return $"{name}: bu dosya türü desteklenmiyor. İzin verilenler: {string.Join(", ", AllowedTypes.Keys)}";
        return null;
    }

    // Checks the count and every file; returns the first error or null
    public static string? ValidateAll(IReadOnlyCollection<IFormFile> files)
    {
        if (files.Count > MaxFilesPerUpload)
            return $"Tek seferde en fazla {MaxFilesPerUpload} dosya yükleyebilirsiniz.";
        return files.Select(Validate).FirstOrDefault(error => error != null);
    }

    // Writes the file to disk and returns the record to add; the caller sets the ticket and saves
    public async Task<TicketAttachment> SaveAsync(IFormFile file, string? uploadedById)
    {
        var fileName = CleanFileName(file.FileName);
        var storedName = Guid.NewGuid().ToString("N");

        await using (var stream = File.Create(Path.Combine(_root, storedName)))
        {
            await file.CopyToAsync(stream);
        }

        return new TicketAttachment
        {
            FileName = fileName,
            StoredName = storedName,
            ContentType = AllowedTypes[Path.GetExtension(fileName)],
            Size = file.Length,
            UploadedById = uploadedById,
            UploadedAt = DateTime.UtcNow
        };
    }

    public Stream? OpenRead(TicketAttachment attachment)
    {
        var path = Path.Combine(_root, attachment.StoredName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(TicketAttachment attachment)
    {
        var path = Path.Combine(_root, attachment.StoredName);
        if (File.Exists(path))
            File.Delete(path);
    }

    // "C:\fakepath\ekran görüntüsü.png" -> "ekran görüntüsü.png", without characters Windows does not allow
    private static string CleanFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (name.Length == 0)
            name = "dosya";
        if (name.Length > 200)
            name = name[..(200 - Path.GetExtension(name).Length)] + Path.GetExtension(name);
        return name;
    }
}
