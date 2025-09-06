USE AnalisisVentas;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Almacenes_Codigo' AND object_id = OBJECT_ID('[dbo].[Almacenes]'))
CREATE UNIQUE NONCLUSTERED INDEX [IX_Almacenes_Codigo] ON [dbo].[Almacenes] ([Codigo] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Empleados_AlmacenId' AND object_id = OBJECT_ID('[dbo].[Empleados]'))
CREATE NONCLUSTERED INDEX [IX_Empleados_AlmacenId] ON [dbo].[Empleados] ([AlmacenId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_TiempoAlmacen' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_TiempoAlmacen] ON [dbo].[HechosVentas] ([Anio] ASC, [Mes] ASC, [AlmacenId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_Categoria' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_Categoria] ON [dbo].[HechosVentas] ([Categoria] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_MetodoPago' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_MetodoPago] ON [dbo].[HechosVentas] ([MetodoPago] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_VentaCab' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_VentaCab] ON [dbo].[HechosVentas] ([VentaCabId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_Producto' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_Producto] ON [dbo].[HechosVentas] ([ProductoId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HechosVentas_Cliente' AND object_id = OBJECT_ID('[dbo].[HechosVentas]'))
CREATE NONCLUSTERED INDEX [IX_HechosVentas_Cliente] ON [dbo].[HechosVentas] ([ClienteId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ParametrosAbastecimientos_ProductoId' AND object_id = OBJECT_ID('[dbo].[ParametrosAbastecimientos]'))
CREATE NONCLUSTERED INDEX [IX_ParametrosAbastecimientos_ProductoId] ON [dbo].[ParametrosAbastecimientos] ([ProductoId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Productos_Codigo' AND object_id = OBJECT_ID('[dbo].[Productos]'))
CREATE UNIQUE NONCLUSTERED INDEX [IX_Productos_Codigo] ON [dbo].[Productos] ([Codigo] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Usuarios_Email' AND object_id = OBJECT_ID('[dbo].[Usuarios]'))
CREATE UNIQUE NONCLUSTERED INDEX [IX_Usuarios_Email] ON [dbo].[Usuarios] ([Email] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasCab_AlmacenId' AND object_id = OBJECT_ID('[dbo].[VentasCab]'))
CREATE NONCLUSTERED INDEX [IX_VentasCab_AlmacenId] ON [dbo].[VentasCab] ([AlmacenId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasCab_ClienteId' AND object_id = OBJECT_ID('[dbo].[VentasCab]'))
CREATE NONCLUSTERED INDEX [IX_VentasCab_ClienteId] ON [dbo].[VentasCab] ([ClienteId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasCab_EmpleadoId' AND object_id = OBJECT_ID('[dbo].[VentasCab]'))
CREATE NONCLUSTERED INDEX [IX_VentasCab_EmpleadoId] ON [dbo].[VentasCab] ([EmpleadoId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasCab_Numero_AlmacenId' AND object_id = OBJECT_ID('[dbo].[VentasCab]'))
CREATE UNIQUE NONCLUSTERED INDEX [IX_VentasCab_Numero_AlmacenId] ON [dbo].[VentasCab] ([Numero] ASC, [AlmacenId] ASC)
WHERE ([Numero] IS NOT NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasDet_ProductoId' AND object_id = OBJECT_ID('[dbo].[VentasDet]'))
CREATE NONCLUSTERED INDEX [IX_VentasDet_ProductoId] ON [dbo].[VentasDet] ([ProductoId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasDet_VentaCabId' AND object_id = OBJECT_ID('[dbo].[VentasDet]'))
CREATE NONCLUSTERED INDEX [IX_VentasDet_VentaCabId] ON [dbo].[VentasDet] ([VentaCabId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasPago_MetodoPagoId' AND object_id = OBJECT_ID('[dbo].[VentasPago]'))
CREATE NONCLUSTERED INDEX [IX_VentasPago_MetodoPagoId] ON [dbo].[VentasPago] ([MetodoPagoId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VentasPago_VentaCabId' AND object_id = OBJECT_ID('[dbo].[VentasPago]'))
CREATE NONCLUSTERED INDEX [IX_VentasPago_VentaCabId] ON [dbo].[VentasPago] ([VentaCabId] ASC);
GO
