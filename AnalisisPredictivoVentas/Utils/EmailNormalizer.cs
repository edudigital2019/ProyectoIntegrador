namespace AnalisisPredictivoVentas.Utils
{
    public static class EmailNormalizer
    {
        public static string Normalize(string email)
            => (email ?? string.Empty).Trim().ToLowerInvariant();
    }
}
