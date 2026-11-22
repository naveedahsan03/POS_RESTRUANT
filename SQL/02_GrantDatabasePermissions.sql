/* ============================================================================
   RESTAURANT POS - SQL SERVER PERMISSIONS SCRIPT FOR CLIENT PC
   Grants: db_datareader, db_datawriter, db_owner on [RestaurantPOS_DB]
   
   How to run on Client PC:
   1. Open SQL Server Management Studio (SSMS) as Administrator / sysadmin (sa).
   2. Open this file and execute (F5).
   3. If using a specific SQL Login or Windows User in appsettings.json,
      set @TargetLogin below (e.g. N'pos_user' or N'DESKTOP-ABC\Cashier').
   ============================================================================ */

USE [RestaurantPOS_DB];
GO

SET NOCOUNT ON;

-- ============================================================================
-- OPTION 1: Grant permissions to a specific SQL Login or Windows User
-- Change N'pos_user' to your SQL Server Login / Windows account if needed.
-- (Set to NULL to skip creating a new SQL Login)
-- ============================================================================
DECLARE @TargetLogin NVARCHAR(128) = NULL; -- e.g. N'pos_user' or N'PC_NAME\User'
DECLARE @SqlPassword NVARCHAR(128) = N'Pos@123456'; -- Used only if creating a new SQL Login

IF @TargetLogin IS NOT NULL AND LTRIM(RTRIM(@TargetLogin)) <> N''
BEGIN
    -- Create Server Login if it does not exist (for SQL Login, not Windows '\' login)
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @TargetLogin)
    BEGIN
        IF CHARINDEX(N'\', @TargetLogin) > 0
        BEGIN
            DECLARE @CreateWinLogin NVARCHAR(MAX) = N'CREATE LOGIN ' + QUOTENAME(@TargetLogin) + N' FROM WINDOWS;';
            EXEC sp_executesql @CreateWinLogin;
        END
        ELSE
        BEGIN
            DECLARE @CreateSqlLogin NVARCHAR(MAX) = N'CREATE LOGIN ' + QUOTENAME(@TargetLogin)
                + N' WITH PASSWORD = ' + QUOTENAME(@SqlPassword, '''') + N', CHECK_POLICY = OFF;';
            EXEC sp_executesql @CreateSqlLogin;
        END
    END

    -- Create Database User if it does not exist
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @TargetLogin)
    BEGIN
        DECLARE @CreateDbUser NVARCHAR(MAX) = N'CREATE USER ' + QUOTENAME(@TargetLogin) + N' FOR LOGIN ' + QUOTENAME(@TargetLogin) + N';';
        EXEC sp_executesql @CreateDbUser;
    END

    -- Grant db_datareader, db_datawriter, db_owner
    DECLARE @GrantRoles NVARCHAR(MAX) =
        N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@TargetLogin) + N'; ' +
        N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@TargetLogin) + N'; ' +
        N'ALTER ROLE [db_owner]      ADD MEMBER ' + QUOTENAME(@TargetLogin) + N';';
    EXEC sp_executesql @GrantRoles;

    PRINT N'Granted db_datareader, db_datawriter, db_owner to: ' + @TargetLogin;
END
GO

-- ============================================================================
-- OPTION 2: Grant permissions to Current Windows User & Common App Accounts
-- (Safe to run: skips accounts that do not exist on this PC)
-- ============================================================================
DECLARE @Accounts TABLE (AccountName NVARCHAR(128));
INSERT INTO @Accounts (AccountName)
VALUES
    (ORIGINAL_LOGIN()),
    (N'NT AUTHORITY\NETWORK SERVICE'),
    (N'NT AUTHORITY\SYSTEM'),
    (N'IIS APPPOOL\DefaultAppPool');

DECLARE @Acc NVARCHAR(128);
DECLARE acc_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT AccountName FROM @Accounts WHERE AccountName IS NOT NULL;

OPEN acc_cursor;
FETCH NEXT FROM acc_cursor INTO @Acc;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        IF @Acc <> N'sa'
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @Acc)
            BEGIN
                DECLARE @SqlCreateLogin NVARCHAR(MAX) = N'CREATE LOGIN ' + QUOTENAME(@Acc) + N' FROM WINDOWS;';
                EXEC sp_executesql @SqlCreateLogin;
            END

            -- Skip if already mapped as dbo
            IF NOT EXISTS (
                SELECT 1 FROM sys.database_principals dp
                JOIN sys.server_principals sp ON dp.sid = sp.sid
                WHERE sp.name = @Acc AND dp.name = N'dbo'
            )
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @Acc)
                BEGIN
                    DECLARE @SqlCreateUser NVARCHAR(MAX) = N'CREATE USER ' + QUOTENAME(@Acc) + N' FOR LOGIN ' + QUOTENAME(@Acc) + N';';
                    EXEC sp_executesql @SqlCreateUser;
                END

                DECLARE @SqlGrant NVARCHAR(MAX) =
                    N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@Acc) + N'; ' +
                    N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@Acc) + N'; ' +
                    N'ALTER ROLE [db_owner]      ADD MEMBER ' + QUOTENAME(@Acc) + N';';
                EXEC sp_executesql @SqlGrant;

                PRINT N'Granted db_datareader, db_datawriter, db_owner to: ' + @Acc;
            END
        END
    END TRY
    BEGIN CATCH
        PRINT N'Skipped ' + @Acc + N': ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM acc_cursor INTO @Acc;
END

CLOSE acc_cursor;
DEALLOCATE acc_cursor;
GO

-- ============================================================================
-- VERIFY PERMISSIONS
-- ============================================================================
SELECT
    dp.name AS [DatabaseUser],
    dp.type_desc AS [UserType],
    r.name AS [AssignedRole]
FROM sys.database_role_members drm
JOIN sys.database_principals r  ON drm.role_principal_id = r.principal_id
JOIN sys.database_principals dp ON drm.member_principal_id = dp.principal_id
WHERE r.name IN (N'db_datareader', N'db_datawriter', N'db_owner')
ORDER BY dp.name, r.name;
GO
