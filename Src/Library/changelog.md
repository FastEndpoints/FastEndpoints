### CHANGES
- `AddAuthenticationJWTBearer()` has been renamed to `AddJWTBearerAuth()` and signature changed

### NEW
- customize dto property binding failure message #356
- ability to specify jwt bearer events with `AddJWTBearerAuth()` #359

### IMPROVEMENTS
- not fail modelbinding if queryparam is empty and dto property is nullable [#info](https://discord.com/channels/933662816458645504/1053657195771858944)