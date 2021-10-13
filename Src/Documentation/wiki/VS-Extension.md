# visual studio new item template
if you're doing vertical slice architecture and placing each individual feature in their own namespace, you can take advantage of [this vs extension](https://github.com/dj-nitehawk/FastEndpoints/raw/main/VSExtension/FastEndpointsVSExtension.vsix) that will add a new item to the "add new file" dialog of visual studio to make it convenient for you to add feature file sets to your project.

once installed, your visual studio add new item dialog will have `FastEndpoints Feature File Set` listed under `Installed > Visual C#` node. then, instead of entering a file name, simply enter the namespace you want your new feature to be added to followed by `.cs`

a new feature file set will then be created in the folder you selected.

there will be 3 new files created under the namespace you chose.

**Data.cs** - use this class to place all of your data access logic.

**Models.cs** - place your request, response dtos and the validator in this file.

**Endpoint.cs** - this will be your new endpoint definition.

[see here](https://github.com/dj-nitehawk/FastEndpoints/tree/main/Web/%5BFeatures%5D/Admin/Login) for an example feature file set.