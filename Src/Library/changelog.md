### CHANGE
- the signature of the global error response builder has changed to include the `HttpContext` #220 #230

### NEW
- `[Throttle(...)]` attribute for configuring endpoint #227

### FIX
- pre/post processor collection modification bug #224
- response dto initialization not working with array types #225
- unable to instantiate validators for unit tests [#info](https://discord.com/channels/933662816458645504/1017889876521267263)