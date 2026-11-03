IF DB_ID('RaceDay') IS NULL
    CREATE DATABASE RaceDay;

USE RaceDay;

-- Drop tables in reverse dependency order so the script can be re-run safely
DROP TABLE IF EXISTS Results;
DROP TABLE IF EXISTS Enrollment;
DROP TABLE IF EXISTS EventCategories;
DROP TABLE IF EXISTS Events;
DROP TABLE IF EXISTS Venues;
DROP TABLE IF EXISTS Users;

-- Organisers and participants
CREATE TABLE Users (
    User_ID      INT IDENTITY(1,1) PRIMARY KEY,
    FullName     VARCHAR(100) NOT NULL,
    Email        VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    Role         VARCHAR(20)  NOT NULL,
    CreatedAt    DATETIME     NOT NULL DEFAULT GETDATE(),
    Gender       VARCHAR(20)  NULL,
    DateOfBirth  DATE         NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role  CHECK (Role IN ('Organiser', 'Participant'))
);

-- Where events take place
CREATE TABLE Venues (
    Venue_ID INT IDENTITY(1,1) PRIMARY KEY,
    Name     VARCHAR(100) NOT NULL,
    City     VARCHAR(50)  NOT NULL,
    Province VARCHAR(50)  NOT NULL,
    Address  VARCHAR(150) NOT NULL
);

-- Created by an organiser, held at a venue
CREATE TABLE Events (
    Event_ID     INT IDENTITY(1,1) PRIMARY KEY,
    Organiser_ID INT          NOT NULL,
    Venue_ID     INT          NOT NULL,
    Title        VARCHAR(100) NOT NULL,
    Description  VARCHAR(500) NOT NULL,
    EventDate    DATETIME     NOT NULL,
    Status       VARCHAR(50)  NOT NULL DEFAULT 'Upcoming',
    EventType    VARCHAR(50)  NOT NULL,
    FOREIGN KEY (Organiser_ID) REFERENCES Users(User_ID),
    FOREIGN KEY (Venue_ID)     REFERENCES Venues(Venue_ID)
);

-- Race categories within an event (e.g. 10km, 21km)
CREATE TABLE EventCategories (
    Category_ID      INT IDENTITY(1,1) PRIMARY KEY,
    Event_ID         INT           NOT NULL,
    CategoryName     VARCHAR(50)   NOT NULL,
    DistanceKm       DECIMAL(5,2)  NOT NULL,
    EntryFee         DECIMAL(10,2) NOT NULL,
    MaxParticipants  INT           NOT NULL,
    StartDateTime    DATETIME      NOT NULL,
    MinAge           INT           NULL,
    RouteDescription VARCHAR(MAX)  NULL,
    FOREIGN KEY (Event_ID) REFERENCES Events(Event_ID)
);

-- A participant entering a category (the table name is singular; the C# model maps to it with [Table("Enrollment")])
CREATE TABLE Enrollment (
    Enrollment_ID    INT IDENTITY(1,1) PRIMARY KEY,
    User_ID          INT         NOT NULL,
    Category_ID      INT         NOT NULL,
    EnrollmentDate   DATETIME    NOT NULL DEFAULT GETDATE(),
    PaymentStatus    VARCHAR(50) NOT NULL DEFAULT 'Pending',
    BibNumber        VARCHAR(20) NULL,
    EmergencyContact VARCHAR(50) NULL,
    FOREIGN KEY (User_ID)     REFERENCES Users(User_ID),
    FOREIGN KEY (Category_ID) REFERENCES EventCategories(Category_ID),
    CONSTRAINT UQ_Enrollment_User_Category UNIQUE (User_ID, Category_ID)
);

-- One result per enrollment
CREATE TABLE Results (
    Result_ID         INT IDENTITY(1,1) PRIMARY KEY,
    Enrollment_ID     INT          NOT NULL,
    FinishTimeMinutes DECIMAL(6,2) NULL,
    OverallRank       INT          NULL,
    Completed         INT          NULL,
    CategoryRank      INT          NULL,
    FOREIGN KEY (Enrollment_ID) REFERENCES Enrollment(Enrollment_ID),
    CONSTRAINT UQ_Results_Enrollment UNIQUE (Enrollment_ID)
);

-- Sample data (IDENTITY_INSERT lets us pick the ids)
SET IDENTITY_INSERT Users ON;
INSERT INTO Users (User_ID, FullName, Email, PasswordHash, Role, CreatedAt, Gender, DateOfBirth) VALUES
(101, 'Iman Adams',       'iman@raceday.co.za',    'PBKDF2$100000$XDeW+D8etqNVru1VZHkYYg==$oYYoo4FF4seRg73F1Wfz/nrm/clpLbwsj1j5spSOePM=', 'Organiser',   '2026-01-10', 'Female', '1995-04-12'),
(102, 'Kiersten Smith',   'kiersten@events.co.za', 'PBKDF2$100000$/tMig1ex+e7lHHxndi81vw==$JCI8dnWyRoP9j27y9A6QvBmNo9341Ho3K6ktkfk9toE=', 'Organiser',   '2026-01-12', 'Female', '1998-08-20'),
(103, 'Mahmood Mohammad', 'mahmood@gmail.com',     'PBKDF2$100000$fNDhlk/FzcbfA6Yi77nl7Q==$4zX8HY8+r0t+0n3kKgM/sML2j4nGieQZFaM+8DxZRV8=', 'Participant', '2026-02-01', 'Male',   '2000-03-15'),
(104, 'Jamie Abrahams',   'jamie@yahoo.com',       'PBKDF2$100000$W8XDp1iPn8p/6gbTMATxgQ==$hyPSFlHxBfe7AOWmhvY6W6Z3yKg2Rs1IcrLLUz/3/bY=', 'Participant', '2026-02-05', 'Male',   '2002-11-30');
SET IDENTITY_INSERT Users OFF;

SET IDENTITY_INSERT Venues ON;
INSERT INTO Venues (Venue_ID, Name, City, Province, Address) VALUES
(201, 'Green Point Athletics Stadium', 'Cape Town',    'Western Cape', '1 Fritz Sonnenberg Rd'),
(202, 'Cape Town Stadium Grounds',     'Cape Town',    'Western Cape', 'V&A Waterfront'),
(203, 'Stellenbosch Rugby Fields',     'Stellenbosch', 'Western Cape', '12 Coetzenburg Rd');
SET IDENTITY_INSERT Venues OFF;

SET IDENTITY_INSERT Events ON;
INSERT INTO Events (Event_ID, Organiser_ID, Venue_ID, Title, Description, EventDate, Status, EventType) VALUES
(301, 101, 201, 'Cape Coastal Marathon',     'Scenic coastal road race', '2026-09-15 06:00:00', 'Completed', 'Running'),
(302, 102, 202, 'Community Trail Run',       'Township run event',       '2026-10-10 07:00:00', 'Completed', 'Running'),
(303, 101, 201, 'Peninsula Cycle Challenge', 'Scenic coastal cycle',     '2026-11-20 06:30:00', 'Upcoming',  'Cycling'),
(304, 102, 203, 'Winelands Fun Walk',        'Relaxed family walk through the vineyards', '2026-12-05 08:00:00', 'Upcoming', 'Walking');
SET IDENTITY_INSERT Events OFF;

SET IDENTITY_INSERT EventCategories ON;
INSERT INTO EventCategories (Category_ID, Event_ID, CategoryName, DistanceKm, EntryFee, MaxParticipants, StartDateTime, MinAge, RouteDescription) VALUES
(401, 301, 'Full Marathon', 42.20, 350.00, 2000, '2026-09-15 06:00:00', 18,   'Coastal route along Atlantic seaboard'),
(402, 302, '10km Road Run', 10.00, 120.00, 1500, '2026-10-10 07:00:00', 14,   'Flat street circuit'),
(403, 303, '45km Cycle',    45.00, 400.00, 2500, '2026-11-20 06:30:00', 16,   'Highway loop and coastal road'),
(404, 304, '5km Fun Walk',   5.00,  60.00,  800, '2026-12-05 08:00:00', NULL, 'Gentle loop through the vineyards');
SET IDENTITY_INSERT EventCategories OFF;

SET IDENTITY_INSERT Enrollment ON;
INSERT INTO Enrollment (Enrollment_ID, User_ID, Category_ID, EnrollmentDate, PaymentStatus, BibNumber, EmergencyContact) VALUES
(501, 103, 401, '2026-03-01', 'Paid', '1001', '0821234567'),
(502, 104, 402, '2026-03-05', 'Paid', '2005', '0839876543');
SET IDENTITY_INSERT Enrollment OFF;

SET IDENTITY_INSERT Results ON;
INSERT INTO Results (Result_ID, Enrollment_ID, FinishTimeMinutes, OverallRank, Completed, CategoryRank) VALUES
(601, 501, 215.45, 42, 1, 12),
(602, 502,  48.12, 10, 1,  3);
SET IDENTITY_INSERT Results OFF;

-- When executed it showcases all the tables
SELECT * FROM Users;
SELECT * FROM Venues;
SELECT * FROM Events;
SELECT * FROM EventCategories;
SELECT * FROM Enrollment;
SELECT * FROM Results;
