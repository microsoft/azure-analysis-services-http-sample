
This sample is a HTTP proxy for XMLA endpoints, intended for use with Power BI Premium or Azure Analysis Services.

It's implemented as ASP.NET Core 5 API Project. The main API is `/api/Query` which allows you to POST a DAX query and receive the results as a JSON result.

The sample is coded to pass-through authentication from the client to the XMLA endpoint.  So to call the API either use HTTP BASIC auth over HTTPS, passing credentials with the request.  It's highly advised that this be a Service Principal, rather than an AAD user.  To specify a Service Principal use a UserName of the form `app:ClientID@TenantID`, and pass a Client Secret as the Password.

For better security, instead of passing a UserName/Password using HTTP BASIC auth, fetch a Bearer token for your XMLA endpoint.  To fetch a token use the Resource ID `https://analysis.windows.net/powerbi/api` for Power BI, or `https://*.asazure.windows.net` for Azure Analysis Services

# Configuration
To get started modify the configuration settings to set 

**Server**: this can be the XMLA endpoint of an Azure Analysis Services Server, 

`asazure://[region name].asazure.windows.net/[AAS Server Name]`

or a Power BI Premium Workspace

`powerbi://api.powerbi.com/v1.0/myorg/[Workspace Name]`

Or for local testing you can use an Analysis Services Server

`localhost`

If you specify an Analysis Services Server the API will attempt to connect using Windows Integrated Authentication, instead of using passthrough Auth.  This is handy for testing of the endpont itself, but is not intended for production deployment.

**Database**  this is the database name

**TenantId**  this is the tenantID to perform authentication against in Auth scenarios where the user hasn't passed a Bearer token or a Service Principal with the TenantID specified.

You can specify these setting through any [configuration provider](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration-providers).  If you are considering committing your code back to a GitHub public repository, you should specify these in a local secrets store, per https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-5.0&tabs=windows, or in a git-ignored settings file.

**Example appsettings.json**
```
{
    "Server": "powerbi://api.powerbi.com/v1.0/myorg/[Workspace Name]",
    "Database": "[Your AAS Database or Power BI Data Set Name]",
    "TenantId": "[Your Tenant ID]",
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information",
            "Microsoft.Samples.XMLA.HTTP.Proxy.Controllers.QueryController":  "Information"
        }
    },
    "AllowedHosts": "*"
}

```

Once you've got the configuration set, just run the API project in your Development environment and test with the included Swagger UI.  

GET `/api/Tables` endpoint to list the tables in the database.  The response will look like:

```
[
  {
    "TABLE_NAME": "DimDate"
  },
  {
    "TABLE_NAME": "DimProduct"
  },
  {
    "TABLE_NAME": "DimProductCategory"
  },
  {
    "TABLE_NAME": "DimProductSubcategory"
  },
  {
    "TABLE_NAME": "FactInternetSales"
  }
]
```

GET the `/api/Tables/{tableName}` endpoint to dump the contents of a table.


GET or POST the `/api/Query` endpoint to run an arbitrary DAX query.  A handy bit of DAX to know is the evaluate statement: https://docs.microsoft.com/en-us/dax/evaluate-statement-dax, eg:

`evaluate(DimProduct)`

to query an entire table. And instead of writing DAX by hand, you use the Perofrmance Analyzer in Power BI desktop to see the DAX queries sent by a Power BI report. https://docs.microsoft.com/en-us/power-bi/create-reports/desktop-performance-analyzer



# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
