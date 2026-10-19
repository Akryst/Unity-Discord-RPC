Run the IPC smoke tests with the .NET 8 SDK on Windows or Linux:

```sh
dotnet run --project Tests~/IpcSmokeTests.csproj
```

The tests use local mock IPC servers; Discord and Unity are not required. They check socket locations, Unix socket connections, framing, writes, disconnects, reconnects, oversized frames, and Windows named pipes. A real Unity Editor and Discord session on Linux is still needed to verify desktop integration and sandbox permissions.
