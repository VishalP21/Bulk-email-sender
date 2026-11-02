using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MailNotificationWirhOverandInterface.Models;

namespace MailNotificationWirhOverandInterface.Services;

public interface ISheetImportService
{
    Task<SheetImportResult> ImportAsync(Stream? fileStream, string? fileName, string? sheetUrl, CancellationToken cancellationToken);
}

public sealed class SheetImportResult
{
    public string? Error { get; init; }

    public string Label { get; set; } = "";

    public List<RecruiterContact> Contacts { get; init; } = [];

    public List<string> Notes { get; init; } = [];
}

public sealed class SheetImportService : ISheetImportService
{
    public const string HttpClientName = "sheets";
    public const int MaxContacts = 200;

    private static readonly Regex SheetIdPattern = new(@"/spreadsheets/d/([a-zA-Z0-9-_]{10,})", RegexOptions.Compiled);
    private static readonly Regex GidPattern = new(@"[?&#]gid=(\d+)", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SheetImportService> _logger;

    public SheetImportService(IHttpClientFactory httpClientFactory, ILogger<SheetImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SheetImportResult> ImportAsync(Stream? fileStream, string? fileName, string? sheetUrl, CancellationToken cancellationToken)
    {
        var hasFile = fileStream is not null && fileStream.Length > 0 && !string.IsNullOrWhiteSpace(fileName);
        var hasUrl = !string.IsNullOrWhiteSpace(sheetUrl);

        if (!hasFile && !hasUrl)
        {
            return Fail("Upload a CSV or Excel file, or paste a Google Sheet link.");
        }

        try
        {
            if (hasFile)
            {
                var rows = await ReadFileAsync(fileStream!, fileName!);
                var result = BuildContacts(rows);
                result.Label = Path.GetFileName(fileName!);
                if (hasUrl)
                {
                    result.Notes.Insert(0, "Used the uploaded file. The Google Sheet link was left unused.");
                }

                return result;
            }

            var table = await DownloadGoogleSheetAsync(sheetUrl!.Trim(), cancellationToken);
            var imported = BuildContacts(table.Rows);
            imported.Label = "Google Sheet";
            return imported;
        }
        catch (SheetReadException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not import recruiter sheet {FileName}", fileName);
            return Fail("Could not read that file. Use a .csv or .xlsx sheet with Company Name and Email columns.");
        }
    }

    private async Task<List<string[]>> ReadFileAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer);
        buffer.Position = 0;

        return extension switch
        {
            ".csv" or ".txt" => ParseCsv(Encoding.UTF8.GetString(buffer.ToArray())),
            ".xlsx" => ReadExcel(buffer),
            _ => throw new SheetReadException("Upload a .csv or .xlsx file. In Google Sheets use File, then Download.")
        };
    }

    private async Task<(List<string[]> Rows, string Label)> DownloadGoogleSheetAsync(string sheetUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(sheetUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new SheetReadException("Paste a Google Sheets link from docs.google.com. Share the sheet as anyone with the link can view.");
        }

        var idMatch = SheetIdPattern.Match(sheetUrl);
        if (!idMatch.Success)
        {
            throw new SheetReadException("That link is not a Google Sheet. Open the sheet and copy the address from the browser.");
        }

        var export = $"https://docs.google.com/spreadsheets/d/{idMatch.Groups[1].Value}/export?format=csv";
        var gidMatch = GidPattern.Match(sheetUrl);
        if (gidMatch.Success)
        {
            export += "&gid=" + gidMatch.Groups[1].Value;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(export, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || LooksLikeHtml(body))
        {
            throw new SheetReadException("Could not read that Google Sheet. Share it as anyone with the link can view, then paste the link again.");
        }

        return (ParseCsv(body), "Google Sheet");
    }

    private static List<string[]> ReadExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            throw new SheetReadException("The Excel file has no worksheet.");
        }

        var range = worksheet.RangeUsed();
        if (range is null)
        {
            throw new SheetReadException("The Excel sheet is empty.");
        }

        var rows = new List<string[]>();
        for (var rowNumber = range.FirstRow().RowNumber(); rowNumber <= range.LastRow().RowNumber(); rowNumber++)
        {
            var cells = new string[range.ColumnCount()];
            for (var offset = 0; offset < cells.Length; offset++)
            {
                cells[offset] = worksheet.Cell(rowNumber, range.FirstColumn().ColumnNumber() + offset).GetString().Trim();
            }

            rows.Add(cells);
        }

        return rows;
    }

    public static List<string[]> ParseCsv(string text)
    {
        text = text.TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SheetReadException("The sheet is empty.");
        }

        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(character => character == ';') > firstLine.Count(character => character == ',')
            ? ';'
            : ',';

        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                inQuotes = true;
            }
            else if (character == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row = [];
            }
            else if (character != '\r')
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows.Where(cells => cells.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToList();
    }

    private static SheetImportResult BuildContacts(List<string[]> rows)
    {
        if (rows.Count == 0)
        {
            return Fail("The sheet is empty.");
        }

        var header = rows[0].Select(cell => cell.Trim()).ToArray();
        var headerLooksNamed = header.Any(cell => IsEmailHeader(cell) || IsCompanyHeader(cell));
        var dataRows = headerLooksNamed ? rows.Skip(1) : rows;
        var (companyIndex, emailIndex) = headerLooksNamed
            ? FindColumns(header)
            : FindColumnsFromData(rows);

        if (companyIndex < 0)
        {
            return Fail("Could not find a company column. Add a header named Company Name.");
        }

        if (emailIndex < 0)
        {
            return Fail("Could not find an email column. Add a header named Email.");
        }

        var contacts = new List<RecruiterContact>();
        var notes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRow = headerLooksNamed ? 2 : 1;

        foreach (var cells in dataRows)
        {
            if (contacts.Count >= MaxContacts)
            {
                notes.Add($"Stopped at {MaxContacts} companies. Split the sheet and load the rest later.");
                break;
            }

            var company = Cell(cells, companyIndex);
            var email = Cell(cells, emailIndex);
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(email))
            {
                sourceRow++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(email) || !IsEmail(email))
            {
                notes.Add($"Skipped row {sourceRow}: need both a company name and a valid email.");
                sourceRow++;
                continue;
            }

            if (!seen.Add(email))
            {
                notes.Add($"Skipped row {sourceRow}: {email} is already in the list.");
                sourceRow++;
                continue;
            }

            contacts.Add(new RecruiterContact
            {
                RowNumber = sourceRow,
                CompanyName = company,
                Email = email
            });
            sourceRow++;
        }

        if (contacts.Count == 0)
        {
            return new SheetImportResult
            {
                Error = "No company emails were found.",
                Notes = notes
            };
        }

        notes.Insert(0, $"Found {contacts.Count} {(contacts.Count == 1 ? "company" : "companies")}.");
        return new SheetImportResult
        {
            Contacts = contacts,
            Notes = notes
        };
    }

    private static (int Company, int Email) FindColumns(string[] header)
    {
        var email = -1;
        var company = -1;
        for (var index = 0; index < header.Length; index++)
        {
            if (email < 0 && IsEmailHeader(header[index]))
            {
                email = index;
            }
        }

        for (var index = 0; index < header.Length; index++)
        {
            if (index == email)
            {
                continue;
            }

            if (company < 0 && IsCompanyHeader(header[index]))
            {
                company = index;
            }
        }

        if (company < 0 && email >= 0 && header.Length == 2)
        {
            company = email == 0 ? 1 : 0;
        }

        return (company, email);
    }

    private static (int Company, int Email) FindColumnsFromData(List<string[]> rows)
    {
        var width = rows.Max(row => row.Length);
        if (width < 2)
        {
            return (-1, -1);
        }

        var email = -1;
        for (var column = 0; column < width; column++)
        {
            var emails = rows.Count(row => IsEmail(Cell(row, column)));
            if (emails > 0 && (email < 0 || emails > rows.Count(row => IsEmail(Cell(row, email)))))
            {
                email = column;
            }
        }

        if (email < 0)
        {
            return (-1, -1);
        }

        var company = email == 0 ? 1 : 0;
        return (company, email);
    }

    private static bool IsEmailHeader(string header)
    {
        var value = header.Trim().ToLowerInvariant();
        return value.Contains("e-mail", StringComparison.Ordinal)
            || value.Contains("email", StringComparison.Ordinal)
            || value.Contains("mail id", StringComparison.Ordinal)
            || value == "mail"
            || value.EndsWith(" mail", StringComparison.Ordinal);
    }

    private static bool IsCompanyHeader(string header)
    {
        var value = header.Trim().ToLowerInvariant();
        return value.Contains("company", StringComparison.Ordinal)
            || value.Contains("organi", StringComparison.Ordinal)
            || value.Contains("employer", StringComparison.Ordinal)
            || value.Contains("firm", StringComparison.Ordinal)
            || value == "name";
    }

    private static string Cell(string[] cells, int index)
    {
        return index >= 0 && index < cells.Length ? cells[index].Trim() : "";
    }

    private static bool IsEmail(string value)
    {
        return MailAddress.TryCreate(value.Trim(), out var address)
            && address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHtml(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static SheetImportResult Fail(string error)
    {
        return new SheetImportResult { Error = error };
    }

    private sealed class SheetReadException : Exception
    {
        public SheetReadException(string message) : base(message)
        {
        }
    }
}
