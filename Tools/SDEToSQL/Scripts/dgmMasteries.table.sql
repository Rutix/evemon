﻿IF OBJECT_ID('dbo.dgmMasteries', 'U') IS NOT NULL
DROP TABLE [dbo].[dgmMasteries]

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

SET ANSI_PADDING ON

CREATE TABLE [dbo].[dgmMasteries](
	[masteryID] [int] NOT NULL,
	[certificateID] [int] NOT NULL,
	[grade] [tinyint] NOT NULL,
 CONSTRAINT [dgmMasteries_PK] PRIMARY KEY CLUSTERED 
(
	[masteryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[dgmMasteries] ADD CONSTRAINT [certificateID_grade_UC] UNIQUE (certificateID, grade)