using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using AmylandiaWeb;
using AmylandiaWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient base (para recursos locales del propio servidor)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Configuración MSAL para autenticación con Azure AD / Entra ID
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/User.Read");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Sites.ReadWrite.All");
    options.UserOptions.RoleClaim = "roles";
});

// HttpClient con bearer token automático para Microsoft Graph REST API
// Usa AuthorizationMessageHandler (no BaseAddressAuthorizationMessageHandler)
builder.Services.AddHttpClient("GraphAPI",
    client => client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"))
    .AddHttpMessageHandler(sp =>
        sp.GetRequiredService<AuthorizationMessageHandler>()
          .ConfigureHandler(
              authorizedUrls: new[] { "https://graph.microsoft.com" },
              scopes: new[] {
                  "https://graph.microsoft.com/User.Read",
                  "https://graph.microsoft.com/Sites.ReadWrite.All"
              }
          )
    );

// Servicio genérico de SharePoint (usa el HttpClient con autenticación)
builder.Services.AddScoped<GraphSharePointService>();

await builder.Build().RunAsync();
