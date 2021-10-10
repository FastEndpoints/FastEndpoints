# Install

install the nuget package with command: 
```
Install-Package MongoDB.Entities
```

# Initialize

first import the package with `using MongoDB.Entities;`

then initialize the database connection like so:

## Basic initialization
```csharp
await DB.InitAsync("DatabaseName", "HostAddress", PortNumber);
```

## Advanced initialization
```csharp
await DB.InitAsync("DatabaseName", new MongoClientSettings()
{
    Server = new MongoServerAddress("localhost", 27017),
    Credential = MongoCredential.CreateCredential("DatabaseName", "username", "password")
});
```
> this will only work for mongodb v4.0 or newer databases as it will use the `SCRAM-SHA-256` authentication method. if your db version is older than that and uses `SCRAM-SHA-1` authentication method, please [click here](https://gist.github.com/dj-nitehawk/a0b1484dbba90085305520c156502608) to see how to connect or you may use a connection string to connect as shown below.

## Using a connection string
```csharp
await DB.InitAsync("DatabaseName",
    MongoClientSettings.FromConnectionString(
        "mongodb://{username}:{password}@{hostname}:{port}/?authSource=admin"));
```