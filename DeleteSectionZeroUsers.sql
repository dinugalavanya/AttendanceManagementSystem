-- Delete users with SectionId = 0 (All Sections)
-- Section 0 is only for SuperAdmin and GM, not for regular users
DELETE FROM Users WHERE SectionId = 0;
