# Project Preferences - Scoreboard

## Development Preferences

### Code Quality & Best Practices
- **Never hard-code values** - Always use configuration, database lookups, or API calls
- Avoid stubs/placeholders with hardcoded validation in production code
- Implement proper API endpoints for validation and data retrieval
- Use database queries for dynamic data (e.g., coupon codes, user data)

### Deployment & Security
- **Always prefer modern and secure approaches** when options are available
- For Azure deployment: Use federated credentials (managed identity) over publish profiles
- For authentication: Use modern OAuth/OIDC flows where applicable

### Security & Secrets - CRITICAL RULES
- **NEVER commit passwords, secrets, or credentials to git** - This is non-negotiable
- **NEVER create scripts with hardcoded passwords** - Use environment variables or Azure Key Vault
- Before any git commit, verify:
  - No `.sh`, `.ps1`, or config files contain passwords or secrets
  - Check `git status` for any debug/test scripts that may contain credentials
  - Remove all log files, Azure logs, and debug artifacts
- Update `.gitignore` immediately if new types of sensitive files are created
- Secrets should only exist in:
  - Azure App Service Configuration (connection strings, app settings)
  - Azure Key Vault
  - Local environment variables (never committed)

### Git Workflow & Pull Requests
- **I handle all git operations** - commits, pushes, and PR creation
- **Branch naming convention**: Always create branches under `users/craigp/<title_summary_of_pr>`
  - Example: `users/craigp/implement_coupon_validation`
- **One PR per fix/feature** - Create separate PRs for each logical change, don't bundle unrelated changes
- **Before creating PR**: Always pull from `main` first to ensure branch is up to date
  - `git checkout main && git pull origin main`
  - `git checkout <feature-branch> && git merge main`
- **Pull Request process**:
  - I commit changes with descriptive messages
  - I push feature branch: `git push origin <feature-branch>`
  - I create PR targeting `main` branch with clear description
  - **User reviews and approves** - I wait for explicit approval before merging
  - Use GitHub CLI (`gh pr create`) for PR creation
- **Never push directly to main** - always use Pull Request workflow
- **Files that should NEVER be committed**:
  - Log files (*.log, azure-logs/, azure-logs*.zip)
  - Debug/test scripts (test-*.sh, debug-*.sh, especially those with passwords)
  - Temporary files and build artifacts
  - Any file containing passwords, connection strings, or secrets
  - Check `git status` before every commit and verify only source code changes are included

### Bug Fix Process
- When fixing a bug, perform root cause analysis to understand WHY it happened
- After each fix, update the "Lessons Learned" section in this file with the finding
- Before committing CLAUDE.md changes, ask user to approve the proposed additions
- Review Lessons Learned before writing new code to avoid repeating past mistakes

### Azure Resources
- App Service: `scoreboard-app`
- Resources are manually deployed to Azure
- Primary workflow: `.github/workflows/main_scoreboard-app.yml`

## Architecture Notes
- .NET 8 ASP.NET Core application
- SignalR for real-time updates
- Azure SQL Database backend
- Multi-tenant design with StreamKey-based isolation

## Lessons Learned
Captures root cause analysis from bugs to prevent recurrence.

### Deployment & Configuration
- `dotnet publish` without specifying a project won't include static files (wwwroot) - always target the specific `.csproj`
- Azure CLI escapes special characters (`!`, `#`, `^`) in passwords - use Azure Portal for connection strings with special chars, or avoid special chars in passwords
- Azure App Service has TWO config sections: "Connection strings" and "Application settings" - App Settings override Connection Strings, so conflicting entries cause hard-to-debug issues
- Always set `ASPNETCORE_ENVIRONMENT=Production` in Azure App Service settings

### API Design
- Signup/registration endpoints should be idempotent - if email exists and password matches, treat as login instead of error
- All API endpoints should have try-catch error handling returning user-friendly messages
- Never expose raw exception details to end users in production

### Database & Entity Framework
- Hardcoding foreign key IDs (e.g., `SportId = 1`) causes FK constraint violations if the referenced table is empty — always look up or auto-create referenced records dynamically
- New user flows that create records across multiple tables (Teams, Games, GameTeamStats) need all FK dependencies satisfied before `SaveChangesAsync()` — trace the full entity chain during code review

### Error Handling
- A global exception handler middleware (`app.UseExceptionHandler`) covers all endpoints consistently instead of requiring per-endpoint try-catch — add it early in the pipeline
- Critical endpoints like game creation should still have specific try-catch for better contextual error messages

### Code Practices
- Never hardcode validation values in frontend JavaScript - always call backend API
- Static files in wwwroot are only deployed when the correct project is published
