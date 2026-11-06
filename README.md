# Pharmacy Management System

A web-based pharmacy management application built with **ASP.NET Core MVC**, **Entity Framework Core**, and **SQL Server**.

The project focuses on organizing day-to-day pharmacy operations through a structured MVC application, with attention to inventory workflows, sales, purchasing, security, and bilingual user experience.

## Tech Stack

- C# / .NET 10
- ASP.NET Core MVC
- Entity Framework Core 10
- Microsoft SQL Server
- Razor Views
- MailKit for email-related functionality
- Resource-based localization (Arabic and English)

## Main Areas

- **Authentication and Authorization** — account workflows and role-based access.
- **Products and Categories** — pharmacy product catalog management.
- **Inventory and Locations** — inventory tracking and product storage locations.
- **Purchases** — purchase transaction workflows.
- **Sales and Returns** — transaction services for sales and returns.
- **FEFO Allocation** — service-based allocation of batches using First Expired, First Out.
- **Audit Logs** — tracking important system activities.
- **Dashboard and Notifications** — operational overview and alerts.
- **Localization** — Arabic and English resource files.

## Architecture

The application follows the ASP.NET Core MVC pattern and separates responsibilities across:

- Controllers
- Domain entities and ViewModels
- Application services
- Data access and Entity Framework Core
- Validation and authorization components
- Localization resources

## Getting Started

1. Clone the repository.
2. Open `PharmacySystem.slnx` in Visual Studio or another compatible .NET IDE.
3. Configure the SQL Server connection string in your local configuration.
4. Restore NuGet packages and build the solution.
5. Review the existing EF Core migration state before applying database changes.
6. Run the application using your IDE or the .NET CLI.

> **Security:** Never commit real passwords, connection strings, SMTP credentials, API keys, or other secrets. Use local configuration or user secrets for sensitive values.

## Project Status

This README documents the repository structure and currently represented application areas. Refer to the source code for the exact implementation details and current behavior.

---

*Built as a practical ASP.NET Core MVC project focused on pharmacy operations.*
