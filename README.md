# LoginExample

An ASP.NET Core MVC application built with .NET 10, Entity Framework Core, ASP.NET Core Identity, and Microsoft SQL Server.

LoginExample is a backend development project focused on implementing authentication, authorization, article and category management, data integrity, and secure web application practices.

The project is developed incrementally, with an emphasis on understanding application behavior, database operations, testing, and maintainability.

## Tech Stack

| Category | Technology |
|---|---|
| Framework | ASP.NET Core MVC (.NET 10) |
| Language | C# |
| ORM | Entity Framework Core |
| Database | Microsoft SQL Server |
| Authentication | ASP.NET Core Identity |
| Authorization | Role-based and resource ownership authorization |
| Frontend | Razor Views, Bootstrap |
| Testing | xUnit, ASP.NET Core Integration Testing |
| Version Control | Git |
| Development Environment | Windows, SQL Server Express LocalDB |

## Features

### Authentication & Authorization

- User registration and login
- Authentication using ASP.NET Core Identity
- Role-based authorization with Admin privileges
- Resource ownership authorization for article operations
- Anti-forgery protection for state-changing HTTP requests
- Server-side validation of user input

### Article Management

- Create articles with category selection
- Edit articles owned by the authenticated user
- Display published articles
- Display article authors and category information
- View article details
- Archive published articles
- View personal archived articles
- Restrict article editing and archiving to authorized owners

### Category Management

- Admin-only category creation, editing, deletion, and restoration
- Category listing for authenticated users
- Category soft deletion using `IsDeleted` and `DeletedAt`
- Restore deleted categories without restoring previous article associations
- Database-level unique category name constraint
- Reserved system category: `未分類` (ID: 1)
- Automatic reassignment of articles to the system category when a category is deleted
- Transaction-based data consistency for category deletion
- Prevention of editing soft-deleted categories

### User Interface

- Shared navigation layout
- Navigation items based on authentication state
- Category management interface for administrators
- Article edit and archive controls based on ownership
- Article author and category display

## Architecture

The application follows the ASP.NET Core Model-View-Controller (MVC) pattern.

### Project Components

| Component | Responsibility |
|---|---|
| Models | Entity definitions and domain data |
| ViewModels | Input validation and view-specific data |
| Controllers | HTTP request processing, authorization, and application logic |
| Views | Razor-based user interface |
| Data | Entity Framework Core DbContext and entity relationships |
| Migrations | Database schema version management |
| Tests | Automated verification of selected application behaviors |

### Data Access

Entity Framework Core is used to interact with Microsoft SQL Server.

The application uses `ApplicationDbContext`, which inherits from `IdentityDbContext`, allowing Identity and application entities to share the same database context.

Application entities include:

- `Article`
- `Category`

Identity provides user and role management through the corresponding Identity entities and tables.

### Article and Category Relationship

Each article belongs to one category.

When an administrator soft-deletes a category:

1. The application begins a database transaction.
2. Articles belonging to that category are reassigned to the reserved `未分類` category.
3. The original category is marked as deleted.
4. The transaction is committed if all operations succeed.

If a database operation fails, the transaction is rolled back.

Restoring a deleted category makes the category available again but does not automatically restore its previous article associations.

## Getting Started

The following instructions describe how to run LoginExample in a local Windows development environment.

### Prerequisites

Install the following dependencies:

- .NET 10 SDK
- Microsoft SQL Server Express LocalDB
- Git
- Entity Framework Core CLI tools (`dotnet-ef`)

Verify the installed .NET SDK:

```powershell
dotnet --version
```

Verify the EF Core CLI:

```powershell
dotnet ef --version
```

If `dotnet-ef` is not installed, install a version compatible with the project's EF Core packages.

For EF Core 10:

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

If the tool is already installed, update it when necessary rather than installing it again.

### 1. Clone the Repository

Clone the repository:

```powershell
git clone <repository-url>
cd LoginExample
```

Replace `<repository-url>` with the actual Git repository URL.

Locate the application project directory containing `LoginExample.csproj`.

The following application commands should be executed from that directory unless otherwise specified.

### 2. Configure the Database

The application uses Microsoft SQL Server through Entity Framework Core.

The default local development configuration is:

| Setting | Value |
|---|---|
| SQL Server instance | `(localdb)\MSSQLLocalDB` |
| Database | `LoginExampleDb` |
| Authentication | Windows Integrated Authentication |
| Connection string key | `ConnectionStrings:DefaultConnection` |

The default `appsettings.json` contains:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoginExampleDb;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False"
  }
}
```

Before proceeding, ensure that SQL Server Express LocalDB is installed and accessible.

If another SQL Server instance is used, override `ConnectionStrings:DefaultConnection` using an appropriate local configuration source or environment variable.

For example, ASP.NET Core supports the environment variable:

```text
ConnectionStrings__DefaultConnection
```

Do not commit database passwords, API keys, or other sensitive credentials to source control.

**Note:** SQL Server Express LocalDB is intended for Windows development. Linux deployment requires a compatible SQL Server connection configuration.

### 3. Restore Dependencies

From the application project directory:

```powershell
dotnet restore
```

Build the application:

```powershell
dotnet build
```

### 4. Initialize the Database

The application uses Entity Framework Core migrations to manage database schema changes.

Apply the existing migrations:

```powershell
dotnet ef database update
```

To inspect the migration history:

```powershell
dotnet ef migrations list
```

The project currently contains the following migrations:

| Migration | Description |
|---|---|
| `20261016104856_InitialIdentiry` | Initialize ASP.NET Core Identity |
| `20261017080400_AddArticles` | Add article persistence |
| `20261018022515_AddArticleArchive` | Add article archiving |
| `20261019031712_AddArticleCategory` | Add category relationships |
| `20261019054013_AddCategoryUniquName` | Add unique category name constraint |
| `20261020224534_AddCategorySoftDelete` | Add category soft deletion support |

Migration names are preserved as originally generated.

Entity Framework Core tracks applied migrations in the `__EFMigrationsHistory` database table.

Running `dotnet ef database update` applies any migrations that have not yet been applied to the configured database.

### 5. Run the Application

Start the application in the Development environment:

```powershell
dotnet run --environment Development
```

After startup, open the local URL displayed in the terminal.

The application uses ASP.NET Core MVC routing, authentication middleware, and authorization middleware.

### 6. Administrator Setup

In the Development environment, the application automatically ensures that the `Admin` role exists.

An existing registered user can be assigned to this role using the `SeedAdmin:Email` configuration key.

Example configuration:

```json
{
  "SeedAdmin": {
    "Email": "admin@example.com"
  }
}
```

Replace the example email address with the email address of an existing registered user.

The setup process is:

1. Start the application in the Development environment.
2. Register a user account.
3. Configure `SeedAdmin:Email` using local development configuration.
4. Restart the application.
5. The application locates the existing user and assigns the `Admin` role if necessary.

The application does not automatically create an administrator account.

If an email address is configured but no matching user exists, application startup will fail with a configuration-related exception.

This administrator assignment mechanism is intended for local development and should not be used as an unrestricted production administrator provisioning process.

## Testing

The project includes automated integration tests using xUnit and ASP.NET Core testing infrastructure.

### Test Coverage

Existing integration tests cover selected security and data consistency scenarios, including:

- Authentication and authorization behavior
- Category operation permissions
- Category creation with anti-forgery protection
- Category soft deletion
- Article reassignment during category deletion
- Transaction rollback when a database operation fails

Manual testing has also been performed for:

- User login and navigation
- Article creation and editing
- Article ownership restrictions
- Article archiving
- Category creation, editing, deletion, and restoration
- Article and category relationships
- Administrator management workflows

### Test Database Isolation

Integration tests use a separate database:

| Environment | Database |
|---|---|
| Development | `LoginExampleDb` |
| Testing | `LoginExample_TestDb` |

When the application runs in the `Testing` environment, startup validates the configured SQL Server target.

The expected test database configuration is:

- **Server:** `(localdb)\MSSQLLocalDB`
- **Database:** `LoginExample_TestDb`

This validation helps prevent integration tests from accidentally connecting to the development database.

The test database must be configured and initialized according to the test project's setup requirements.

### Running Automated Tests

From the solution directory, automated tests can be executed using:

```powershell
dotnet test
```

Before running integration tests, verify that their configuration points to the dedicated test database.

The test infrastructure and any required test database initialization must be available for the tests to run successfully.

## Security Considerations

The application includes several security-related controls:

- ASP.NET Core Identity authentication
- Role-based authorization
- Article ownership verification
- Server-side input validation
- Anti-forgery token validation for state-changing requests
- Database uniqueness constraints
- Transaction-based consistency for multi-step operations
- Separation between development and integration-test databases

User interface visibility is not treated as an authorization boundary.

For example, hiding an Edit button does not replace server-side authorization checks in the corresponding Controller action.

The project is intended for educational and portfolio development purposes. These controls do not constitute a complete production security assessment.

## Development Status

### Completed

- [x] ASP.NET Core Identity integration
- [x] User authentication and role authorization
- [x] Article creation, editing, and archiving
- [x] Article ownership authorization
- [x] Category management
- [x] Category soft deletion and restoration
- [x] Reserved system category
- [x] Transaction-based article reassignment
- [x] Integration tests for selected critical scenarios
- [x] Article and category navigation
- [x] Initial project documentation

### Planned

- [ ] Linux deployment
- [ ] systemd service configuration
- [ ] Production-oriented configuration management
- [ ] Repeatable deployment scripts
- [ ] Automated deployment workflow
- [ ] Deployment and operations documentation

## Project Purpose

LoginExample is a practical backend engineering project intended to strengthen skills in:

- C# and ASP.NET Core development
- Authentication and authorization
- Relational data modeling
- Entity Framework Core
- Secure HTTP request handling
- Data integrity and transactional operations
- Integration testing
- Software maintenance and version control

The longer-term objective is to develop the application toward a deployable and maintainable system, including Linux hosting, service management, configuration security, and deployment automation.
