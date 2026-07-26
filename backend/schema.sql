-- Run against a SQL Server database (or let EF Core migrations create this
-- automatically via `dotnet ef database update`)

CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    GitHubId NVARCHAR(50) NOT NULL,
    Username NVARCHAR(50) NOT NULL,
    DisplayName NVARCHAR(100) NULL,
    Bio NVARCHAR(500) NULL,
    AvatarUrl NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_GitHubId UNIQUE (GitHubId),
    CONSTRAINT UQ_Users_Username UNIQUE (Username)
);

CREATE TABLE Projects (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Title NVARCHAR(100) NOT NULL,
    Description NVARCHAR(1000) NULL,
    RepoUrl NVARCHAR(MAX) NULL,
    LiveUrl NVARCHAR(MAX) NULL,
    TechStack NVARCHAR(300) NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Projects_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Backs the main query the public profile page runs: "all projects for
-- this user, in display order"
CREATE INDEX IX_Projects_UserId_DisplayOrder ON Projects (UserId, DisplayOrder);

CREATE TABLE Contributions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    RepoName NVARCHAR(150) NOT NULL,
    ContributionType NVARCHAR(50) NOT NULL DEFAULT 'commit',
    Count INT NOT NULL DEFAULT 0,
    ContributionDate DATE NOT NULL,
    CONSTRAINT FK_Contributions_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Contributions_UserId_Date ON Contributions (UserId, ContributionDate);
