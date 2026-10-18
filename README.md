## Part 1 — Programming POE

RaceDay is a web-based race event management system designed to manage users, race events, event types, categories, enrolments and race results.

This repository contains the Part 1 work for the RaceDay Programming POE, including the Entity Relationship Diagram (ERD), SQL database script and API endpoint plan.

## Project Overview

The RaceDay system is designed to support the management of race events and the people involved in those events.

The database stores information about:

* System users
* Race events
* Event types
* Event categories
* Participant enrolments
* Race results

The database uses primary keys and foreign keys to connect the different entities and maintain relationships between the data.

## Database Entities

The RaceDay database contains the following six main tables:

### 1. AppUser

The AppUser table stores information about users of the system.

Users can have different roles, such as Organiser or Participant.

**Primary Key:**UserID

Main attributes include:

* UserID
* UserName
* UserSurname
* Email
* PasswordHash
* Role
* PhoneNumber

### 2. EventType

The EventType table stores the different types of events, such as Run, Walk and Cycle.

**Primary Key:** EventTypeID

Main attribute:

* TypeName

### 3. Event

The Event table stores information about individual race events.

**Primary Key:** EventID

**Foreign Keys:**

* EventTypeID → EventType(EventTypeID)
* UserID → AppUser(UserID)

Main attributes include:

* EventID
* EventName
* Description
* EventDate
* EventLocation
* Distance
* EventTypeID
* UserID

### 4. Category

The Category table stores categories for events. Categories can represent different age groups or race divisions.

**Primary Key:** CategoryID

**Foreign Key:**

* EventID → Event(EventID)

Main attributes include:

* CategoryID
* CategoryName
* Description
* Age
* Distance
* EventID

### 5. Enrolment

The Enrolment table records users who enrol for events and the category they enter.

**Primary Key:** EnrolmentID

**Foreign Keys:**

* EventID → Event(EventID)
* CategoryID` → Category(CategoryID)
* UserID → AppUser(UserID)

Main attributes include:

* EnrolmentID
* EnrolmentDate
* Status
* EventID
* CategoryID
* UserID

### 6. Result

The Result table stores the performance of an enrolled participant after an event.

**Primary Key:** ResultID

**Foreign Keys:**

* EnrolmentID → Enrolment(EnrolmentID)
* EventID → Event(EventID)

Main attributes include:

* ResultID
* FinishTime
* FinishPosition
* PublishedDate
* TotalPosition
* EnrolmentID
* EventID

## Entity Relationships

The main relationships in the RaceDay database are:

* One **AppUser** can organise many **Events**.
* One **EventType** can be associated with many **Events**.
* One **Event** belongs to one **EventType**.
* One **Event** is associated with one organising **AppUser**.
* One **Event** can have many **Categories**.
* One **Category** belongs to one **Event**.
* One **AppUser** can have many **Enrolments**.
* One **Event** can have many **Enrolments**.
* One **Category** can be selected by many **Enrolments**.
* One **Enrolment** belongs to one **AppUser**, one **Event** and one **Category**.
* One **Enrolment** can have a corresponding **Result**.
* One **Event** can be associated with many **Results**.

## Entity Relationship Diagram

The Entity Relationship Diagram shows the database entities, attributes, primary keys, foreign keys and relationships.

The ERD is available in the repository:

-RACEDAY.ERD (1).png

## SQL Database

The SQL script creates the RaceDay database and its tables.

The database created by the script is:

-RaceDayDb

The SQL script includes:

* Database creation
* Table creation
* Primary keys
* Foreign keys
* Relationships
* Sample data

The SQL file is located in:

-Docs/RaceDaySQL.sql

### Running the Database

To run the database:

1. Open **SQL Server Management Studio (SSMS)**.
2. Open Docs/RaceDaySQL.sql.
3. Execute the script.
4. Refresh the Databases section.
5. Confirm that RaceDayDb has been created.
6. Expand the database and check that the six tables have been created.

## API Endpoint Plan

The API endpoint plan describes how the RaceDay application will communicate with the system's data through REST API endpoints.

The API documentation is provided in:

-Docs/RaceDay API Endpoint Plan.pdf

The planned API covers the main RaceDay resources and provides endpoints for working with the system data.

## Sample Data

The SQL script includes sample data for testing the database.

The sample data includes:

* Organisers
* Participants
* Event types
* Race events
* Event categories
* Enrolments
* Race results

Example event types include:

* Run
* Walk
* Cycle

## Technologies and Tools

The following technologies and tools are used for the Part 1 work:

* Microsoft SQL Server
* SQL
* SQL Server Management Studio
* Visual Studio
* GitHub
* diagrams.net / draw.io
* REST API design
* Markdown

## Repository Structure


RaceDay/
│
├── Docs/
│   ├── RaceDay API Endpoint Plan.pdf
│   └── RaceDaySQL.sql
│
├── RACEDAY.ERD (1).png
│
└── README.md

CI/CD Check Result
<img width="1887" height="697" alt="CI" src="https://github.com/user-attachments/assets/15f02ff0-9ed6-49c0-957c-f0dac6375980" />



Youtube: https://youtu.be/shMkz-KFS4w

## Part 1 Deliverables

The repository contains the following Part 1 deliverables:

* Entity Relationship Diagram (ERD)
* Database design
* Primary keys
* Foreign keys
* Entity relationships
* SQL database script
* Sample database data
* API endpoint plan
* GitHub repository documentation
* Video link

## Author

**Mmabatho Vilakazi**

**RaceDay — Programming POE Part 1**
