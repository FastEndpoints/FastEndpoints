# dotnet new template
similar to the [vs extension](VS-Extension.md), you can use our `dotnet new` template to create a new feature file set (slice).

## instllation
```shell
dotnet new -i FastEndpoints.TemplatePack
```

## basic usage
the following command will use the namepsace `MyProject.Comments.Create`, method `POST` and route `api/comments`. Files will be created in folder `Features/Comments/Create`:

```shell
dotnet new feat --name MyProject.Comments.Create -m post -r api/comments -o Features/Comments/Create
```

## all options
```shell
> dotnet new feat --help

FastEndpoints Feature Fileset (C#)
Author: @lazyboy1
Options:
  -t|--attributes  Whether to use attributes for endpoint configuration
                   bool - Optional
                   Default: false

  -p|--mapper      Whether to use a mapper
                   bool - Optional
                   Default: true

  -v|--validator   Whether to use a validator
                   bool - Optional
                   Default: true

  -m|--method      Endpoint HTTP method
                       GET
                       POST
                       PUT
                       DELETE
                       PATCH
                   Default: GET

  -r|--route       Endpoint path
                   string - Optional
                   Default: api/route/here
```
