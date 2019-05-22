CREATE TABLE [dbo].[MR](
	[SOPInstanceUID] [varchar](256) NOT NULL,
	[SeriesInstanceUID] [varchar](256) NOT NULL,
	[StudyInstanceUID] [varchar](256) NOT NULL,
	[SeriesDescription] [varchar](max) NULL,
	[StudyDescription] [varchar](max) NULL,
	[SeriesDate] [date] NULL,
	[SeriesTime] [time](0) NULL,
	[StudyDate] [date] NULL,
	[StudyTime] [time](0) NULL,
	[ScanningSequence] [varchar](10) NULL,
	[SequenceVariant] [varchar](10) NULL,
	[SequenceName] [varchar](128) NULL,
	[SliceThickness] [decimal](8, 5) NULL,
	[ProtocolName] [varchar](128) NULL,
 CONSTRAINT [PK_MR] PRIMARY KEY CLUSTERED 
(
	[SOPInstanceUID] ASC
)
) ON [PRIMARY]

CREATE TABLE [dbo].[CT](
	[SOPInstanceUID] [varchar](256) NOT NULL,
	[SeriesInstanceUID] [varchar](256) NOT NULL,
	[StudyInstanceUID] [varchar](256) NOT NULL,
	[SeriesDescription] [varchar](max) NULL,
	[StudyDescription] [varchar](max) NULL,
	[SeriesDate] [date] NULL,
	[SeriesTime] [time](0) NULL,
	[StudyDate] [date] NULL,
	[StudyTime] [time](0) NULL,
	[Exposure] [varchar](10) NULL,
	[ContrastBolusAgent] [varchar](100) NULL,
 CONSTRAINT [PK_CT] PRIMARY KEY CLUSTERED 
(
	[SOPInstanceUID] ASC
)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]