# RaceDay --- Programming POE Part 2

## 1. Project Overview

RaceDay is a REST API for a race event management system. It supports
race organisers who create and manage events, and participants who
browse events, enrol in race categories, and view their results.

Part 2 develops the planned system into a working ASP.NET Core Web API
connected to a database. It includes session-based authentication,
role-based access control, database operations, API documentation, and
automated tests.

## 2. Main Features

-   Register and log in as an **Organiser** or **Participant**.
-   Log out and manage an authenticated session.
-   View and update a user's profile.
-   Upload a profile picture.
-   Browse race events and view event details.
-   Allow organisers to create, update, view and delete their events.
-   Allow organisers to create, update and delete event categories.
-   Allow participants to enrol in events and view or cancel their own
    enrolments.
-   Allow organisers to view enrolments for their events.
-   Allow organisers to capture and update race results.
-   Allow participants to view their own results.
-   Test API behaviour and access permissions using automated tests.

## 3. Technologies Used

-   C#
-   ASP.NET Core Web API (.NET 8)
-   Entity Framework Core 8
-   Microsoft SQL Server / LocalDB
-   Swagger / OpenAPI
-   xUnit
-   ASP.NET Core integration testing
-   Visual Studio
-   Git and GitHub

## 4. Project Structure

``` text
PROG6212PART2-master/
├── RaceDay.Api/
│   ├── Controllers/       # API endpoints for authentication, users, events,
│   │                      # categories, enrolments and results
│   ├── Data/              # Entity Framework database context
│   ├── Dtos/              # Request and response objects used by the API
│   ├── Models/            # Database entity models
│   ├── Security/          # Password hashing, session authorisation and helpers
│   ├── wwwroot/           # Static files and uploaded profile pictures
│   ├── Program.cs         # Application setup and middleware configuration
│   ├── appsettings.json   # Connection string and application settings
│   └── RaceDay.Api.csproj
├── RaceDay.Tests/
│   ├── AuthTests.cs
│   ├── EnrolmentTests.cs
│   ├── EventTests.cs
│   ├── ResultTests.cs
│   ├── RoleEnforcementTests.cs
│   └── TestSupport.cs
├── RaceDay.sln
└── README.md
```

## 5. Requirements

Before running the project, make sure you have:

1.  Visual Studio 2022 or a compatible IDE with the **.NET 8 SDK**
    installed.
2.  Microsoft SQL Server LocalDB installed, or access to a SQL Server
    instance.
3.  NuGet package restore enabled so the solution can download its
    dependencies.

## 6. Database Configuration

The API uses Entity Framework Core with SQL Server. The default
connection string is configured in:

`RaceDay.Api/appsettings.json`

The current default connection string uses SQL Server LocalDB and
creates or connects to a database named `RaceDayDb`.

If your computer uses a different SQL Server instance, update the
`ConnectionStrings:RaceDayDb` setting before running the API. For
example, change the server part of the connection string to match your
installed SQL Server instance.

On startup, the application checks the database. It applies migrations
if migrations are present; otherwise, it creates the database from the
Entity Framework model when needed.

The application model includes these main entities:

-   `AppUser`
-   `EventType`
-   `Event`
-   `Category`
-   `Enrolment`
-   `Result`

The event types **Run**, **Walk** and **Cycle** are configured as
initial data in the model.

### Organiser registration code

Organiser registration checks the `Registration:OrganiserCode` setting
in `appsettings.json`. Configure this value for your own local
environment and do not publish real secrets in a public repository.

## 7. How to Run the API

1.  Download or clone this repository.
2.  Open `RaceDay.sln` in Visual Studio.
3.  Allow Visual Studio to restore the NuGet packages.
4.  Check the database connection string in
    `RaceDay.Api/appsettings.json`.
5.  Set **RaceDay.Api** as the startup project.
6.  Build the solution.
7.  Run the project.

When the API starts, Swagger UI is configured at:

`/swagger`

If you are running the project locally, open the Swagger address shown
in the browser or in Visual Studio's output. The root address is
configured to redirect to Swagger.

## 8. Using the API in Swagger

Swagger provides an interactive page for viewing and trying the API
endpoints.

### Authentication

1.  Open Swagger.
2.  Use `POST /api/auth/register` to create an account.
3.  To register as an Organiser, provide the configured organiser
    registration code.
4.  Use `POST /api/auth/login` with the registered email and password.
5.  The API uses a session cookie to maintain the login. When testing in
    the same Swagger/browser session, keep using that session for
    protected requests.
6.  Use `POST /api/auth/logout` to end the session.

### Main endpoints

  ----------------------------------------------------------------------------------------
  Method                  Endpoint                                 Purpose / access
  ----------------------- ---------------------------------------- -----------------------
  POST                    `/api/auth/register`                     Register an account

  POST                    `/api/auth/login`                        Log in

  POST                    `/api/auth/logout`                       Log out; requires a
                                                                   session

  GET                     `/api/users/me`                          View the current user's
                                                                   profile; requires a
                                                                   session

  PUT                     `/api/users/me`                          Update the current
                                                                   user's profile;
                                                                   requires a session

  PUT                     `/api/users/me/profile-picture`          Upload a profile
                                                                   picture; requires a
                                                                   session

  GET                     `/api/events`                            Browse events

  GET                     `/api/events/{id}`                       View an event

  POST                    `/api/events`                            Create an event;
                                                                   Organiser only

  PUT                     `/api/events/{id}`                       Update an event;
                                                                   Organiser only

  DELETE                  `/api/events/{id}`                       Delete an event;
                                                                   Organiser only

  GET                     `/api/events/mine`                       View events managed by
                                                                   the logged-in Organiser

  GET                     `/api/events/{eventId}/categories`       View an event's
                                                                   categories

  POST                    `/api/events/{eventId}/categories`       Add a category;
                                                                   Organiser only

  PUT                     `/api/categories/{id}`                   Update a category;
                                                                   Organiser only

  DELETE                  `/api/categories/{id}`                   Delete a category;
                                                                   Organiser only

  POST                    `/api/events/{eventId}/enrolments`       Enrol in an event;
                                                                   Participant only

  GET                     `/api/enrolments/mine`                   View the logged-in
                                                                   Participant's
                                                                   enrolments

  GET                     `/api/events/{eventId}/enrolments`       View enrolments for an
                                                                   event; Organiser only

  DELETE                  `/api/enrolments/{id}`                   Cancel an enrolment;
                                                                   Participant only

  POST                    `/api/enrolments/{enrolmentId}/result`   Capture a result;
                                                                   Organiser only

  PUT                     `/api/results/{id}`                      Update a result;
                                                                   Organiser only

  GET                     `/api/results/mine`                      View the logged-in
                                                                   Participant's results

  GET                     `/api/events/{eventId}/results`          View results for an
                                                                   event; Organiser only
  ----------------------------------------------------------------------------------------

**Note:** Protected endpoints require an active session. Organiser-only
and Participant-only endpoints also check the user's role.

## 9. Authentication and Security

-   Passwords are hashed before being stored; the plain-text password is
    not returned by the API.
-   Login creates a server-side session that stores the user's ID and
    role.
-   Logout clears the current session.
-   Session-protected endpoints reject requests without an authenticated
    session.
-   Role checks prevent Participants from using Organiser-only functions
    and Organisers from using Participant-only functions.
-   Organiser registration uses a configurable registration code.

For deployment beyond a local development environment, review the
database connection string, organiser registration code, HTTPS settings,
session configuration, and other environment-specific security settings.
Do not commit production credentials or secrets to GitHub.

## 10. Running the Automated Tests

The solution includes the `RaceDay.Tests` project, which uses xUnit and
ASP.NET Core integration testing.

To run the tests in Visual Studio:

1.  Open `RaceDay.sln`.
2.  Build the solution.
3.  Open **Test \> Test Explorer**.
4.  Select **Run All Tests**.

Alternatively, open a terminal in the folder containing `RaceDay.sln`
and run:

``` bash
dotnet test
```

The tests cover areas including authentication, event operations,
enrolments, results, and role-based access restrictions. The test
project uses an in-memory database for its API tests, so those tests do
not need to use the local SQL Server database.

## 11. Part 2 Deliverables

This repository contains the Part 2 implementation, including:

-   ASP.NET Core REST API.
-   Entity Framework Core data access and entity configuration.
-   Authentication and session handling.
-   Organiser and Participant role-based access control.
-   Endpoints for user profiles, events, categories, enrolments and
    results.
-   Swagger API documentation.
-   Automated xUnit tests.
-   Solution and project files for opening and running the application
    in Visual Studio.

## 12. Author

**Mmabatho Vilakazi**\
**RaceDay --- Programming POE Part 2**
