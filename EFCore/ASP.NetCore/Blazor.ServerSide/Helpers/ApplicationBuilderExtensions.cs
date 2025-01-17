﻿using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using DevExpress.ExpressApp.EFCore;
using DatabaseUpdater;

namespace Microsoft.Extensions.DependencyInjection {
    public static class ApplicationBuilderExtensions {
        public static IApplicationBuilder UseDemoData<TContext>(this IApplicationBuilder app, EFCoreDatabaseProviderHandler databaseProviderHandler) where TContext : DbContext {
            using(var objectSpaceProvider = new EFCoreObjectSpaceProvider(typeof(TContext), databaseProviderHandler))
            using(var objectSpace = objectSpaceProvider.CreateUpdatingObjectSpace(true)) {
                new Updater(objectSpace).UpdateDatabase();
            }
            return app;
        }
    }
}
