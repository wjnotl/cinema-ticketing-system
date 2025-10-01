using Microsoft.EntityFrameworkCore;

namespace Supershow.Services;

public class VerificationService
{
    private readonly DB db;
    private readonly EmailService es;

    public VerificationService(DB db, EmailService es)
    {
        this.db = db;
        this.es = es;
    }

    public Verification CreateVerification(string action, string baseUrl, int accountId, int? deviceId = null)
    {
        // Generate token
        var token = GeneratorService.RandomString(50);
        while (db.Verifications.Any(u => u.Token == token))
        {
            token = GeneratorService.RandomString(50);
        }

        // Generate OTP
        var otp = GeneratorService.RandomString(6, "0123456789");

        // Add new verification
        Verification verification = new()
        {
            Token = token,
            OTP = otp,
            Action = action,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            DeviceId = deviceId,
            AccountId = accountId
        };
        db.Verifications.Add(verification);
        db.SaveChanges();

        db.Entry(verification).Reference(v => v.Account).Load();

        // Send email
        var link = $"{baseUrl}/Auth/Verify?Token={verification.Token}&otp={verification.OTP}";
        es.SendVerificationEmail(verification, link);

        return verification;
    }

    public Verification? GetVerificationRequest(string? token, string action)
    {
        return db.Verifications.Include(v => v.Account).FirstOrDefault(u => u.Token == token && u.Action == action && u.ExpiresAt > DateTime.Now);
    }
}