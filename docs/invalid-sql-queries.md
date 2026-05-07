# Blocking invalid SQL queries

Zen can block SQL queries that it can't tokenize when they contain user input. This prevents attackers from bypassing SQL injection detection with malformed queries. For example, ClickHouse ignores invalid SQL after `;`, and SQLite runs queries before an unclosed `/*` comment.

This is off by default. Enable it with:

```
AIKIDO_BLOCK_INVALID_SQL=true dotnet run
```
