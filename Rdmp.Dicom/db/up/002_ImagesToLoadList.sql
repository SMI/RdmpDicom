--Version:1.1.0.1
--Description:Adds a flag which states whether the UID represented by the mapping is a reference to an item not present in the released dataset
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ImagesToLoadList')
BEGIN
	CREATE TABLE [dbo].[ImagesToLoadList](
	[SOPInstanceUID] [varchar](100) NOT NULL,
	[ImageLocation] [varchar](max) NOT NULL,
	[ProcessedDataLoadRunID] [int] NULL,
 CONSTRAINT [PK_ImagesToLoadList] PRIMARY KEY CLUSTERED 
(
	[SOPInstanceUID] ASC
)
) 
CREATE NONCLUSTERED INDEX ix_processed   
    ON [dbo].[ImagesToLoadList] (ProcessedDataLoadRunID);   
END
