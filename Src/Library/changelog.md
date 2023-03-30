### NEW
- `ValidationContext<T>` class for manipulating the validation failures list of the current endpoint [#info](https://discord.com/channels/933662816458645504/1090551226598432828)

### IMPROVEMENTS
- add overload to `AddError()`, `ThrowError()`, `ThrowIfAnyErrors()` methods to accept a `ValidationFailure` [#info](https://discord.com/channels/933662816458645504/1090551226598432828/1090934715952926740)

### FIXES
- default response serializer func overriding the `content-type` of 400 responses [#info](https://discord.com/channels/933662816458645504/1090697556549447821)