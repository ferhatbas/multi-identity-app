#!/usr/bin/env bash
# Sign in and list the first page of users. Local demo data only.
set -euo pipefail

API_URL="${API_URL:-http://localhost:5240}"
API_USER="${API_USER:-admin}"
API_PASSWORD="${API_PASSWORD:-Demo@12345}"

# Every response is wrapped in the envelope: code, message, data, errors, traceId.
token=$(curl -sf -X POST "$API_URL/api/Auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"userName\":\"$API_USER\",\"password\":\"$API_PASSWORD\"}" \
  | jq -r '.data.accessToken')

curl -sf "$API_URL/api/User/paged?PageNumber=1&PageSize=5" \
  -H "Authorization: Bearer $token" \
  | jq '{page: .data.pageNumber, totalRecords: .data.totalRecords, users: [.data.data[] | {userName, email}]}'
