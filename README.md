# ⚽ Scoreboard - Live Streaming Scoreboard Overlays

Professional scoreboard overlays for OBS Studio live streams. Built for youth sports streamers.

<!-- Deployment trigger: 2026-02-20 -->

## Architecture

```
Scoreboard.sln
├── src/
│   ├── Scoreboard.Shared/        # Shared DTOs, Enums (reused by future MAUI app)
│   ├── Scoreboard.Api/           # ASP.NET Core API + SignalR Hub + Static Files
│   │   ├── Controllers/         # REST API endpoints
│   │   ├── Hubs/               # SignalR GameHub for real-time updates
│   │   ├── Services/           # Business logic (GameService, AuthService)
│   │   ├── Data/               # EF Core DbContext + Entity models
│   │   └── wwwroot/            # Static HTML pages served by the app
│   │       ├── index.html      # Landing page
│   │       ├── signup.html     # Signup flow (stubbed payment)
│   │       ├── controller.html # Streamer game controller
│   │       └── overlay/        # OBS Browser Source overlays
│   │           ├── pregame.html
│   │           ├── scoreboard.html
│   │           ├── halftime.html
│   │           └── fulltime.html
│   ├── Scoreboard.Web/          # Blazor components (future MAUI reuse)
│   └── Scoreboard.Database/     # SQL scripts (one per table, 3NF)
│       ├── Tables/
│       └── SeedData/
├── .github/workflows/          # CI/CD pipeline
└── docs/
```

## Key Design Decisions

- **Timer**: Client-side computed from `TimerStartedAtUtc` + `ElapsedSecondsAtPause`. No server round-trips every second. Crash-resilient: on reconnect, client recalculates from stored timestamps.
- **Real-time**: SignalR pushes state changes to overlay clients. Controller → API → SignalR broadcast.
- **Multi-tenancy**: Each streamer has a `StreamKey` (GUID). Overlays subscribe to a SignalR group by StreamKey. No stream crossing.
- **Security**: OBS overlays use StreamKey in URL + optional `X-Stream-Token` header. Controller uses JWT authentication. Streamers can be blocked instantly via `IsBlocked` flag.
- **Sport-agnostic**: Database designed with `Sport` table. Soccer is sport #1. Baseball, Football, etc. can be added without schema changes.

## Setup Instructions

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB for dev, Azure SQL for production)
- Git

### 1. Clone & Build
```bash
git clone https://github.com/craig-patnode/scoreboard.git
cd scoreboard
dotnet restore
dotnet build
```

### 2. Create Database
Run the SQL scripts in order against your SQL Server:
```
1. Tables/Sport.sql
2. Tables/SubscriptionPlan.sql
3. Tables/Discount.sql
4. Tables/Streamer.sql
5. Tables/Team.sql
6. Tables/Game.sql
7. Tables/GameTeamStats.sql
8. SeedData/SeedData.sql
```

Or use the provided script:
```bash
# Using sqlcmd (adjust connection string)
sqlcmd -S "(localdb)\MSSQLLocalDB" -d ScoreboardDb -i src/Scoreboard.Database/Tables/Sport.sql
# ... repeat for each table, then seed data
```

### 3. Update Connection String
Edit `src/Scoreboard.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ScoreboardDb;Trusted_Connection=True;"
  }
}
```

### 4. Run Locally
```bash
cd src/Scoreboard.Api
dotnet run
```
Navigate to `https://localhost:5001` (or the port shown in console).

### 5. Pilot Login
Use the seeded pilot accounts:
- **Craig**: craig@scoreboard.live
- **Dave**: dave@scoreboard.live
- Password: Set via signup or update the hash in the database

## Azure Deployment

### 1. Create Azure Resources
```bash
# Login
az login

# Create Resource Group
az group create --name rg-scoreboard --location westus2

# Create Azure SQL Server (serverless)
az sql server create \
  --name scoreboard-sql \
  --resource-group rg-scoreboard \
  --location westus2 \
  --admin-user scoreadmin \
  --admin-password "YourStrongPassword123!"

# Create Database (serverless tier)
az sql db create \
  --resource-group rg-scoreboard \
  --server scoreboard-sql \
  --name ScoreboardDb \
  --edition GeneralPurpose \
  --compute-model Serverless \
  --family Gen5 \
  --capacity 1 \
  --auto-pause-delay 60

# Allow Azure services
az sql server firewall-rule create \
  --resource-group rg-scoreboard \
  --server scoreboard-sql \
  --name AllowAzure \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create App Service Plan
az appservice plan create \
  --name scoreboard-plan \
  --resource-group rg-scoreboard \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --resource-group rg-scoreboard \
  --plan scoreboard-plan \
  --name scoreboard-app \
  --runtime "DOTNETCORE:8.0"

# Set Connection String
az webapp config connection-string set \
  --resource-group rg-scoreboard \
  --name scoreboard-app \
  --settings DefaultConnection="Server=tcp:scoreboard-sql.database.windows.net,1433;Database=ScoreboardDb;User ID=scoreadmin;Password=YourStrongPassword123!;Encrypt=True;" \
  --connection-string-type SQLAzure
```

### 2. Configure GitHub Actions
1. Download the publish profile from Azure Portal (App Service → Deployment Center → Manage publish profile)
2. Add it as a GitHub secret: `AZURE_WEBAPP_PUBLISH_PROFILE`
3. Push to `main` branch — auto-deploys!

### 3. Run Database Scripts
Connect to Azure SQL via SSMS or Azure Data Studio and run all table scripts + seed data.

## OBS Setup

In OBS Studio, add a Browser Source for each overlay:
1. **Pre-Game**: `https://your-app.azurewebsites.net/overlay/pregame.html?key=YOUR_STREAM_KEY`
2. **Scoreboard**: `https://your-app.azurewebsites.net/overlay/scoreboard.html?key=YOUR_STREAM_KEY`
3. **Half Time**: `https://your-app.azurewebsites.net/overlay/halftime.html?key=YOUR_STREAM_KEY`
4. **Full Time**: `https://your-app.azurewebsites.net/overlay/fulltime.html?key=YOUR_STREAM_KEY`

Set width to 1920 and height to 1080. The overlays have transparent backgrounds.

Optional: Add custom header `X-Stream-Token: YOUR_STREAM_TOKEN` in OBS Browser Source properties for additional security.

## Roadmap
- [ ] Stripe payment integration
- [ ] .NET MAUI Hybrid iOS app (shared Blazor components)
- [ ] Baseball sport support
- [ ] Football sport support
- [ ] Team logo/crest upload
- [ ] Game history & statistics
