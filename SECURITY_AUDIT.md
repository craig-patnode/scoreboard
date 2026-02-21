# Security Audit Report — Scoreboard

**Date:** February 20, 2026
**Scope:** Full application — API, client-side HTML/JS, database, SignalR, CI/CD, dependencies
**Auditor:** Claude Code Security Analysis

---

## Executive Summary

This audit identified **30 security findings** across the Scoreboard application: **6 Critical**, **10 High**, **10 Medium**, and **4 Low**. The application uses good practices in some areas (BCrypt password hashing, parameterized EF Core queries, JWT authentication on API controllers), but has significant vulnerabilities in secrets management, input validation, CORS configuration, and client-side security.

**Risk Rating: HIGH** — Critical findings include hardcoded JWT secrets, overly permissive CORS, and missing authentication on SignalR hub methods. These should be remediated immediately before any production deployment.

---

## Findings by Severity

### CRITICAL

#### C1. Hardcoded JWT Secret Fallback in Source Code
| Field | Value |
|-------|-------|
| **Files** | `src/Scoreboard.Api/Program.cs:21`, `src/Scoreboard.Api/Services/AuthService.cs:173` |
| **Category** | Secrets Management |

**Issue:** Application falls back to a hardcoded JWT signing key if configuration is missing:
```csharp
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ScoreboardDefaultSecretKeyThatShouldBeChanged123!";
```
This key is duplicated in `AuthService.cs` line 173.

**Impact:** Any attacker with repository access can forge valid JWT tokens and impersonate any user. If configuration is missing in production, the well-known default key is used.

**Remediation:**
- Remove all hardcoded secrets from source code
- Throw an exception if `Jwt:Key` is not configured: `?? throw new InvalidOperationException("Jwt:Key must be configured")`
- Store the key in Azure Key Vault, not in config files

---

#### C2. Weak Production JWT Secret in appsettings.json
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/appsettings.json:13` |
| **Category** | Secrets Management |

**Issue:** Production JWT secret is a predictable string committed to version control:
```json
"Key": "ScoreboardProductionSecretKey_ChangeThisToSomethingSecure_AtLeast32Chars!"
```

**Impact:** Anyone with repository access can sign valid JWTs. The key name itself acknowledges it needs changing.

**Remediation:**
- Generate a cryptographically random 256-bit secret
- Store in Azure Key Vault, reference via environment variable
- Add `appsettings.json` JWT section to `.gitignore` or use User Secrets for dev

---

#### C3. Overly Permissive CORS Configuration
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Program.cs:61-77` |
| **Category** | Access Control |

**Issue:** Two CORS policies allow all origins:
```csharp
policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();  // "AllowAll"
policy.SetIsOriginAllowed(_ => true).AllowCredentials();     // "SignalR"
```
The `SignalR` policy combines `AllowCredentials()` with a wildcard origin — a dangerous CORS misconfiguration.

**Impact:** Any website can make authenticated cross-origin requests to the API. Enables CSRF attacks and credential theft.

**Remediation:**
- Replace with explicit origin whitelist: `policy.WithOrigins("https://yourdomain.com")`
- Never combine `AllowCredentials()` with wildcard origins

---

#### C4. Exception Details Leaked to Clients
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Controllers/AuthController.cs:58-59` |
| **Category** | Information Disclosure |

**Issue:** Full exception messages returned in API responses:
```csharp
Details = ex.Message,
InnerException = ex.InnerException?.Message
```

**Impact:** Reveals database structure, file paths, internal logic. Aids SQL injection and other attack reconnaissance.

**Remediation:**
- Remove `Details` and `InnerException` from responses
- Log exceptions server-side with `ILogger`
- Return generic message: `"An error occurred. Please try again."`

---

#### C5. SignalR JoinStream Missing Authentication
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Hubs/GameHub.cs:28-45` |
| **Category** | Authentication |

**Issue:** `JoinStream` method has no `[Authorize]` attribute. Any unauthenticated client can subscribe to any stream key and receive real-time game state updates.
```csharp
public async Task JoinStream(string streamKey)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, streamKey);
    // ... returns full game state and logos
}
```

**Impact:** Attackers can monitor any streamer's live game. Stream keys (GUIDs) can be brute-forced or obtained from URL leakage.

**Remediation:**
- Add `[Authorize]` to the `GameHub` class
- Validate that the caller owns or is authorized for the given stream key
- Implement rate limiting on hub method calls

---

#### C6. Sensitive Credentials in localStorage
| Field | Value |
|-------|-------|
| **Files** | `src/Scoreboard.Api/wwwroot/controller.html:429-431`, `signup.html:272-274` |
| **Category** | Client-Side Security |

**Issue:** JWT tokens and stream keys stored in browser localStorage:
```javascript
localStorage.setItem('sc_token', data.token);
localStorage.setItem('sc_streamKey', data.streamKey);
```

**Impact:** Any XSS vulnerability gives attackers full access to stored credentials. localStorage has no expiration and persists across sessions.

**Remediation:**
- Store JWT in HTTP-only, Secure, SameSite cookies instead of localStorage
- Fetch stream key from a protected API endpoint, don't store client-side
- Implement server-side session management

---

### HIGH

#### H1. Missing Rate Limiting on All Endpoints
| Field | Value |
|-------|-------|
| **Files** | `src/Scoreboard.Api/Controllers/AuthController.cs`, `GameController.cs`, `GameHub.cs` |
| **Category** | Availability / Brute Force |

**Issue:** No rate limiting on any endpoint — login, signup, coupon validation, game operations, or SignalR hub methods.

**Impact:** Enables brute-force password attacks, credential stuffing, coupon code enumeration, and DoS attacks.

**Remediation:**
- Add `Microsoft.AspNetCore.RateLimiting` (built into .NET 8)
- Login: 5 attempts per IP per 15 minutes
- Signup: 3 per IP per hour
- Coupon validation: 10 per IP per 5 minutes
- SignalR: per-connection rate limiting

---

#### H2. Missing Security Headers
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Program.cs` (not present) |
| **Category** | Defense in Depth |

**Issue:** No security headers configured. Missing: `X-Frame-Options`, `X-Content-Type-Options`, `Strict-Transport-Security`, `Content-Security-Policy`, `X-XSS-Protection`, `Referrer-Policy`.

**Impact:** Vulnerable to clickjacking, MIME sniffing, XSS in older browsers, HTTPS downgrade attacks.

**Remediation:** Add middleware in `Program.cs`:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
```

---

#### H3. No Input Validation on Request DTOs
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Shared/DTOs/GameDtos.cs` |
| **Category** | Input Validation |

**Issue:** No `[Required]`, `[StringLength]`, `[EmailAddress]`, or `[Range]` annotations on any DTO:
- `SignUpRequest`: No email validation, no password complexity, no display name length limit
- `UpdateTeamNameRequest`: No length limit on `Name`
- `UpdateTeamAppearanceRequest`: No format validation on `LogoData`
- `CreateGameRequest`: Unvalidated string fields

**Impact:** Accepts empty/null emails, single-character passwords, arbitrarily long strings (DoS), invalid data.

**Remediation:** Add data annotations to all DTOs and check `ModelState.IsValid` in controllers.

---

#### H4. innerHTML XSS Vectors in Overlay Files
| Field | Value |
|-------|-------|
| **Files** | All overlay HTML files: `pregame.html:135`, `scoreboard.html:406`, `halftime.html:150`, `fulltime.html:165`, `penalties.html:205` |
| **Category** | Cross-Site Scripting |

**Issue:** Logo URLs rendered via `innerHTML`:
```javascript
el.innerHTML = `<img src="${logoUrl}" alt="">`;
```
Logo data comes from the database as base64 data URIs.

**Impact:** If database is compromised, malicious payloads could be injected. A crafted `data:text/html,...` URI could execute scripts.

**Remediation:**
- Use `document.createElement('img')` and `appendChild()` instead of `innerHTML`
- Validate that logo URLs start with `data:image/` only
- Add Content-Security-Policy header (see H2)

---

#### H5. Stream Key Exposed in URL Query Parameters
| Field | Value |
|-------|-------|
| **Files** | `controller.html:515-521`, all overlay HTML files |
| **Category** | Information Disclosure |

**Issue:** Stream keys passed as URL query parameters:
```javascript
const params = `key=${streamKey}&boardStyleName=${theme}`;
```

**Impact:** Stream keys visible in browser history, server logs, referrer headers, proxy logs, and OBS configuration files.

**Remediation:**
- Use HTTP-only cookies set during a server-side authentication flow
- Or use short-lived tokens (5-minute expiry) instead of static stream keys

---

#### H6. Default Pilot Credentials in Seed Data
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Database/SeedData/SeedData.sql:29-34` |
| **Category** | Secrets Management |

**Issue:** Pilot account stream keys, stream tokens, and email addresses hardcoded in seed data committed to repository. Pilot accounts have `IsPilot=1` which bypasses billing.

**Impact:** Anyone with repository access knows pilot stream keys/tokens and can control their overlays.

**Remediation:**
- Rotate all production stream keys and tokens immediately
- Move seed data for pilot accounts to a separate, non-committed script
- Use environment variables for pilot account setup

---

#### H7. Base64 Logo Data Not Validated
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Controllers/GameController.cs:283-290` |
| **Category** | Input Validation |

**Issue:** Logo upload has a 5MB size limit but no content type validation:
```csharp
[RequestSizeLimit(5_000_000)]
public async Task<IActionResult> SetHomeTeamAppearance([FromBody] UpdateTeamAppearanceRequest request)
```

**Impact:** Accepts any base64 data, not just images. Could store malicious payloads.

**Remediation:**
- Reduce size limit to 1MB
- Validate `LogoData` starts with `data:image/png;`, `data:image/jpeg;`, or `data:image/webp;` only
- Validate the base64 decodes to a valid image

---

#### H8. Email Address in JWT Claims
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/AuthService.cs:178` |
| **Category** | Data Exposure |

**Issue:** Email address included in JWT tokens stored in localStorage:
```csharp
new Claim(ClaimTypes.Email, streamer.EmailAddress),
```

**Impact:** PII exposed in client-side storage. Accessible to XSS attacks and browser extensions.

**Remediation:** Remove email from JWT claims. Fetch from a protected API endpoint if needed.

---

#### H9. Long JWT Expiration (24 Hours)
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/AuthService.cs:188` |
| **Category** | Session Management |

**Issue:** JWT tokens valid for 24 hours with no revocation mechanism:
```csharp
expires: DateTime.UtcNow.AddHours(24),
```

**Impact:** Stolen tokens grant access for a full day. No way to invalidate compromised tokens.

**Remediation:**
- Reduce to 1-2 hours
- Implement refresh token pattern
- Add server-side token blacklist for logout

---

#### H10. StreamKey Returned in Auth Response
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/AuthService.cs:110-117, 129-136` |
| **Category** | Information Disclosure |

**Issue:** `AuthResponse` includes `StreamKey` and `StreamerId`:
```csharp
StreamKey = streamer.StreamKey.ToString(),
StreamerId = streamer.StreamerId,
```

**Impact:** Sensitive identifiers transmitted in login/signup responses. Can be captured in logs, monitoring tools, error reports.

**Remediation:** Return only the JWT token. Client extracts stream key from JWT claims or fetches via separate secure endpoint.

---

### MEDIUM

#### M1. Weak Password Requirements
| Field | Value |
|-------|-------|
| **Files** | `src/Scoreboard.Api/Services/AuthService.cs:73`, `wwwroot/signup.html:201` |
| **Category** | Authentication |

**Issue:** Only minimum 8 characters required (client-side). No server-side complexity validation. Users can set `password` or `12345678`.

**Remediation:** Enforce 12+ characters with uppercase, lowercase, number, and special character on both client and server.

---

#### M2. Missing CSRF Protection
| Field | Value |
|-------|-------|
| **Files** | `signup.html`, `controller.html`, `GameController.cs` |
| **Category** | Cross-Site Request Forgery |

**Issue:** No CSRF tokens on POST endpoints. Partially mitigated by JWT auth (CSRF-resistant for JSON APIs) but CORS misconfiguration (C3) weakens this defense.

**Remediation:** Fix CORS first (C3). Consider anti-forgery tokens for form submissions.

---

#### M3. Unvalidated Team Names
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/GameService.cs:384-396` |
| **Category** | Input Validation |

**Issue:** Team names stored without length or character validation:
```csharp
team.TeamName = name;  // No validation
```

**Remediation:** Add `[StringLength(50)]` and `[RegularExpression]` on the DTO. Validate server-side.

---

#### M4. Unvalidated Penalty JSON (No Size Limit)
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/GameService.cs:503-543` |
| **Category** | Input Validation |

**Issue:** Penalty kicks list can grow unbounded:
```csharp
kicks.Add(normalized);  // No limit on list size
```

**Remediation:** Cap at 15 kicks per team (standard 5 rounds + reasonable sudden death). Add JSON CHECK constraint in database.

---

#### M5. No Security Event Logging
| Field | Value |
|-------|-------|
| **Files** | All services |
| **Category** | Monitoring |

**Issue:** Failed logins, signups, authorization failures, and token generation not logged.

**Impact:** Cannot detect brute force attacks. No audit trail for incident response.

**Remediation:** Add `ILogger` calls for all security events. Configure Application Insights in Azure.

---

#### M6. Coupon Code Enumeration
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Controllers/AuthController.cs:45-62` |
| **Category** | Information Disclosure |

**Issue:** Unauthenticated endpoint reveals coupon validity and discount details. No rate limiting.

**Remediation:** Add rate limiting. Return generic "Coupon applied!" without discount percentage.

---

#### M7. Database Connection String in appsettings.json
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/appsettings.json:9-11` |
| **Category** | Secrets Management |

**Issue:** Connection string template with placeholder credentials visible in repository.

**Remediation:** Use Azure Key Vault or environment variables for all connection strings.

---

#### M8. GitHub Actions: Unpinned Action Versions
| Field | Value |
|-------|-------|
| **File** | `.github/workflows/main_scoreboard-app.yml:19,22,33,47,52,60` |
| **Category** | Supply Chain |

**Issue:** Actions referenced by tag (`@v4`) instead of SHA hash. Vulnerable to tag mutation attacks.

**Remediation:** Pin all actions to full commit SHAs with version comments.

---

#### M9. No Security Scanning in CI/CD Pipeline
| Field | Value |
|-------|-------|
| **File** | `.github/workflows/main_scoreboard-app.yml` |
| **Category** | DevSecOps |

**Issue:** No SAST, dependency scanning, or secret detection in the build pipeline.

**Remediation:** Enable Dependabot. Add CodeQL analysis step. Add `dotnet list package --vulnerable` check.

---

#### M10. SignalR Token in Query String
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Program.cs:39-46` |
| **Category** | Token Exposure |

**Issue:** JWT passed as query parameter for SignalR WebSocket upgrade:
```csharp
var accessToken = context.Request.Query["access_token"];
```

**Impact:** Token visible in server logs and network monitoring. Acceptable trade-off for WebSocket but should be documented.

**Remediation:** This is a known SignalR limitation. Ensure server logs redact query parameters. Use short-lived tokens.

---

### LOW

#### L1. Swagger Accessible in Non-Development Environments
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Program.cs:100-104` |
| **Category** | Information Disclosure |

**Issue:** Swagger disabled only by environment check. If environment variable is misconfigured, API schema is exposed.

**Remediation:** Add explicit configuration flag: `DisableSwagger=true` for production.

---

#### L2. Email Enumeration on Signup
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Services/AuthService.cs:43` |
| **Category** | Information Disclosure |

**Issue:** Returns "Email already registered" on duplicate signup, confirming account existence.

**Remediation:** Return generic message: "If this email is available, an account will be created."

---

#### L3. Implicit Security Defaults in GetStreamerId()
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Controllers/GameController.cs:26-30` |
| **Category** | Defense in Depth |

**Issue:** Returns `0` or empty string if claims are missing:
```csharp
int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
```

**Remediation:** Throw `UnauthorizedAccessException` instead of using default values.

---

#### L4. No Request ID Tracking
| Field | Value |
|-------|-------|
| **File** | `src/Scoreboard.Api/Program.cs` (not present) |
| **Category** | Monitoring |

**Issue:** No correlation ID in responses for debugging and log tracing.

**Remediation:** Add `X-Request-ID` header middleware using `context.TraceIdentifier`.

---

## Dependency Analysis

| Package | Version | Status |
|---------|---------|--------|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | Current |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | Current |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | Current |
| BCrypt.Net-Next | 4.0.3 | Current |
| Swashbuckle.AspNetCore | 10.1.2 | Current |
| System.IdentityModel.Tokens.Jwt | 8.3.0 | Current |

No known vulnerable package versions detected. Consider adding:
- `Microsoft.AspNetCore.RateLimiting` — built-in rate limiting for .NET 8
- `Azure.Extensions.AspNetCore.Configuration.Secrets` — Key Vault integration

---

## Remediation Priority

### Immediate (before any production deployment)
| # | Finding | Effort |
|---|---------|--------|
| C1 | Remove hardcoded JWT fallback | 15 min |
| C2 | Move JWT secret to Key Vault | 30 min |
| C3 | Fix CORS to explicit origins | 15 min |
| C4 | Remove exception details from responses | 10 min |
| H2 | Add security headers middleware | 15 min |

### This Sprint
| # | Finding | Effort |
|---|---------|--------|
| C5 | Add [Authorize] to GameHub | 20 min |
| C6 | Move tokens to HTTP-only cookies | 2 hours |
| H1 | Implement rate limiting | 2 hours |
| H3 | Add DTO validation annotations | 1 hour |
| H6 | Rotate pilot credentials | 30 min |
| H9 | Reduce JWT expiry, add refresh tokens | 2 hours |

### Next Sprint
| # | Finding | Effort |
|---|---------|--------|
| H4 | Fix innerHTML XSS in overlays | 1 hour |
| H5 | Move stream key from URL to cookie | 2 hours |
| H7 | Validate logo uploads (format/size) | 30 min |
| M1-M10 | All medium findings | 4 hours |
| M8-M9 | CI/CD hardening | 1 hour |

### Backlog
| # | Finding | Effort |
|---|---------|--------|
| L1-L4 | All low findings | 2 hours |

---

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 6 |
| HIGH | 10 |
| MEDIUM | 10 |
| LOW | 4 |
| **Total** | **30** |

The most urgent action items are removing hardcoded secrets (C1, C2), fixing CORS (C3), and stopping exception leakage (C4). These four fixes take under an hour and eliminate the highest-impact attack vectors.
