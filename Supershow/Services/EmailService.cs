using System.Net;
using System.Net.Mail;

namespace Supershow.Services;

public class EmailService
{
    private readonly IConfiguration cf;
    private readonly IWebHostEnvironment en;

    public EmailService(IConfiguration cf, IWebHostEnvironment en)
    {
        this.cf = cf;
        this.en = en;
    }

    public void SendEmail(MailMessage mail)
    {
        string user = cf["Smtp:User"] ?? "";
        string pass = cf["Smtp:Pass"] ?? "";
        string name = cf["Smtp:Name"] ?? "";
        string host = cf["Smtp:Host"] ?? "";
        int port = cf.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };

        smtp.Send(mail);
    }

    public void SendVerificationEmail(Verification verification, string? link = "")
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(verification.Account.Email, verification.Account.Name));

        var logoPath = Path.Combine(en.WebRootPath, "img", "supershow.png");
        var logo = new Attachment(logoPath);
        mail.Attachments.Add(logo);
        logo.ContentId = "logo";

        var subject = verification.Action switch
        {
            "Login" => "Verify Device",
            "ChangeEmail" => "Request Email Change",
            "DeleteAccount" => "Request Account Deletion",
            "ResetPassword" => "Request Password Reset",
            _ => "Request Verification"
        };
        mail.Subject = subject + " - Supershow Malaysia";
        mail.IsBodyHtml = true;
        mail.Body = $@"
			<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border-radius: 10px; background-color: #151f2e;'>
            	<div style='text-align: center;'>
            	    <img src='cid:logo' style='height: 80px;'>
            	    <h2 style='color: #ffffff;'>{subject}</h2>
            	    <p style='font-size: 16px; color: #ffffff;'>
            	        Hello, we detected a new {subject.ToLower()} attempt to your <strong>Supershow Malaysia</strong> account.
            	        If you wish to continue, please click the button below
            	    </p>
	
            	    <a href='{link}' style='display: inline-block; margin: 20px 0; padding: 8px 25px; text-decoration: none; border: none; border-radius: 6px; font-size: 16px; background-color: #ffbb00; color: #020916;'>
            	        Continue
            	    </a>
	
            	    <p style='font-size: 15px; margin-top: 20px; color: #ffffff;'>
            	        Or use this code:
            	    </p>
            	    <div style='font-size: 28px; font-weight: bold; color: #ffbb00; margin: 10px 0;'>{verification.OTP}</div>
	
            	    <p style='font-size: 13px; color: #c4c5c9;'>
            	        This request is valid for 5 minutes. If you didn't request it, please ignore this message.
            	    </p>
	
            	    <hr style='margin: 30px 0; color: #c4c5c9;'>
            	    <p style='font-size: 12px; color: #c4c5c9;'>
            	        &copy; {DateTime.Now.Year} Supershow Malaysia. All rights reserved.
            	    </p>
            	</div>
        	</div>
		";

        SendEmail(mail);
    }

    public void SendPasswordChangedEmail(Account account, string? link = "")
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(account.Email, account.Name));

        var logoPath = Path.Combine(en.WebRootPath, "img", "supershow.png");
        var logo = new Attachment(logoPath);
        mail.Attachments.Add(logo);
        logo.ContentId = "logo";

        mail.Subject = "Password Changed - Supershow Malaysia";
        mail.IsBodyHtml = true;
        mail.Body = $@"
			<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border-radius: 10px; background-color: #151f2e;'>
            	<div style='text-align: center;'>
            	    <img src='cid:logo' style='height: 80px;'>
            	    <h2 style='color: #ffffff;'>Password Changed</h2>
            	    <p style='font-size: 16px; color: #ffffff;'>
            	        Hello, this is to inform you that your password has been changed. If you did not change your password, please change your password immediately by clicking the button below.
            	    </p>
	
            	    <a href='{link}' style='display: inline-block; margin: 20px 0; padding: 8px 25px; text-decoration: none; border: none; border-radius: 6px; font-size: 16px; background-color: #ffbb00; color: #020916;'>
            	        Change Password
            	    </a>

            	    <hr style='margin: 30px 0; color: #c4c5c9;'>
            	    <p style='font-size: 12px; color: #c4c5c9;'>
            	        &copy; {DateTime.Now.Year} Supershow Malaysia. All rights reserved.
            	    </p>
            	</div>
        	</div>
		";

        SendEmail(mail);
    }

    public void SendEmailChangedEmail(Account account, string originalEmail, string? link = "")
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(originalEmail, account.Name));

        var logoPath = Path.Combine(en.WebRootPath, "img", "supershow.png");
        var logo = new Attachment(logoPath);
        mail.Attachments.Add(logo);
        logo.ContentId = "logo";

        mail.Subject = "Email Changed - Supershow Malaysia";
        mail.IsBodyHtml = true;
        mail.Body = $@"
			<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border-radius: 10px; background-color: #151f2e;'>
            	<div style='text-align: center;'>
            	    <img src='cid:logo' style='height: 80px;'>
            	    <h2 style='color: #ffffff;'>Email Changed</h2>
            	    <p style='font-size: 16px; color: #ffffff;'>
            	        Hello, this is to inform you that your email has been changed from <strong>{originalEmail}</strong> to <strong>{account.Email}</strong>. If you did not change your email, please contact us immediately by clicking the button below.
            	    </p>
	
            	    <a href='{link}' style='display: inline-block; margin: 20px 0; padding: 8px 25px; text-decoration: none; border: none; border-radius: 6px; font-size: 16px; background-color: #ffbb00; color: #020916;'>
            	        Contact Us
            	    </a>

            	    <hr style='margin: 30px 0; color: #c4c5c9;'>
            	    <p style='font-size: 12px; color: #c4c5c9;'>
            	        &copy; {DateTime.Now.Year} Supershow Malaysia. All rights reserved.
            	    </p>
            	</div>
        	</div>
		";

        SendEmail(mail);
    }

    public void SendAccountCreatedEmail(Account account, string password, string? link = "")
    { 
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(account.Email, account.Name));

        var logoPath = Path.Combine(en.WebRootPath, "img", "supershow.png");
        var logo = new Attachment(logoPath);
        mail.Attachments.Add(logo);
        logo.ContentId = "logo";

        mail.Subject = "Account Created - Supershow Malaysia";
        mail.IsBodyHtml = true;
        mail.Body = $@"
			<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border-radius: 10px; background-color: #151f2e;'>
            	<div style='text-align: center;'>
            	    <img src='cid:logo' style='height: 80px;'>
            	    <h2 style='color: #ffffff;'>Account Created</h2>
            	    <p style='font-size: 16px; color: #ffffff;'>
            	        Hello, this is to inform you that your account has been created. You can login by clicking the button below.
            	    </p>
                    <p style='font-size: 16px; color: #ffffff;'>
                        Email: {account.Email}
                    </p>
                    <p style='font-size: 16px; color: #ffffff;'>
                        Password: {password}
                    </p>
	
            	    <a href='{link}' style='display: inline-block; margin: 20px 0; padding: 8px 25px; text-decoration: none; border: none; border-radius: 6px; font-size: 16px; background-color: #ffbb00; color: #020916;'>
            	        Login
            	    </a>

            	    <hr style='margin: 30px 0; color: #c4c5c9;'>
            	    <p style='font-size: 12px; color: #c4c5c9;'>
            	        &copy; {DateTime.Now.Year} Supershow Malaysia. All rights reserved.
            	    </p>
            	</div>
        	</div>
		";

        SendEmail(mail);
    }
}