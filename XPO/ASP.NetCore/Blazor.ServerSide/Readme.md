This example demonstrates how to access data protected by the [Security System](https://docs.devexpress.com/eXpressAppFramework/113366/concepts/security-system/security-system-overview) from a non-XAF Blazor application.
You will also see how to execute Create, Write, and Delete data operations and take security permissions into account.

## Prerequisites

- [Visual Studio 2019 v16.8+](https://visualstudio.microsoft.com/vs/) with the following workloads:
  - **ASP.NET and web development**
  - **.NET Core cross-platform development**
- [.NET SDK 5.0+](https://dotnet.microsoft.com/download/dotnet-core)
- Download and run the [Unified Component Installer](https://www.devexpress.com/Products/Try/) or add [NuGet feed URL](https://docs.devexpress.com/GeneralInformation/116042/installation/install-devexpress-controls-using-nuget-packages/obtain-your-nuget-feed-url) to Visual Studio NuGet feeds.
  
  *We recommend that you select all products when you run the DevExpress installer. It will register local NuGet package sources and item / project templates required for these tutorials. You can uninstall unnecessary components later.*


> **NOTE** 
>
> If you have a pre-release version of our components, for example, provided with the hotfix, you also have a pre-release version of NuGet packages. These packages will not be restored automatically and you need to update them manually as described in the [Updating Packages](https://docs.devexpress.com/GeneralInformation/118420/Installation/Install-DevExpress-Controls-Using-NuGet-Packages/Updating-Packages) article using the [Include prerelease](https://docs.microsoft.com/en-us/nuget/create-packages/prerelease-packages#installing-and-updating-pre-release-packages) option.

> If you wish to create a Blazor project with our Blazor Components from scratch, follow the [Create a New Blazor Application](https://docs.devexpress.com/Blazor/401057/getting-started/create-a-new-application) article.

---

## Step 1. Configure the Blazor Application

For detailed information about the ASP.NET Core application configuration, see [official Microsoft documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/get-started?view=aspnetcore-3.1&tabs=visual-studio).

Configure the Blazor application in the `ConfigureServices` and `Configure` methods of [Startup.cs](Startup.cs):

```csharp
public void ConfigureServices(IServiceCollection services) {
    services.AddRazorPages();
    services.AddServerSideBlazor();
    services.AddDevExpressBlazor();
    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
    services.AddHttpContextAccessor();
    services.AddSession();
    services.AddSingleton<XpoDataStoreProviderService>();
    services.AddSingleton(Configuration);
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
    if(env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
    } else {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    app.UseSession();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseDefaultFiles();
    app.UseRouting();
    app.UseEndpoints(endpoints => {
        endpoints.MapFallbackToPage("/_Host");
        endpoints.MapBlazorHub();
    });
    app.UseDemoData(Configuration.GetConnectionString("ConnectionString"));
}
```
- The [XpoDataStoreProviderService](Helpers/XpoDataStoreProviderService.cs) class provides access to the Data Store Provider object.
        
    ```csharp
    public class XpoDataStoreProviderService {
        private IXpoDataStoreProvider dataStoreProvider;
        private string connectionString;
        public XpoDataStoreProviderService(IConfiguration config) {
            connectionString = config.GetConnectionString("ConnectionString");
        }
        public IXpoDataStoreProvider GetDataStoreProvider() {
            if(dataStoreProvider == null) {
                dataStoreProvider = XPObjectSpaceProvider.GetDataStoreProvider(connectionString, null, true);
            }
            return dataStoreProvider;
        }
    }
    ```    

- The `IConfiguration` object is used to access the application configuration [appsettings.json](appsettings.json) file. We register it as a singleton to have access to connectionString from SecurityProvider.

    ```csharp        
    //...
    public IConfiguration Configuration { get; }
    public Startup(IConfiguration configuration) {
        Configuration = configuration;
    }
    ```
    In _appsettings.json_, add the connection string.
    ``` json
    "ConnectionStrings": {
        "ConnectionString": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=XPOTestDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"
    }
    ```
        
- Register HttpContextAccessor in the `ConfigureServices` method to access [HttpContext](https://docs.microsoft.com/en-us/dotnet/api/system.web.httpcontext?view=netframework-4.8) in controller constructors.

- Call the `UseDemoData` method at the end of the `Configure` method of _Startup.cs_:
    
    
    ```csharp
    public static IApplicationBuilder UseDemoData(this IApplicationBuilder app, string connectionString) {
        using(var objectSpaceProvider = new XPObjectSpaceProvider(connectionString)) {
            SecurityProvider.RegisterEntities(objectSpaceProvider);
            using(var objectSpace = objectSpaceProvider.CreateUpdatingObjectSpace(true)) {
                new Updater(objectSpace).UpdateDatabase();
            }
        }
        return app;
    }
    ```
    For more details about how to create demo data from code, see the [Updater.cs](/XPO/DatabaseUpdater/Updater.cs) class.

## Step 2. Initialize Data Store and XAF Security System. Authentication and Permission Configuration

Register security system and authentication in [Startup.cs](Startup.cs). We register it as a scoped to have access to SecurityStrategyComplex from SecurityProvider. The `AuthenticationMixed` class allows you to register several authentication providers, so you can use both [AuthenticationStandard authentication](https://docs.devexpress.com/eXpressAppFramework/119064/Concepts/Security-System/Authentication#standard-authentication) and ASP.NET Core Identity authentication.

```csharp
public void ConfigureServices(IServiceCollection services) {
    services.AddScoped((serviceProvider) => {
        AuthenticationMixed authentication = new AuthenticationMixed();
        authentication.LogonParametersType = typeof(AuthenticationStandardLogonParameters);
        authentication.AddAuthenticationStandardProvider(typeof(PermissionPolicyUser));
        authentication.AddIdentityAuthenticationProvider(typeof(PermissionPolicyUser));
        SecurityStrategyComplex security = new SecurityStrategyComplex(typeof(PermissionPolicyUser), typeof(PermissionPolicyRole), authentication);
        return security;
    });
}    
```

The [SecurityProvider](Helpers/SecurityProvider.cs) class contains helper functions that provide access to XAF Security System functionality.

```csharp
public class SecurityProvider : IDisposable {
    public SecurityStrategyComplex Security { get; private set; }
    public IObjectSpaceProvider ObjectSpaceProvider { get; private set; }
    XpoDataStoreProviderService xpoDataStoreProviderService;
    IHttpContextAccessor contextAccessor;
    public SecurityProvider(SecurityStrategyComplex security, XpoDataStoreProviderService xpoDataStoreProviderService, IHttpContextAccessor contextAccessor) {
        Security = security;
        this.xpoDataStoreProviderService = xpoDataStoreProviderService;
        this.contextAccessor = contextAccessor;
        if(contextAccessor.HttpContext.User.Identity.IsAuthenticated) {
            Initialize();
        }
    }
     public void Initialize() {
        ((AuthenticationMixed)Security.Authentication).SetupAuthenticationProvider(typeof(IdentityAuthenticationProvider).Name, contextAccessor.HttpContext.User.Identity);
        ObjectSpaceProvider = GetObjectSpaceProvider(Security);
        Login(Security, ObjectSpaceProvider);
    }
    //...
}
```

- Register `SecurityProvider`, in the `ConfigureServices` method in [Startup.cs](Startup.cs).

    ```csharp
    public void ConfigureServices(IServiceCollection services) {
        // ...
        services.AddScoped<SecurityProvider>();
    }
    ```


- The `GetObjectSpaceProvider` method provides access to the Object Space Provider. The [XpoDataStoreProviderService](Helpers/XpoDataStoreProviderService.cs) class provides access to the Data Store Provider object.

    ```csharp
    private IObjectSpaceProvider GetObjectSpaceProvider(SecurityStrategyComplex security) {
        SecuredObjectSpaceProvider objectSpaceProvider = new SecuredObjectSpaceProvider(security, xpoDataStoreProviderService.GetDataStoreProvider(), true);
        RegisterEntities(objectSpaceProvider);
        return objectSpaceProvider;
    }
    //...
    public class XpoDataStoreProviderService {
        private IXpoDataStoreProvider dataStoreProvider;
        private string connectionString;
        public XpoDataStoreProviderService(IConfiguration config) {
            connectionString = config.GetConnectionString("ConnectionString");
        }
        public IXpoDataStoreProvider GetDataStoreProvider() {
            if(dataStoreProvider == null) {
                dataStoreProvider = XPObjectSpaceProvider.GetDataStoreProvider(connectionString, null, true);
            }
            return dataStoreProvider;
        }
    }
    // Registers all business object types you use in the application.
    private void RegisterEntities(SecuredObjectSpaceProvider objectSpaceProvider) {
        objectSpaceProvider.TypesInfo.RegisterEntity(typeof(Employee));
        objectSpaceProvider.TypesInfo.RegisterEntity(typeof(PermissionPolicyUser));
        objectSpaceProvider.TypesInfo.RegisterEntity(typeof(PermissionPolicyRole));
    }
    ```
    
- The `InitConnection` method authenticates a user both in the Security System and in [ASP.NET Core HttpContext](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext?view=aspnetcore-2.2). 
A user is identified by the user name and password parameters.

    ```csharp
    public bool InitConnection(string userName, string password) {
        AuthenticationStandardLogonParameters parameters = new AuthenticationStandardLogonParameters(userName, password);
        Security.Logoff();
        ((AuthenticationMixed)Security.Authentication).SetupAuthenticationProvider(typeof(AuthenticationStandardProvider).Name, parameters);
        IObjectSpaceProvider objectSpaceProvider = GetObjectSpaceProvider(Security);
        try {
            Login(Security, objectSpaceProvider);
            SignIn(contextAccessor.HttpContext, userName);
            return true;
        } catch {
            return false;
        }
    }
    //...
    // Logs into the Security System.
    private void Login(SecurityStrategyComplex security, IObjectSpaceProvider objectSpaceProvider) {
        IObjectSpace objectSpace = ((INonsecuredObjectSpaceProvider)objectSpaceProvider).CreateNonsecuredObjectSpace();
        security.Logon(objectSpace);
    }
    // Signs into HttpContext and creates a cookie.
    private void SignIn(HttpContext httpContext, string userName) {
        List<Claim> claims = new List<Claim>{
                new Claim(ClaimsIdentity.DefaultNameClaimType, userName)
            };
        ClaimsIdentity id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
        ClaimsPrincipal principal = new ClaimsPrincipal(id);
        httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
    ```

## Step 3. Pages

[Login.cshtml](Pages/Login.cshtml) is a login page that allows you to log into the application.

[Login.cshtml.cs](Pages/Login.cshtml.cs) class uses SecurityProveder and implements the Login logic.

```csharp
public IActionResult OnPost() {
    Response.Cookies.Append("userName", Input.UserName ?? string.Empty);
    if(ModelState.IsValid) {
        if(securityProvider.InitConnection(Input.UserName, Input.Password)) {
            return Redirect("/");
        }
        ModelState.AddModelError("Error", "User name or password is incorrect");
    }
    return Page();
}
```

[Logout.cshtml.cs](Pages/Logout.cshtml.cs) class implements the Logout logic

```csharp
public void OnGet() {
    Input = new InputModel();
    string userName = Request.Cookies["userName"]?.ToString();
    Input.UserName = userName ?? "User";
}
public class InputModel {
    [Required(ErrorMessage = "User name is required")]
    public string UserName { get; set; }
    public string Password { get; set; }
}
```

[Index.razor](Pages/Index.razor) is the main page. It configures the [Blazor Data Grid](https://docs.devexpress.com/Blazor/DevExpress.Blazor.DxDataGrid-1) and allows a use to log out.

The `OnInitialized` method creates an ObjectSpace instance and gets Employee and Department objects.

```csharp
protected override void OnInitialized() {
    objectSpace = securityProvider.ObjectSpaceProvider.CreateObjectSpace();
    employees = objectSpace.GetObjectsQuery<Employee>();
    departments = objectSpace.GetObjectsQuery<Department>();
}
```

The `HandleValidSubmit` method saves changes if data is valid.

```csharp
async Task HandleValidSubmit() {
    ObjectSpace.CommitChanges();
    await grid.Refresh();
    employee = null;
    await grid.CancelRowEdit();
}
```

The `OnRowRemoving` method removes an object.

```csharp
Task OnRowRemoving(object item) {
    ObjectSpace.Delete(item);
    ObjectSpace.CommitChanges();
    return grid.Refresh();
}
```

To show/hide the `New`, `Edit`, `Delete` actions, use the appropriate `CanXXX` methods of the Security System.

```razor
<DxDataGridCommandColumn Width="100px">
    <HeaderCellTemplate>
        @if(securityProvider.Security.CanCreate<Employee>()) {
            <button class="btn btn-link" @onclick="@(() => StartRowEdit(null))">New</button>
        }
    </HeaderCellTemplate>
    <CellTemplate>
        @if(securityProvider.Security.CanWrite(context)) {
            <a @onclick="@(() => StartRowEdit(context))" href="javascript:;">Edit </a>
        }
        @if(securityProvider.Security.CanDelete(context)) {
            <a @onclick="@(() => OnRowRemoving(context))" href="javascript:;">Delete</a>
        }
    </CellTemplate>
</DxDataGridCommandColumn>
```

The page is decorated with the Authorize attribute to prohibit unauthorized access.

```razor
@attribute [Authorize]
```

To show the `*******` text instead of a default value in data grid cells and editors, use [SecuredContainer](Components/SecuredContainer.razor)

```razor
<DxDataGridColumn Field="@nameof(Employee.FirstName)">
    <DisplayTemplate>
        <SecuredContainer Context="readOnly" CurrentObject="@context" PropertyName="@nameof(Employee.FirstName)">
            @(((Employee)context).FirstName)
        </SecuredContainer>
    </DisplayTemplate>
</DxDataGridColumn>
//...
<DxFormLayoutItem Caption="First Name">
    <Template>
        <SecuredContainer Context="readOnly" CurrentObject=@employee PropertyName=@nameof(Employee.FirstName) IsEditor=true>
            <DxTextBox @bind-Text=employee.FirstName ReadOnly=@readOnly />
        </SecuredContainer>
    </Template>
</DxFormLayoutItem>
```

To show the `*******` text instead of the default text, check the Read permission by using the `CanRead` method of the Security System.
Use the `CanWrite` method of the Security System to check if a user is allowed to edit a property and an editor should be created for this property.

```razor
private bool HasAccess => ObjectSpace.IsNewObject(CurrentObject) ?
    SecurityProvider.Security.CanWrite(CurrentObject.GetType(), PropertyName) :
    SecurityProvider.Security.CanRead(CurrentObject, PropertyName);
```

## Step 4: Run and Test the App

- Log in a 'User' with an empty password.
  ![](/images/Blazor_LoginPage.png)

- Note that secured data is displayed as '*******'.
  ![](/images/Blazor_ListView.png)

- Press the **Logout** button and log in as 'Admin' to see all the records.
