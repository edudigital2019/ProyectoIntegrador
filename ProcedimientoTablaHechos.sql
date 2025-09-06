CREATE OR PROCEDURE [dbo].[usp_MaterializarHechosVentas]
    @CabIds dbo.IntList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID('tempdb..#Cabs') IS NOT NULL DROP TABLE #Cabs;
    SELECT c.Id, c.Numero, c.Fecha, c.AlmacenId, c.ClienteId, c.EmpleadoId
    INTO #Cabs
    FROM dbo.VentasCab c
    JOIN @CabIds i ON i.Id = c.Id;

    IF OBJECT_ID('tempdb..#TotalesPago') IS NOT NULL DROP TABLE #TotalesPago;
    SELECT p.VentaCabId, SUM(p.Monto) AS Total
    INTO #TotalesPago
    FROM dbo.VentasPago p
    JOIN #Cabs c ON c.Id = p.VentaCabId
    GROUP BY p.VentaCabId;

    IF OBJECT_ID('tempdb..#DistPago') IS NOT NULL DROP TABLE #DistPago;
    SELECT p.VentaCabId,
           ISNULL(mp.Nombre,'N/A') AS MetodoPago,
           CAST(CASE WHEN t.Total > 0 THEN p.Monto / t.Total ELSE 1 END AS DECIMAL(18,6)) AS Ratio
    INTO #DistPago
    FROM dbo.VentasPago p
    LEFT JOIN dbo.MetodosPago mp ON mp.Id = p.MetodoPagoId
    JOIN #TotalesPago t ON t.VentaCabId = p.VentaCabId;

    INSERT #DistPago (VentaCabId, MetodoPago, Ratio)
    SELECT c.Id, 'N/A', CAST(1 AS DECIMAL(18,6))
    FROM #Cabs c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.VentasPago p WHERE p.VentaCabId = c.Id);

    IF OBJECT_ID('tempdb..#BaseDet') IS NOT NULL DROP TABLE #BaseDet;
    SELECT d.VentaCabId,
           d.ProductoId,
           d.Cantidad,
           ISNULL(d.Descuento,0) AS Descuento,
           ISNULL(d.Subtotal,0)  AS NetoLinea,
           NULLIF(d.Marca,'')      AS Marca,
           COALESCE(NULLIF(d.Categoria,''), p.Categoria) AS Categoria,
           NULLIF(d.Genero,'')     AS Genero,
           NULLIF(d.Color,'')      AS Color,
           NULLIF(d.Referencia,'') AS Referencia,
           NULLIF(d.Talla,'')      AS Talla
    INTO #BaseDet
    FROM dbo.VentasDet d
    JOIN #Cabs c ON c.Id = d.VentaCabId
    LEFT JOIN dbo.Productos p ON p.Id = d.ProductoId;

    DELETE h
    FROM dbo.HechosVentas h
    JOIN #Cabs c ON c.Id = h.VentaCabId;

    INSERT dbo.HechosVentas
        (VentaCabId, Numero, Fecha, Anio, Mes, Semana,
         AlmacenId, ProductoId, ClienteId, EmpleadoId,
         Marca, Categoria, Genero, Color, Referencia, Talla,
         MetodoPago, Cantidad, Neto, Descuento)
    SELECT
         c.Id,
         c.Numero,
         CAST(c.Fecha AS date),
         YEAR(c.Fecha),
         MONTH(c.Fecha),
         DATEPART(ISO_WEEK, c.Fecha),
         c.AlmacenId,
         b.ProductoId,
         c.ClienteId,
         c.EmpleadoId,
         b.Marca,
         b.Categoria,
         b.Genero,
         b.Color,
         b.Referencia,
         b.Talla,
         dp.MetodoPago,
         CAST(b.Cantidad  * dp.Ratio AS DECIMAL(18,3)),
         CAST(b.NetoLinea * dp.Ratio AS DECIMAL(18,2)),
         CAST(b.Descuento * dp.Ratio AS DECIMAL(18,2))
    FROM #BaseDet b
    JOIN #Cabs c   ON c.Id = b.VentaCabId
    JOIN #DistPago dp ON dp.VentaCabId = c.Id;
END