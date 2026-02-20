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

### Azure Resources
- App Service: `scoreboard-app`
- Resources are manually deployed to Azure
- Primary workflow: `.github/workflows/main_scoreboard-app.yml`

## Architecture Notes
- .NET 8 ASP.NET Core application
- SignalR for real-time updates
- Azure SQL Database backend
- Multi-tenant design with StreamKey-based isolation
