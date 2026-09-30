## Current State
- PostgreSQL migration completed (MSSQL legacy removed)
- Ikas API v2 integrated (OAuth2 + GraphQL v2)
- Multi-variant products grouped per product name
- XML pull service active (Ikas exporter format supported)

## Key Decisions
- v2 API uses category name instead of ID (CategoryResolver removed)
- Variant grouping is name-based aggregation
- OAuth2 token cached (4h)

## Active Issues
- None

## Last Changes
- CategoryFilterService null filter bug fixed
- TransferService refactored to group-based flow