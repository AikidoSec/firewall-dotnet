# Track custom events

Use `Zen.Track()` to report events that only your application knows about, such as failed logins. [Playbooks](https://help.aikido.dev/zen-firewall/zen-features/playbooks) can act when an event occurs repeatedly, for example by blocking an IP after three failed logins in five minutes.

```csharp
using Aikido.Zen.DotNetCore;

if (!Authenticate(request))
{
    Zen.Track("user.login_failed");
    return Unauthorized();
}
```

After adding `Zen.Track()`, trigger the event at least once. It will then appear on the Playbooks page in the Aikido dashboard. From there, you can create a playbook and choose what should happen when the event occurs. Calling `Zen.Track()` by itself does not create a playbook or block anything.

Call `Zen.Track()` while handling an HTTP request. Zen associates the event with the request's IP address. Playbook counts are per IP, not across your whole app. If you call [`Zen.SetUser()`](user.md) before `Zen.Track()`, Zen also includes the current user. `Zen.SetUser()` is optional. Events without a user are still tracked.

Event names can use any format. We recommend lowercase, dot-separated names such as `user.login_failed`.

<details>
<summary>ASP.NET Framework example</summary>

```csharp
using Aikido.Zen.DotNetFramework;

if (!Authenticate(request))
{
    Zen.Track("user.login_failed");
    return Unauthorized();
}
```

</details>
