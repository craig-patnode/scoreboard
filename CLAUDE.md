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

### Azure Resources
- App Service: `scoreboard-app`
- Resources are manually deployed to Azure
- Primary workflow: `.github/workflows/main_scoreboard-app.yml`

## Architecture Notes
- .NET 8 ASP.NET Core application
- SignalR for real-time updates
- Azure SQL Database backend
- Multi-tenant design with StreamKey-based isolation
