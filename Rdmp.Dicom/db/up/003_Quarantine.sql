--Version:1.2.0.1
--Description: This saves the files that cause exceptions when running pipelines to be looked at later
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Quarantine')
BEGIN
	CREATE TABLE [dbo].[Quarantine](
	[SOPInstanceUID] [varchar](100) NULL,
	[ImageLocation] [varchar](260) NOT NULL,
	[ProcessedDataLoadRunID] [int] NULL,
	[ExceptionMessage] [varchar](max) NULL,
 CONSTRAINT [PK_Quarantine] PRIMARY KEY CLUSTERED 
(
	[ImageLocation] ASC
)
) 
END