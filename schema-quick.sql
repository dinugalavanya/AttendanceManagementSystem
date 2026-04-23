-- =============================================
-- Attendance Management System Database Schema (Quick Setup)
-- SQL Server Compatible
-- =============================================

-- Create database if not exists
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AttendanceDB')
BEGIN
    CREATE DATABASE AttendanceDB;
END
GO

USE AttendanceDB;
GO

-- =============================================
-- ROLES TABLE
-- =============================================
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(200) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NULL
);
GO

-- =============================================
-- SECTIONS TABLE
-- =============================================
CREATE TABLE Sections (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NULL
);
GO

-- =============================================
-- USERS TABLE
-- =============================================
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    PhoneNumber NVARCHAR(20) NULL,
    Address NVARCHAR(500) NULL,
    RoleId INT NOT NULL,
    SectionId INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NULL,
    
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id),
    CONSTRAINT FK_Users_Sections FOREIGN KEY (SectionId) REFERENCES Sections(Id)
);
GO

-- =============================================
-- ATTENDANCES TABLE
-- =============================================
CREATE TABLE Attendances (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    AttendanceDate DATE NOT NULL,
    InTime TIME(7) NULL,
    OutTime TIME(7) NULL,
    Status NVARCHAR(20) NOT NULL,
    TotalWorkedMinutes INT NOT NULL DEFAULT 0,
    OvertimeMinutes INT NOT NULL DEFAULT 0,
    RegularWorkedMinutes INT NOT NULL DEFAULT 0,
    IsLocked BIT NOT NULL DEFAULT 0,
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NULL,
    
    CONSTRAINT FK_Attendances_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT UQ_Attendances_User_Date UNIQUE (UserId, AttendanceDate)
);
GO

-- =============================================
-- ATTENDANCE EDIT LOGS TABLE
-- =============================================
CREATE TABLE AttendanceEditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
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
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT FK_AttendanceEditLogs_Attendances FOREIGN KEY (AttendanceId) REFERENCES Attendances(Id),
    CONSTRAINT FK_AttendanceEditLogs_Users FOREIGN KEY (EditedByUserId) REFERENCES Users(Id)
);
GO

-- =============================================
-- INDEXES FOR PERFORMANCE
-- =============================================
CREATE INDEX IX_Users_RoleId ON Users(RoleId);
CREATE INDEX IX_Users_SectionId ON Users(SectionId);
CREATE INDEX IX_Users_IsActive ON Users(IsActive);
CREATE INDEX IX_Users_Email ON Users(Email);

CREATE INDEX IX_Sections_IsActive ON Sections(IsActive);
CREATE INDEX IX_Sections_Name ON Sections(Name);

CREATE INDEX IX_Attendances_UserId ON Attendances(UserId);
CREATE INDEX IX_Attendances_AttendanceDate ON Attendances(AttendanceDate);
CREATE INDEX IX_Attendances_Status ON Attendances(Status);
CREATE INDEX IX_Attendances_IsLocked ON Attendances(IsLocked);

CREATE INDEX IX_AttendanceEditLogs_AttendanceId ON AttendanceEditLogs(AttendanceId);
CREATE INDEX IX_AttendanceEditLogs_EditedByUserId ON AttendanceEditLogs(EditedByUserId);
GO

-- =============================================
-- SAMPLE DATA INSERTION
-- =============================================

-- Insert Roles
INSERT INTO Roles (Name, Description, IsActive) VALUES
('SuperAdmin', 'Super Administrator with full system access', 1),
('Admin', 'Section Administrator with limited access', 1),
('Worker', 'Regular worker who can mark attendance', 1);
GO

-- Insert Sections
INSERT INTO Sections (Name, Description, IsActive) VALUES
('Information Technology', 'IT Department and Software Development', 1),
('Human Resources', 'Human Resources and Administration', 1),
('Finance', 'Accounting and Financial Management', 1),
('Marketing', 'Marketing and Sales', 1),
('Operations', 'Operations and Logistics', 1);
GO

-- Insert Super Admin
INSERT INTO Users (FirstName, LastName, Email, PasswordHash, PhoneNumber, Address, RoleId, SectionId, IsActive) VALUES
('Super', 'Admin', 'superadmin@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234567', '123 Main Street, Colombo', 1, NULL, 1);
GO

-- Insert Admin Users
INSERT INTO Users (FirstName, LastName, Email, PasswordHash, PhoneNumber, Address, RoleId, SectionId, IsActive) VALUES
('John', 'Smith', 'john.smith@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234568', '456 Park Avenue, Colombo', 2, 1, 1),
('Sarah', 'Johnson', 'sarah.johnson@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234569', '789 Queen Street, Colombo', 2, 2, 1),
('Michael', 'Brown', 'michael.brown@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234570', '321 King Street, Colombo', 2, 3, 1),
('Emily', 'Davis', 'emily.davis@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234571', '654 Prince Street, Colombo', 2, 4, 1),
('David', 'Wilson', 'david.wilson@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234572', '987 Duke Street, Colombo', 2, 5, 1);
GO

-- Insert Worker Users
INSERT INTO Users (FirstName, LastName, Email, PasswordHash, PhoneNumber, Address, RoleId, SectionId, IsActive) VALUES
('Robert', 'Taylor', 'robert.taylor@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234573', '147 Church Street, Colombo', 3, 1, 1),
('Lisa', 'Anderson', 'lisa.anderson@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234574', '258 Temple Street, Colombo', 3, 1, 1),
('James', 'Thomas', 'james.thomas@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234575', '369 Mosque Street, Colombo', 3, 1, 1),
('Jennifer', 'Jackson', 'jennifer.jackson@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234576', '741 Beach Road, Colombo', 3, 1, 1),
('William', 'White', 'william.white@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234577', '852 Galle Road, Colombo', 3, 2, 1),
('Patricia', 'Harris', 'patricia.harris@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234578', '963 Marine Drive, Colombo', 3, 2, 1),
('Christopher', 'Martin', 'christopher.martin@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234579', '147 Harbour Road, Colombo', 3, 2, 1),
('Daniel', 'Garcia', 'daniel.garcia@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234580', '258 Bank Street, Colombo', 3, 3, 1),
('Nancy', 'Martinez', 'nancy.martinez@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234581', '369 Exchange Road, Colombo', 3, 3, 1),
('Mark', 'Robinson', 'mark.robinson@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234582', '741 Media Avenue, Colombo', 3, 4, 1),
('Linda', 'Clark', 'linda.clark@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234583', '852 Advertising Street, Colombo', 3, 4, 1),
('Paul', 'Rodriguez', 'paul.rodriguez@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234584', '963 Logistics Road, Colombo', 3, 5, 1),
('Karen', 'Lewis', 'karen.lewis@attendance.com', '$2a$11$LQv3c1yqF5mK5E8J9Yv4mK8hK2r', '+94771234585', '147 Warehouse Street, Colombo', 3, 5, 1);
GO

-- Insert Sample Attendance Data (Last 30 days)
DECLARE @StartDate DATE = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
DECLARE @EndDate DATE = CAST(GETDATE() AS DATE);

INSERT INTO Attendances (UserId, AttendanceDate, InTime, OutTime, Status, TotalWorkedMinutes, OvertimeMinutes, RegularWorkedMinutes)
SELECT 
    u.Id,
    DATEADD(DAY, v.number, @StartDate) AS AttendanceDate,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN NULL -- Weekends
        ELSE CASE 
            WHEN v.number % 7 = 0 THEN '08:30' -- On time
            ELSE '08:45' -- Some late days
        END
    END AS InTime,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN NULL -- Weekends
        ELSE CASE 
            WHEN v.number % 7 = 0 THEN '17:30' -- Regular days
            ELSE '17:15' -- Some late days
        END
    END AS OutTime,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN 'Absent' -- Weekends
        ELSE CASE 
            WHEN v.number % 10 = 0 THEN 'Present' -- Most days present
            WHEN v.number % 9 = 0 THEN 'Late' -- Some late days
            ELSE 'Present'
        END
    END AS Status,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN 0 -- Weekends
        ELSE 
            CASE 
                WHEN v.number % 10 = 0 THEN 540 -- 9 hours regular
                WHEN v.number % 8 = 0 THEN 570 -- 9.5 hours with overtime
                ELSE 510 -- 8.5 hours
            END
    END AS TotalWorkedMinutes,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN 0 -- Weekends
        ELSE 
            CASE 
                WHEN v.number % 8 = 0 THEN 60 -- Some overtime days
                ELSE 0
            END
    END AS OvertimeMinutes,
    CASE 
        WHEN DATEPART(WEEKDAY, DATEADD(DAY, v.number, @StartDate)) IN (1, 7) THEN 0 -- Weekends
        ELSE 
            CASE 
                WHEN v.number % 10 = 0 THEN 480 -- Regular work hours
                WHEN v.number % 8 = 0 THEN 480 -- Regular work hours
                ELSE 480 -- Regular work hours
            END
    END AS RegularWorkedMinutes,
    GETDATE() AS CreatedAt
FROM (
    SELECT 1 as v
    CROSS JOIN Users u ON u.Id IN (
        SELECT 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 -- IT Department Workers
        UNION ALL
        SELECT 16, 17, 18 -- HR Department Workers
        UNION ALL
        SELECT 19, 20 -- Finance Department Workers
        UNION ALL
        SELECT 21, 22 -- Marketing Department Workers
        UNION ALL
        SELECT 23, 24 -- Operations Department Workers
    )
    WHERE DATEADD(DAY, v.number, @StartDate) <= @EndDate
) v;
GO

PRINT 'Database setup completed successfully!';
PRINT 'Sample data inserted successfully!';
PRINT 'Default login: superadmin@attendance.com / Admin@123';
GO
