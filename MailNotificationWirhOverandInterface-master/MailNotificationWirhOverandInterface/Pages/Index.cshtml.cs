using System.Text.Json;
using MailNotificationWirhOverandInterface.Models;
using MailNotificationWirhOverandInterface.Options;
using MailNotificationWirhOverandInterface.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace MailNotificationWirhOverandInterface.Pages;

public class IndexModel : PageModel
{
    private const string ContactsKey = "recruiter-contacts";
    private const string LabelKey = "recruiter-sheet-label";

    private readonly ISheetImportService _sheetImport;
    private readonly IOutreachMailService _mail;
    private readonly IMailSettingsStore _settings;
    private readonly OutreachOptions _options;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISheetImportService sheetImport,
        IOutreachMailService mail,
        IMailSettingsStore settings,
        IOptions<OutreachOptions> options,
        ILogger<IndexModel> logger)
    {
        _sheetImport = sheetImport;
        _mail = mail;
        _settings = settings;
        _options = options.Value;
        _logger = logger;
    }

    [BindProperty]
    public IFormFile? SheetFile { get; set; }

    [BindProperty]
    public string? SheetUrl { get; set; }

    [BindProperty]
    public ComposeInput Compose { get; set; } = new();

    [BindProperty]
    public List<int> SelectedRows { get; set; } = [];

    [BindProperty]
    public IFormFile? ResumeFile { get; set; }

    public List<RecruiterContact> Contacts { get; private set; } = [];

    public List<string> ImportNotes { get; private set; } = [];

    public List<SendOutcome> Outcomes { get; private set; } = [];

    public string? SheetLabel { get; private set; }

    public string? Banner { get; private set; }

    public bool BannerIsError { get; private set; }

    public string SavedFromEmail { get; private set; } = "";

    public bool HasSavedPassword { get; private set; }

    public int SendLimit => Math.Clamp(_options.MaxRecipientsPerSend, 1, 100);

    public void OnGet()
    {
        LoadContacts();
        LoadNotes();
        ApplySavedProfile();
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        Stream? stream = null;
        if (SheetFile is not null)
        {
            stream = SheetFile.OpenReadStream();
        }

        var result = await _sheetImport.ImportAsync(stream, SheetFile?.FileName, SheetUrl, cancellationToken);
        if (stream is not null)
        {
            await stream.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            TempData["Banner"] = result.Error;
            TempData["BannerIsError"] = "1";
            if (result.Notes.Count > 0)
            {
                TempData["ImportNotes"] = JsonSerializer.Serialize(result.Notes);
            }

            return RedirectToPage();
        }

        HttpContext.Session.SetString(ContactsKey, JsonSerializer.Serialize(result.Contacts));
        HttpContext.Session.SetString(LabelKey, result.Label);
        TempData["ImportNotes"] = JsonSerializer.Serialize(result.Notes);
        TempData["Banner"] = result.Notes.FirstOrDefault() ?? "Sheet loaded.";
        return RedirectToPage();
    }

    public IActionResult OnPostClear()
    {
        HttpContext.Session.Remove(ContactsKey);
        HttpContext.Session.Remove(LabelKey);
        TempData["Banner"] = "Cleared the company list.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        LoadContacts();
        ApplySavedMailAccount();

        if (!Compose.Confirmed)
        {
            ModelState.AddModelError(nameof(Compose.Confirmed), "Confirm that you want to contact the selected companies.");
        }

        if (string.IsNullOrWhiteSpace(Compose.FromEmail) || string.IsNullOrWhiteSpace(Compose.FromPassword))
        {
            ModelState.AddModelError(string.Empty, "Open Settings and save your email and app password.");
        }

        if (string.IsNullOrWhiteSpace(Compose.Host) || Compose.Host.Contains('/') || Compose.Host.Contains(' '))
        {
            ModelState.AddModelError(string.Empty, "In Settings, enter a mail server such as smtp.gmail.com.");
        }

        byte[]? attachment = null;
        string? attachmentName = null;
        if (ResumeFile is { Length: > 0 })
        {
            var extension = Path.GetExtension(ResumeFile.FileName).ToLowerInvariant();
            if (extension is not ".pdf" and not ".doc" and not ".docx")
            {
                ModelState.AddModelError(nameof(ResumeFile), "Attach a PDF or Word resume.");
            }
            else if (ResumeFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(ResumeFile), "Keep the resume under 5 MB.");
            }
            else
            {
                await using var memory = new MemoryStream();
                await ResumeFile.CopyToAsync(memory, cancellationToken);
                attachment = memory.ToArray();
                attachmentName = Path.GetFileName(ResumeFile.FileName);
            }
        }

        var selected = Contacts.Where(contact => SelectedRows.Contains(contact.RowNumber)).ToList();
        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Tick at least one company.");
        }
        else if (selected.Count > SendLimit)
        {
            ModelState.AddModelError(string.Empty, $"Send up to {SendLimit} companies at a time. Untick the rest and send again.");
        }

        if (!ModelState.IsValid)
        {
            Banner = "Fix the highlighted fields, then send again.";
            BannerIsError = true;
            ClearPassword();
            return Page();
        }

        try
        {
            Outcomes = (await _mail.SendAsync(selected, Compose, attachment, attachmentName, cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outreach send stopped");
            Banner = "Sending stopped before every mail went out. Check the results below and try the remaining companies again.";
            BannerIsError = true;
            ClearPassword();
            return Page();
        }

        var sent = Outcomes.Count(outcome => outcome.Success);
        var failed = Outcomes.Count - sent;
        Banner = sent == 0
            ? $"No mail was sent. {failed} {(failed == 1 ? "company" : "companies")} failed."
            : failed == 0
                ? $"Sent {sent} {(sent == 1 ? "mail" : "mails")}."
                : $"Sent {sent}. {failed} could not be sent.";
        BannerIsError = failed > 0;
        ClearPassword();
        return Page();
    }

    private void ClearPassword()
    {
        Compose.FromPassword = "";
        ModelState.Remove("Compose.FromPassword");
    }

    private void LoadContacts()
    {
        var json = HttpContext.Session.GetString(ContactsKey);
        Contacts = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<RecruiterContact>>(json) ?? [];
        SheetLabel = HttpContext.Session.GetString(LabelKey);
    }

    private void ApplySavedProfile()
    {
        var saved = _settings.Get();
        SavedFromEmail = saved.FromEmail;
        HasSavedPassword = saved.HasPassword;
        if (!string.IsNullOrWhiteSpace(saved.YourName))
        {
            Compose.YourName = saved.YourName;
        }

        if (!string.IsNullOrWhiteSpace(saved.Role))
        {
            Compose.Role = saved.Role;
        }

        if (!string.IsNullOrWhiteSpace(saved.Phone))
        {
            Compose.Phone = saved.Phone;
        }

        if (!string.IsNullOrWhiteSpace(saved.Link))
        {
            Compose.Link = saved.Link;
        }

        if (!string.IsNullOrWhiteSpace(saved.Subject))
        {
            Compose.Subject = saved.Subject;
        }

        if (!string.IsNullOrWhiteSpace(saved.Body))
        {
            Compose.Body = saved.Body;
        }
    }

    private void ApplySavedMailAccount()
    {
        var saved = _settings.Get();
        SavedFromEmail = saved.FromEmail;
        HasSavedPassword = saved.HasPassword;
        Compose.FromEmail = saved.FromEmail;
        Compose.FromPassword = _settings.ReadPassword() ?? "";
        Compose.Host = string.IsNullOrWhiteSpace(saved.Host) ? "smtp.gmail.com" : saved.Host;
        Compose.Port = saved.Port is < 1 or > 65535 ? 587 : saved.Port;
    }

    private void LoadNotes()
    {
        if (TempData["Banner"] is string banner)
        {
            Banner = banner;
            BannerIsError = TempData["BannerIsError"] as string == "1";
        }

        if (TempData["ImportNotes"] is string notesJson)
        {
            ImportNotes = JsonSerializer.Deserialize<List<string>>(notesJson) ?? [];
        }
    }
}
