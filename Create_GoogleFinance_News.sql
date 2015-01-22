USE [FinanceCrawler]
GO

/****** Object:  Table [dbo].[GoogleFinance]    Script Date: 9/12/2014 9:24:08 p.m. ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[GoogleFinance_News](
	[Identity] [bigint] IDENTITY(1,1) NOT NULL,
	[Title] [nvarchar](max) NOT NULL,
	[Link] [nvarchar](max) NOT NULL,
	[Date] [datetime] NOT NULL,
	[Website] [nvarchar](max) NOT NULL,
	[Author] [nvarchar](max) NULL,
	[Story] [nvarchar](max) NULL,
	[NegWords] [bigint] NULL,
	[PosWords] [bigint] NULL,
	[Length_of_Post] [bigint] NULL,
	[Group] [nvarchar](50) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Identity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO


