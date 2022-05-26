# visual studio new item template
if you're doing vertical slice architecture and placing each individual feature in their own namespace, you can take advantage of [this vs extension](https://marketplace.visualstudio.com/items?itemName=dj-nitehawk.FastEndpointsVSExtension) that will add a new item to the "add new file" dialog of visual studio to make it convenient for you to add feature file sets to your project.

once installed, your visual studio add new item dialog will have `FastEndpoints Feature File Set` listed under `Installed > Visual C#` node. then, instead of entering a file name, simply enter the namespace you want your new feature to be added to followed by `.cs`

a new feature file set will then be created in the folder you selected.

there will be 4 new files created under the namespace you chose.

**Data.cs** - use this class to place all of your data access logic.

**Models.cs** - place your request, response dtos and the validator in this file.

**Mapper.cs** - domain entity mapping logic will live here.

**Endpoint.cs** - this will be your new endpoint definition.

[click here](https://github.com/dj-nitehawk/MiniDevTo/tree/main/Features/Author/Articles/SaveArticle) for an example feature file set.

---

<a target="_blank" href="../images/vslice.gif">
  <img src="../images/vslice.gif">
</a>

# dotnet new template
if you prefer working with the cli, you can use our `dotnet new` template to create a new feature file set.

## installation
```shell
dotnet new -i FastEndpoints.TemplatePack
```

## basic usage
the following command will use the namepsace `MyProject.Comments.Create`

method `POST` 

route `api/comments`

files will be created in folder `Features/Comments/Create`:

```shell
dotnet new feat --name MyProject.Comments.Create \
-m post \
-r api/comments \
-o Features/Comments/Create
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
