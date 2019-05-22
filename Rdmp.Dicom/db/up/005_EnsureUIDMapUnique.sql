--Version:1.4.0.1
--Description: Ensures that UID mappings are unique and aids lookup performance
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='ix_PrivateUIDsUniquePerProject')
BEGIN


CREATE UNIQUE NONCLUSTERED INDEX [ix_PrivateUIDsUniquePerProject] ON [dbo].[UIDMapping]
(
	[PrivateUID] ASC,
	[ProjectNumber] ASC,
	[UIDType] ASC
)

END
