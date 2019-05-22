--Version:1.3.0.1
--Description: This saves the files that cause exceptions when running pipelines to be looked at later
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='TagPromotionConfiguration')
BEGIN
CREATE TABLE [dbo].[TagPromotionConfiguration](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] varchar(250) NOT NULL,
	[FileFinderPipeline_ID] [int] NULL,
	[TagStorePopulationPipeline_ID] [int] NULL,
	[PromotionPipeline_ID] [int] NULL,
	[MongoTagStore_ID] [int] NULL,
	[Catalogue_ID] [int] NULL,
 CONSTRAINT [PK_TagPromotionConfiguration] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)
)
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='MongoTagStore')
BEGIN

CREATE TABLE [dbo].[MongoTagStore](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[DataAccessCredentials_ID] [int] NULL,
	[Name] [varchar](250) NOT NULL,
	[Host] [varchar](255) NULL,
	[Database] varchar(100) null,
	[Port] [int] NOT NULL,
	[Collection] varchar(250) null,
	
 CONSTRAINT [PK_MongoTagStore] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)
)
END

if not exists (select 1 from sys.foreign_keys where name='FK_TagPromotionConfiguration_MongoTagStore')
begin

ALTER TABLE [dbo].[TagPromotionConfiguration]  WITH CHECK ADD  CONSTRAINT [FK_TagPromotionConfiguration_MongoTagStore] FOREIGN KEY([MongoTagStore_ID])
REFERENCES [dbo].[MongoTagStore] ([ID])
ON DELETE SET NULL

--Ensure there can be only 1 MongoTagStore per TagPromotionConfiguration and that a MongoTagStore cannot belong to multiple TagPromotionConfigurations
CREATE UNIQUE NONCLUSTERED INDEX idx_MongoTagStore1To1Relationship
ON TagPromotionConfiguration(MongoTagStore_ID)
WHERE MongoTagStore_ID IS NOT NULL;

end