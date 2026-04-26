-- =============================================
-- ADD ATTENDANCE FIELDS TO USERS TABLE
-- =============================================

USE Atendance_Sheet;
GO

-- Add UpdatedAt column if it doesn't exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('AppUsers') AND name = 'UpdatedAt'
)
BEGIN
    ALTER TABLE AppUsers ADD UpdatedAt DATETIME2 NULL;
    PRINT 'Added UpdatedAt column to AppUsers';
END
GO

-- Add new attendance fields to AppUsers table if they don't exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('AppUsers') AND name = 'LoginTime'
)
BEGIN
    ALTER TABLE AppUsers ADD LoginTime TIME(7) NULL;
    PRINT 'Added LoginTime column to AppUsers';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('AppUsers') AND name = 'LogoutTime'
)
BEGIN
    ALTER TABLE AppUsers ADD LogoutTime TIME(7) NULL;
    PRINT 'Added LogoutTime column to AppUsers';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('AppUsers') AND name = 'AttendanceStatus'
)
BEGIN
    ALTER TABLE AppUsers ADD AttendanceStatus NVARCHAR(20) NULL;
    PRINT 'Added AttendanceStatus column to AppUsers';
END
GO

-- Verify the columns were added
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'AppUsers' 
AND COLUMN_NAME IN ('UpdatedAt', 'LoginTime', 'LogoutTime', 'AttendanceStatus')
ORDER BY COLUMN_NAME;
GO

PRINT 'Attendance fields added successfully to AppUsers table!';
GO
