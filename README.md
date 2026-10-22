# LoginExample

An ASP.NET Core MVC application built with .NET 10, Entity Framework Core, ASP.NET Core Identity, and SQL Server.

This project is developed as a practical exercise in backend engineering, focusing on authentication, authorization, data integrity, and application architecture.

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **Language:** C#
- **ORM:** Entity Framework Core
- **Database:** Microsoft SQL Server
- **Authentication:** ASP.NET Core Identity
- **Frontend:** Razor Views, Bootstrap
- **Testing:** xUnit, ASP.NET Core Integration Testing
- **Version Control:** Git

## Features

### Authentication & Authorization

- User registration and login
- Role-based authorization (Admin / User)
- Resource ownership authorization for articles
- Anti-forgery protection for state-changing operations

### Article Management

- Create articles with category selection
- Edit articles owned by the current user
- View published articles and their authors
- View article details and category information
- Archive published articles
- View personal archived articles

### Category Management

- Admin-only category creation and editing
- Category soft deletion and restoration
- Unique category name validation
- Reserved system category: `未分類`
- Automatic reassignment of articles when a category is deleted
- Transaction-based data consistency

## Architecture

The application follows the ASP.NET Core MVC pattern.

- **Models:** Database entities and domain data
- **ViewModels:** View-specific data and input validation
- **Controllers:** Request handling, authorization, and application logic
- **Views:** Razor-based user interface
- **Data:** Entity Framework Core DbContext and database configuration

## Testing

The project includes integration tests covering selected authorization and category management scenarios, including transaction rollback.

Manual testing is also used to verify article creation, editing, archiving, category management, and navigation workflows.

## Development Status

- [x] Authentication and role authorization
- [x] Article management
- [x] Category management
- [x] Soft deletion and transaction handling
- [x] Integration testing for selected critical scenarios
- [x] Navigation and management interface
- [ ] Linux deployment
- [ ] systemd service configuration
- [ ] Automated deployment workflow

## Project Purpose

The objective of this project is to develop and demonstrate practical ASP.NET Core backend engineering skills through incremental implementation, testing, and maintenance of a functional web application.

The longer-term goal is to extend the application with production-oriented configuration, deployment, and operational practices.
