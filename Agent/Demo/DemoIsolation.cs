using ProGlassAutomation.Services.SampleData;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Demo generation may seed ONLY an empty database (SampleDataService
    /// already refuses when real records exist). It never calls ClearAllData.
    /// </summary>
    public static class DemoIsolation
    {
        public static async Task<(bool Allowed, string Reason)> CanSeedAsync()
        {
            if (await SampleDataService.HasExistingDataAsync().ConfigureAwait(false))
            {
                return (false,
                    "The database already has records. Demo seeding will not run, " +
                    "so real customer and stock data stay untouched. " +
                    "I can still walk the live screens and capture a storyboard.");
            }

            return (true, "Database is empty — sample data may be seeded.");
        }
    }
}
