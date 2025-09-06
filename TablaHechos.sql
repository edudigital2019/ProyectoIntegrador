USE [AnalisisVentas]
GO

/****** Object:  Table [dbo].[HechosVentas]    Script Date: 9/5/2025 9:12:28 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[HechosVentas](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[VentaCabId] [int] NOT NULL,
	[Numero] [nvarchar](80) NOT NULL,
	[Fecha] [date] NOT NULL,
	[Anio] [int] NOT NULL,
	[Mes] [int] NOT NULL,
	[Semana] [int] NULL,
	[AlmacenId] [int] NOT NULL,
	[ProductoId] [int] NOT NULL,
	[ClienteId] [int] NOT NULL,
	[EmpleadoId] [int] NULL,
	[Marca] [nvarchar](100) NULL,
	[Categoria] [nvarchar](100) NULL,
	[Genero] [nvarchar](50) NULL,
	[Color] [nvarchar](50) NULL,
	[Referencia] [nvarchar](100) NULL,
	[Talla] [nvarchar](20) NULL,
	[MetodoPago] [nvarchar](100) NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
	[Neto] [decimal](18, 2) NOT NULL,
	[Descuento] [decimal](18, 2) NOT NULL,
 CONSTRAINT [PK_HechosVentas] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


