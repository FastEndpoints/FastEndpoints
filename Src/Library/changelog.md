### NEW
- add `SendInterceptedAsync()` method to endpoint class for compatibility with FluentResults #365

### IMPROVEMENTS
- automatically send 415 response if endpoint specifies content-types but request doesn't have any content-type headers [#info](https://discord.com/channels/933662816458645504/1064900909181718590)