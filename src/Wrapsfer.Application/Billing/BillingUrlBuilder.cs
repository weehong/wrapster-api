namespace Wrapsfer.Application.Billing;

public static class BillingUrlBuilder
{
    public static string Combine(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        return baseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }
}
