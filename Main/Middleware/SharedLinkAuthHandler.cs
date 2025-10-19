using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Security.Claims;

using Data; // for DbContext / SharedLink

namespace Middleware
{
    public class SharedLinkAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ApplicationDbContext _db;

        public SharedLinkAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ApplicationDbContext db
        ) : base(options, logger, encoder)
        {
            _db = db;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // 1️⃣ Check query string
            var token = Request.Query["token"].ToString();
            if (string.IsNullOrEmpty(token))
                return Task.FromResult(AuthenticateResult.NoResult());

            // 2️⃣ Validate shared link
            var link = _db.SharedLinks.FirstOrDefault(s =>
                s.Token == token &&
                !s.Revoked &&
                (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));

            if (link == null)
                return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token"));

            // 3️⃣ Build identity
            var claims = new[]
            {
                new Claim("SharedToken", token),
                new Claim("ResourceType", link.ResourceType),
                new Claim("ResourceId", link.ResourceId.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // 4️⃣ Success
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
