# Security policy

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Use GitHub's private reporting:
[Report a vulnerability](https://github.com/ArizonaGreenTea05/kyuplan.plugins.example/security/advisories/new).
Describe what you found, how to reproduce it and what it could allow.

## What matters here

The plugin is an example, but people copy it, so what it shows has to be safe. In scope: an endpoint that returns or changes data without asking for a permission, a body that is trusted, a secret that is logged or stored unencrypted, and vulnerabilities in the package or its release workflow.

## What a plugin has to keep safe

A plugin runs **inside the kyuplan server with all its rights**: it can read the databases, the settings file and the network. Administrators decide to trust it; kyuplan only checks the shape of the package. So a plugin should:

- keep its secrets in `password` fields (stored encrypted, never sent back to a client), not log them and not put them in an error message;
- ask for a permission on **every** endpoint that returns or changes club data (`RequirePermission`); hiding a page protects nothing;
- validate every body (it comes from a browser) and limit what it can add, so a user cannot fill the server's memory or disk;
- call only the services it needs, and answer with errors that do not give secrets away.

This repository's tests check the permissions of every endpoint and the limits of what can be added. Keep such tests when you copy it.

## Supported versions

Only the latest release receives fixes.
