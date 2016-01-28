--change AppSettings to whatever you like
--when changing setting in database keep in mind that PeriodicSettingsManager will see change only if [UpdatedAt] is more than last read time
CREATE TABLE [dbo].[AppSettings]
(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Category] [varchar](200) NOT NULL,
	[Name] [varchar](200) NOT NULL,
	[Value] [nvarchar](max) NULL,
	[UpdatedAt] [datetime] NOT NULL CONSTRAINT [DF_AppSettings_UpdatedAt]  DEFAULT (getutcdate()),
	[Description] [nvarchar](max) NULL,
	CONSTRAINT [PK_AppSettings] PRIMARY KEY CLUSTERED (	[Id] ASC )
)

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AppSettings_Category_Name] 
ON [dbo].[Settings] ([Name], [Category])

GO

CREATE NONCLUSTERED INDEX [IX_AppSettings_Category_UpdatedAt] 
ON [dbo].[Settings] ([Category],[UpdatedAt])
INCLUDE ([Name],[Value])
GO