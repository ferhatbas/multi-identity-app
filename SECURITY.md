# Security policy

This project handles sign-in, tokens and access control, so security reports matter.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private reporting instead: **Security → Report a vulnerability** on the repository page. Include what you found, how to reproduce it, and the version or commit.

You can expect an acknowledgement within a few days and a fix or a clear answer as soon as it is understood.

## Scope notes

- The demo data and its shared password exist for local demos only. Never deploy them.
- Secrets (`JWT_KEY`, `MSSQL_SA_PASSWORD`) are yours to generate and are never committed.
