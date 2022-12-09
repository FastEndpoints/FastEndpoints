### NEW
- jwt refresh token support [#info](https://fast-endpoints.com/docs/security#jwt-refresh-tokens)
- export swagger json file with dotnet run/ms build #348
- specify binding sources for the default request binder [#info](https://discord.com/channels/933662816458645504/1045775010226253876)

### IMPROVEMENTS
- tighten up even handling to support long-running processes
- upgrade fluentvalidations pkg to latest
- add safeguard against security policiy builder execution order change
- adjust default jwt verification clock skew to 60 seconds