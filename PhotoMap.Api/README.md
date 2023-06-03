# Database migrations
To create a migration (if needed), go to PhotoMap.Api root folder and run the following command:
* dotnet ef migrations add <--MigrationName--> -s PhotoMap.Api -p PhotoMap.Api.Database

When starting PhotoMap.Api service, the already created migrations will be automatically applied (no need to manually apply them). 