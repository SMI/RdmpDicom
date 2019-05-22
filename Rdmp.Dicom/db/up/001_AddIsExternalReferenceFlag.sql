--Version:1.0.0.1
--Description:Adds support for Image loading list in which UIDs are mapped to image locations and batches processed
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='UIDMapping' AND COLUMN_NAME='IsExternalReference')
BEGIN
	ALTER TABLE UIDMapping ADD IsExternalReference bit NOT NULL 
	CONSTRAINT [DF_UIDMapping_IsExternalReference] DEFAULT ((0))
END
