-- Initial migration-ready schema aligned with ApplicationDbContext mappings
-- Tables: Users, AppRoles, AppSections, AppAttendances, AttendanceEditLogs

CREATE TABLE AppRoles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL,
    Description NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

CREATE UNIQUE INDEX IX_AppRoles_Name ON AppRoles(Name);
GO

CREATE TABLE AppSections (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NULL
);
GO

CREATE UNIQUE INDEX IX_AppSections_Name ON AppSections(Name);
GO

CREATE TABLE Users (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Phone NVARCHAR(20) NULL,
    Address NVARCHAR(200) NULL,
    RoleId INT NOT NULL,
    SectionId INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_Users_AppRoles FOREIGN KEY (RoleId) REFERENCES AppRoles(Id),
    CONSTRAINT FK_Users_AppSections FOREIGN KEY (SectionId) REFERENCES AppSections(Id)
);
GO

CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);
GO

CREATE TABLE AppAttendances (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    AttendanceDate DATETIME2 NOT NULL,
    InTime TIME(7) NULL,
    OutTime TIME(7) NULL,
    Status NVARCHAR(20) NOT NULL,
    TotalWorkedMinutes INT NOT NULL DEFAULT 0,
    OvertimeMinutes INT NOT NULL DEFAULT 0,
    RegularWorkedMinutes INT NOT NULL DEFAULT 0,
    IsLocked BIT NOT NULL DEFAULT 0,
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_AppAttendances_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

CREATE UNIQUE INDEX IX_AppAttendances_UserId_AttendanceDate ON AppAttendances(UserId, AttendanceDate);
GO

CREATE TABLE AttendanceEditLogs (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AttendanceId INT NOT NULL,
    EditedByUserId INT NOT NULL,
    EditReason NVARCHAR(1000) NULL,
    OldInTime TIME(7) NULL,
    OldOutTime TIME(7) NULL,
    OldStatus NVARCHAR(20) NULL,
    OldTotalWorkedMinutes INT NULL,
    OldOvertimeMinutes INT NULL,
    OldRegularWorkedMinutes INT NULL,
    OldNotes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_AttendanceEditLogs_AppAttendances FOREIGN KEY (AttendanceId) REFERENCES AppAttendances(Id),
    CONSTRAINT FK_AttendanceEditLogs_Users FOREIGN KEY (EditedByUserId) REFERENCES Users(Id)
);
GO
