using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool
{
    public static class DBInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Ensure the database is created
            context.Database.EnsureCreated();

            // Check if the EnvironmentSetting table has any data
            if (context.EnvironmentSettings.Any())
            {
                return; // Database has been seeded
            }

            // Define environment settings to seed
            var settings = new EnvironmentSetting[]
            {
                new EnvironmentSetting{EnvironmentName="Development", CsmHost="saf.dev.essential-sandbox.com", ServicesApiUrl="https://dev-ecohub-services-api.azure-api.net"},
                new EnvironmentSetting{EnvironmentName="Staging", CsmHost="saf.essentials-staging.com", ServicesApiUrl="https://stg-ecohub-services-api.azure-api.net"},
                new EnvironmentSetting{EnvironmentName="IAT", CsmHost="saf.test-myecohub.ch", ServicesApiUrl="https://services.test-myecohub.ch"},
                new EnvironmentSetting{EnvironmentName="Production", CsmHost="saf.myecohub.ch", ServicesApiUrl="https://services.myecohub.ch"},
            };

            // Add the settings to the database
            context.EnvironmentSettings.AddRange(settings);
            context.SaveChanges();
        }
    }
}
