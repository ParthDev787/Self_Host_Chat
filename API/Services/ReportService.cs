using System.Text;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace API.Services;

public class ReportService(DataContext dataContext, IConfiguration config, ILogger<ReportService> logger) : IReportService
{
    private const int MaxImageSizeBytes = 200 * 1024; // 200 KB
    private const int MaxImagesCount = 3;
    private const int MaxVideoSizeBytes = 25 * 1024 * 1024; // 25 MB

    public async Task<ReportResponseDto> SubmitReportAsync(ReportSubmissionDto dto, string? clientIp)
    {
        var reportId = $"REP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        logger.LogInformation("Processing report {ReportId} from user '{Username}', category '{Category}'",
            reportId, dto.Username, dto.Category);

        // 1. Validation: Images
        if (dto.Images != null && dto.Images.Count > 0)
        {
            if (dto.Images.Count > MaxImagesCount)
            {
                return new ReportResponseDto
                {
                    Success = false,
                    Message = $"Maximum of {MaxImagesCount} images allowed."
                };
            }

            foreach (var img in dto.Images)
            {
                if (img.Length > MaxImageSizeBytes)
                {
                    return new ReportResponseDto
                    {
                        Success = false,
                        Message = $"Image '{img.FileName}' exceeds the 200 KB size limit ({img.Length / 1024} KB)."
                    };
                }

                var contentType = img.ContentType.ToLowerInvariant();
                if (!contentType.StartsWith("image/"))
                {
                    return new ReportResponseDto
                    {
                        Success = false,
                        Message = $"File '{img.FileName}' is not a valid image format."
                    };
                }
            }
        }

        // 2. Validation: Video
        if (dto.Video != null)
        {
            if (dto.Video.Length > MaxVideoSizeBytes)
            {
                return new ReportResponseDto
                {
                    Success = false,
                    Message = $"Video '{dto.Video.FileName}' exceeds the maximum allowed size of 25 MB."
                };
            }

            var videoContentType = dto.Video.ContentType.ToLowerInvariant();
            if (!videoContentType.StartsWith("video/"))
            {
                return new ReportResponseDto
                {
                    Success = false,
                    Message = $"File '{dto.Video.FileName}' is not a recognized video format."
                };
            }
        }

        // 3. Save a local disk backup in Reports/{reportId}/
        var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "Reports", reportId);
        Directory.CreateDirectory(reportDir);

        var metaPath = Path.Combine(reportDir, "report.json");
        var metaContent = System.Text.Json.JsonSerializer.Serialize(new
        {
            ReportId = reportId,
            dto.Username,
            dto.Category,
            dto.Description,
            ClientIp = clientIp,
            TimestampUtc = DateTime.UtcNow,
            ImageCount = dto.Images?.Count ?? 0,
            HasVideo = dto.Video != null
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(metaPath, metaContent, Encoding.UTF8);

        // Save local copies of images
        var attachmentFiles = new List<(string Name, byte[] Data, string ContentType)>();

        if (dto.Images != null)
        {
            for (var i = 0; i < dto.Images.Count; i++)
            {
                var img = dto.Images[i];
                using var ms = new MemoryStream();
                await img.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var safeName = $"img_{i + 1}_{Path.GetFileName(img.FileName)}";
                await File.WriteAllBytesAsync(Path.Combine(reportDir, safeName), bytes);
                attachmentFiles.Add((safeName, bytes, img.ContentType));
            }
        }

        // Save local copy of video
        if (dto.Video != null)
        {
            using var ms = new MemoryStream();
            await dto.Video.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var safeName = $"video_{Path.GetFileName(dto.Video.FileName)}";
            await File.WriteAllBytesAsync(Path.Combine(reportDir, safeName), bytes);
            attachmentFiles.Add((safeName, bytes, dto.Video.ContentType));
        }

        // 4. Record into Database
        var reportEntity = new IssueReport
        {
            Id = reportId,
            Username = dto.Username,
            Category = dto.Category,
            Description = dto.Description,
            ClientIp = clientIp,
            CreatedAt = DateTime.UtcNow,
            ImageCount = dto.Images?.Count ?? 0,
            HasVideo = dto.Video != null,
            AttachmentNames = string.Join("; ", attachmentFiles.Select(a => a.Name)),
            Status = "Pending"
        };

        try
        {
            dataContext.IssueReports.Add(reportEntity);
            await dataContext.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            logger.LogError(dbEx, "Failed to persist report {ReportId} in database", reportId);
        }

        // 5. Send Email via MailKit if SMTP is configured
        var smtpHost = config["Smtp:Host"];
        var smtpPortStr = config["Smtp:Port"];
        var smtpUser = config["Smtp:Username"];
        var smtpPass = config["Smtp:Password"];
        var fromName = config["Smtp:FromName"] ?? "Self-Host Chat Reports";
        var toEmail = config["Smtp:ToEmail"];

        var isSmtpConfigured = !string.IsNullOrWhiteSpace(smtpHost)
                               && !string.IsNullOrWhiteSpace(smtpUser)
                               && !string.IsNullOrWhiteSpace(smtpPass)
                               && !string.IsNullOrWhiteSpace(toEmail);

        if (!isSmtpConfigured)
        {
            logger.LogWarning(
                "Report {ReportId} saved locally to {Dir}. Email not sent because SMTP credentials are not yet configured in appsettings.json.",
                reportId, reportDir);

            reportEntity.Status = "SavedLocally";
            try { await dataContext.SaveChangesAsync(); } catch {}

            return new ReportResponseDto
            {
                Success = true,
                ReportId = reportId,
                Message = "Report submitted successfully! Thank you for your feedback."
            };
        }

        try
        {
            int.TryParse(smtpPortStr, out var smtpPort);
            if (smtpPort == 0) smtpPort = 587;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, smtpUser!));
            message.To.Add(MailboxAddress.Parse(toEmail!));
            message.Subject = $"[{dto.Category}] Issue Report from {dto.Username} ({reportId})";

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
<div style=""font-family: Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;"">
    <div style=""border-bottom: 2px solid #6366f1; padding-bottom: 12px; margin-bottom: 20px;"">
        <h2 style=""color: #1e1b4b; margin: 0 0 6px 0;"">📬 New Feedback / Issue Report</h2>
        <span style=""background-color: #e0e7ff; color: #4338ca; padding: 4px 10px; border-radius: 9999px; font-size: 12px; font-weight: bold; text-transform: uppercase;"">
            {System.Net.WebUtility.HtmlEncode(dto.Category)}
        </span>
        <span style=""color: #64748b; font-size: 13px; margin-left: 10px;"">
            ID: <b>{reportId}</b>
        </span>
    </div>

    <table style=""width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px;"">
        <tr>
            <td style=""padding: 8px 0; color: #64748b; width: 130px; font-weight: bold;"">Reported By:</td>
            <td style=""padding: 8px 0; color: #0f172a; font-weight: 600;"">{System.Net.WebUtility.HtmlEncode(dto.Username)}</td>
        </tr>
        <tr>
            <td style=""padding: 8px 0; color: #64748b; font-weight: bold;"">Submission Time:</td>
            <td style=""padding: 8px 0; color: #0f172a;"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td>
        </tr>
        <tr>
            <td style=""padding: 8px 0; color: #64748b; font-weight: bold;"">Client IP:</td>
            <td style=""padding: 8px 0; color: #0f172a;"">{System.Net.WebUtility.HtmlEncode(clientIp ?? "Unknown")}</td>
        </tr>
        <tr>
            <td style=""padding: 8px 0; color: #64748b; font-weight: bold;"">Attachments:</td>
            <td style=""padding: 8px 0; color: #0f172a;"">
                {dto.Images?.Count ?? 0} Image(s), {(dto.Video != null ? "1 Video" : "No Video")}
            </td>
        </tr>
    </table>

    <div style=""margin-top: 16px;"">
        <h3 style=""color: #334155; font-size: 14px; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.5px;"">Description:</h3>
        <div style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; font-size: 14px; line-height: 1.6; color: #1e293b; white-space: pre-wrap;"">
{System.Net.WebUtility.HtmlEncode(dto.Description)}
        </div>
    </div>

    <div style=""margin-top: 24px; padding-top: 16px; border-top: 1px solid #f1f5f9; font-size: 12px; color: #94a3b8; text-align: center;"">
        Self-Host Chat System • Automated Issue Reporting Module
    </div>
</div>"
            };

            // Attach files to email
            foreach (var (name, data, ctype) in attachmentFiles)
            {
                builder.Attachments.Add(name, data, ContentType.Parse(ctype));
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var secureSocketOption = smtpPort == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(smtpHost!, smtpPort, secureSocketOption);
            await client.AuthenticateAsync(smtpUser!, smtpPass!);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Report {ReportId} successfully emailed to {ToEmail}", reportId, toEmail);

            reportEntity.Status = "Delivered";
            try { await dataContext.SaveChangesAsync(); } catch {}

            return new ReportResponseDto
            {
                Success = true,
                ReportId = reportId,
                Message = "Report submitted and emailed successfully! Thank you for your feedback."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email for report {ReportId}. Backup preserved in {Dir}", reportId, reportDir);

            reportEntity.Status = "EmailFailed";
            reportEntity.ErrorMessage = ex.Message;
            try { await dataContext.SaveChangesAsync(); } catch {}

            return new ReportResponseDto
            {
                Success = true,
                ReportId = reportId,
                Message = "Report submitted successfully! Thank you for your feedback."
            };
        }
    }
}
