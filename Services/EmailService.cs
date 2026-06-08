using System.Net;
using System.Net.Mail;

namespace AttendanceManagementSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendLoginCredentialsAsync(string toEmail, string fullName, string serviceId, string password, string role)
        {
            var settings = _config.GetSection("EmailSettings");
            var host = settings["SmtpHost"]!;
            var port = int.Parse(settings["SmtpPort"]!);
            var senderEmail = settings["SenderEmail"]!;
            var senderName = settings["SenderName"]!;
            var senderPassword = settings["Password"]!;

            var body = $@"
                <html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
                <div style='background:#6c5ce7;padding:20px;text-align:center;'>
                    <h2 style='color:white;margin:0;'>Welcome to PulseHR</h2>
                </div>
                <div style='padding:30px;border:1px solid #eee;'>
                    <p>Hi <strong>{fullName}</strong>,</p>
                    <p>Your <strong>{role}</strong> account has been created. Here are your login details:</p>
                    <table style='width:100%;border-collapse:collapse;margin:20px 0;'>
                        <tr style='background:#f8f9fa;'>
                            <td style='padding:10px;border:1px solid #dee2e6;'><strong>Email</strong></td>
                            <td style='padding:10px;border:1px solid #dee2e6;'>{toEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px;border:1px solid #dee2e6;'><strong>Service ID</strong></td>
                            <td style='padding:10px;border:1px solid #dee2e6;'>{serviceId}</td>
                        </tr>
                        <tr style='background:#f8f9fa;'>
                            <td style='padding:10px;border:1px solid #dee2e6;'><strong>Password</strong></td>
                            <td style='padding:10px;border:1px solid #dee2e6;font-size:18px;font-weight:bold;color:#6c5ce7;'>{password}</td>
                        </tr>
                    </table>
                    <p style='color:red;'><strong>Please log in and change your password immediately from your Profile page.</strong></p>
                    <p>Login URL: <a href='/Account/Login'>/Account/Login</a></p>
                </div>
                </body></html>";

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Your PulseHR Account Has Been Created",
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
