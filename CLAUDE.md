# Project Preferences - Scoreboard

## Development Preferences

### Formatting
- **Use tabs for indentation, not spaces** — applies to all file types (C#, HTML, JS, SQL, CSS)
  - Exception: JSON and YAML files use 2 spaces (required by format spec / tooling convention)

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
- **One open PR at a time** - Only one PR should be open at any given time to avoid merge conflicts
  - Before creating a new PR, prompt the user to approve/merge the existing open PR first
  - After the existing PR is merged: switch to `main`, pull latest, then create the new feature branch from `main`
  - If a PR has merge conflicts: switch to `main`, pull, then merge `main` into the feature branch to resolve
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

### Database Schema Validation
- **Before deploying code that touches entities/models**, compare local DB schema against Azure SQL to catch discrepancies
- **When changing DB schema (entities, columns, types)**, also update the SQL source files in `src/Scoreboard.Database/`:
  - Individual table files: `Tables/*.sql`
  - Full setup script: `Setup.sql`
  - Seed data: `SeedData/SeedData.sql`
- All three sources of truth must stay in sync: **entity classes** ↔ **SQL source files** ↔ **actual databases** (local + Azure)
- **Always update seed data** (`SeedData/SeedData.sql` and the seed section in `Setup.sql`) when any DB schema change or discrepancy is found — column renames, type changes, new columns, etc.
- Local SQL Server: `localhost\SQLExpress`, Database: `ScoreboardDB`
- Azure SQL Server: `scoreboard-asql.database.windows.net`, Database: `ScoreboardDb`
- Use `sqlcmd` to query `INFORMATION_SCHEMA.COLUMNS` on both and compare column names, data types, nullability, and max lengths
- Schema differences between local dev and Azure production are a top cause of 500 errors that pass local testing

### Azure Resources
- App Service: `scoreboard-app`
- SQL Server: `scoreboard-asql` / Database: `ScoreboardDb`
- Resources are manually deployed to Azure
- Primary workflow: `.github/workflows/main_scoreboard-app.yml`

### Performance & Cost Optimization
- **Use in-memory caching for frequently-read, infrequently-written data** — avoid DB round-trips for hot paths like overlay state polling
  - Pattern: write-through cache (update cache on every write, read from cache on reads)
  - Use `ConcurrentDictionary`-based singletons for simple key-value caches (e.g., `GameStateCache`)
  - Key by stream key or other tenant identifier for multi-tenant isolation
- **Minimize SignalR bandwidth** — send data only when it has changed
  - Use version numbers so clients can skip unchanged state (server returns nothing if version matches)
  - Separate rarely-changing data (logos, team config) from frequently-changing data (score, timer, cards)
  - Logos and large blobs should only be sent on join/reconnect and explicit changes, never on periodic polls
- **Prefer push over poll** — SignalR group broadcasts are far cheaper than N clients polling
  - Use polling only as a safety net (30s+), not as the primary delivery mechanism
  - A 3s poll with 1,000 viewers = 20,000 DB queries/min; a cached 30s poll = 0 DB queries/min
- **Always add `app.UseWebSockets()` before `MapHub`** — ensures SignalR uses WebSocket transport (most efficient)
- **Azure cost awareness** — every DB query, SignalR message, and bandwidth byte costs money at scale
  - Profile hot paths before deploying: how many calls/second at N concurrent viewers?
  - Projection queries (`.Select()`) with `.AsNoTracking()` for read-only data to minimize EF overhead

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
- **Schema drift between local and Azure SQL is a top cause of production 500 errors** — always compare schemas before deploying entity model changes (column renames, new columns, type changes)
- **Three sources of truth must stay in sync**: C# entity classes, SQL source files (`src/Scoreboard.Database/`), and actual databases — when one changes, update all three
- Column renames in entities (e.g., `ShortName` → `TeamCode`) require manual `ALTER TABLE` / `sp_rename` in Azure SQL since EF migrations are not configured
- Hardcoding foreign key IDs (e.g., `SportId = 1`) causes FK constraint violations if the referenced table is empty — always look up or auto-create referenced records dynamically
- New user flows that create records across multiple tables (Teams, Games, GameTeamStats) need all FK dependencies satisfied before `SaveChangesAsync()` — trace the full entity chain during code review

### Error Handling
- A global exception handler middleware (`app.UseExceptionHandler`) covers all endpoints consistently instead of requiring per-endpoint try-catch — add it early in the pipeline
- Critical endpoints like game creation should still have specific try-catch for better contextual error messages

### Code Practices
- Never hardcode validation values in frontend JavaScript - always call backend API
- Static files in wwwroot are only deployed when the correct project is published
