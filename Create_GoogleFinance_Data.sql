USE [FinanceCrawler]
GO

/****** Object:  Table [dbo].[GoogleFinance_Data]    Script Date: 11/12/2014 9:46:58 p.m. ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[GoogleFinance_Data](
	[Identity] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](max) NOT NULL,
	[Price] [numeric](18, 2) NOT NULL,
	[Date] [datetime] NOT NULL,
	[Range_From] [numeric](18, 2) NULL,
	[Range_To] [numeric](18, 2) NULL,
	[52 Weeks_From] [numeric](18, 2) NULL,
	[52 Weeks_To] [numeric](18, 2) NULL,
	[Open] [numeric](18, 2) NULL,
	[Vol(M)] [numeric](18, 4) NULL,
	[Avg(M)] [numeric](18, 4) NULL,
	[Mkt Cap(B)] [numeric](18, 4) NULL,
	[P/E] [numeric](18, 2) NULL,
	[Div] [numeric](18, 2) NULL,
	[Yield] [numeric](18, 2) NULL,
	[EPS] [numeric](18, 2) NULL,
	[Shares(B)] [numeric](18, 4) NULL,
	[Beta] [nvarchar](max) NULL,
	[S&P/ASX 200] [bigint] NULL,
	[Group] [nvarchar](max) NULL
PRIMARY KEY CLUSTERED 
(
	[Identity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO


