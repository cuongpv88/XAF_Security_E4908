using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OData.ModelBuilder;
using BusinessObjectsLibrary.BusinessObjects;
using DevExpress.Persistent.BaseImpl.EFCore;
using Microsoft.OData.Edm;
using Microsoft.AspNetCore.OData;
using DevExpress.Persistent.BaseImpl.EF;
using Microsoft.AspNetCore.Authentication.Cookies;
using DevExpress.EntityFrameworkCore.Security;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.EntityFrameworkCore;
using DevExpress.ExpressApp;

namespace DevExtreme.OData.EFCore {
    public class Startup {

        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services.AddControllers(mvcOptions => {
                mvcOptions.EnableEndpointRouting = false;
            })
                .AddOData(opt => {
                    opt.Count().Filter().Expand().Select().OrderBy().SetMaxTop(null).AddRouteComponents(GetEdmModel());
                });
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            services.AddDbContextFactory<ApplicationDbContext>((serviceProvider, options) => {
                string connectionString = Configuration.GetConnectionString("ConnectionString");
                options.UseSqlServer(connectionString);
                options.UseLazyLoadingProxies();
                options.UseSecurity(serviceProvider.GetRequiredService<SecurityStrategyComplex>(), XafTypesInfo.Instance);
            }, ServiceLifetime.Scoped);
            services.AddHttpContextAccessor();
            services.AddScoped<SecurityProvider>();
            services.AddScoped((serviceProvider) => {
                AuthenticationMixed authentication = new AuthenticationMixed();
                authentication.LogonParametersType = typeof(AuthenticationStandardLogonParameters);
                authentication.AddAuthenticationStandardProvider(typeof(PermissionPolicyUser));
                authentication.AddIdentityAuthenticationProvider(typeof(PermissionPolicyUser));
                SecurityStrategyComplex security = new SecurityStrategyComplex(typeof(PermissionPolicyUser), typeof(PermissionPolicyRole), authentication);
                return security;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if(env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            } else {
                app.UseHsts();
            }
            app.UseODataQueryRequest();
            app.UseODataBatching();

            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();
            app.UseMiddleware<UnauthorizedRedirectMiddleware>();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseCookiePolicy();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
            app.UseDemoData<ApplicationDbContext>((builder, _) =>
                builder.UseSqlServer(Configuration.GetConnectionString("ConnectionString")));
        }
        private IEdmModel GetEdmModel() {
            ODataModelBuilder builder = new ODataConventionModelBuilder();
            EntitySetConfiguration<Employee> employees = builder.EntitySet<Employee>("Employees");
            EntitySetConfiguration<Department> departments = builder.EntitySet<Department>("Departments");
            EntitySetConfiguration<Party> parties = builder.EntitySet<Party>("Parties");
            EntitySetConfiguration<ObjectPermission> objectPermissions = builder.EntitySet<ObjectPermission>("ObjectPermissions");
            EntitySetConfiguration<MemberPermission> memberPermissions = builder.EntitySet<MemberPermission>("MemberPermissions");
            EntitySetConfiguration<TypePermission> typePermissions = builder.EntitySet<TypePermission>("TypePermissions");

            employees.EntityType.HasKey(t => t.ID);
            departments.EntityType.HasKey(t => t.ID);
            parties.EntityType.HasKey(t => t.ID);
            ActionConfiguration login = builder.Action("Login");
            login.Parameter<string>("userName");
            login.Parameter<string>("password");
       

            builder.Action("Logout");

            ActionConfiguration getPermissions = builder.Action("GetPermissions");
            getPermissions.Parameter<string>("typeName");
            getPermissions.CollectionParameter<string>("keys");

            ActionConfiguration getTypePermissions = builder.Action("GetTypePermissions");
            getTypePermissions.Parameter<string>("typeName");
            getTypePermissions.ReturnsFromEntitySet<TypePermission>("TypePermissions");
            return builder.GetEdmModel();
        }
    }
}