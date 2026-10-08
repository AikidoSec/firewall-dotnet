# Blocking invalid SQL queries

Zen blocks SQL queries that it can't tokenize when they contain user input. This prevents attackers from bypassing SQL injection detection with malformed queries. For example, ClickHouse ignores invalid SQL after `;`, and SQLite runs queries before an unclosed `/*` comment.

This is enabled by default. To disable it (not recommended for security reasons):

```
AIKIDO_BLOCK_INVALID_SQL=false dotnet run
```
