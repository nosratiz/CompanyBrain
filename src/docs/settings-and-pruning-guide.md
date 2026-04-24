# Settings and Pruning Insights Guide

This guide explains every tab on the **Settings & Governance** page and then walks through the **Pruning Insights** page in simple beginner language.

## Settings Page Overview

The settings page lives at `/settings` and contains these tabs:

- Tutorial
- General
- Security
- AI Tuning
- DeepRoot
- SharePoint
- Bot Integration
- Notion

Use one tab at a time. After updating fields in a tab, click that tab's save button.

## 1. General Tab

Purpose: control storage limits, excluded files, and generate a Claude Desktop MCP configuration snippet.

Main fields:

- `Maximum Storage (GB)`: sets the allowed size of the knowledge base.
- `Excluded Patterns`: semicolon-separated patterns for files or folders to skip.
- `Tenant ID`: read-only tenant identifier.
- `Generate Config`: creates a `claude_desktop_config.json` snippet.

Sample:

```text
Maximum Storage (GB): 100
Excluded Patterns: *.secret;.env;**/node_modules/**;**/bin/**;**/obj/**
Tenant ID: Not set
```

Good beginner setup:

- Set a reasonable storage limit like `50` or `100`.
- Exclude secret files and generated folders.

## 2. Security Tab

Purpose: control redaction and MCP access restrictions.

Main fields:

- `Security Mode`
  - `Strict`: strongest protection
  - `Moderate`: balanced default
  - `Relaxed`: development only
- `Enable PII Redaction`: masks sensitive strings.
- `Require API Key Authentication`: requires an MCP API key.
- `Enable IP Whitelist`: allows only listed IPs.

Sample:

```text
Security Mode: Strict
Enable PII Redaction: On
Require API Key Authentication: On
MCP API Key: cb_demo_key_for_example_only
Enable IP Whitelist: On
Allowed IP Addresses: 127.0.0.1;192.168.1.0/24
```

Beginner advice:

- Start with `Strict`.
- Turn on PII masking.
- Use API key authentication if external MCP clients connect.

## 3. AI Tuning Tab

Purpose: add a shared instruction prefix before AI resource content.

Main fields:

- `System Prompt Prefix`
- `Prompt Templates`
- `Preview`

Sample:

```text
GOVERNANCE POLICY:
- Summarize only what is relevant to the question
- Do not expose secrets or personal data
- Prefer internal company sources when available
- Say when confidence is low
```

Beginner advice:

- Keep the prefix short and policy-oriented.
- Use the built-in templates as a starting point.

## 4. DeepRoot Tab

Purpose: configure semantic vector search embeddings.

Main fields:

- `Provider`
- `Model`
- `Dimensions`
- `API Key`
- `Custom Endpoint`
- `Vector DB Path`
- `Status`

Sample:

```text
Provider: OpenAI
Model: text-embedding-3-small
Dimensions: 1536
API Key: sk-demo-example-not-real
Custom Endpoint: https://api.openai.com/v1
Vector DB Path: /Users/yourname/CompanyBrain/.deeproot/vectors.db
```

Beginner advice:

- Choose one provider.
- Paste the API key.
- Keep the default model unless you have a reason to change it.
- Leave dimensions as provider defaults if you are unsure.

## 5. SharePoint Tab

Purpose: sync SharePoint documents into the local knowledge base.

Main fields:

- `Enable SharePoint Sync`
- `Client ID`
- `Tenant ID`
- `Client Secret`
- `Sync Interval (minutes)`
- `Local Sync Folder`

Required Microsoft Graph permissions shown in the UI:

- `Sites.Read.All`
- `Files.Read.All`
- `offline_access`

Sample:

```text
Enable SharePoint Sync: On
Client ID: 11111111-2222-3333-4444-555555555555
Tenant ID: 99999999-8888-7777-6666-555555555555
Client Secret: example-secret-value
Sync Interval (minutes): 30
Local Sync Folder: C:\NexusData\SharePoint
```

Beginner advice:

- Register an Azure AD app first.
- Add the required Graph permissions.
- Save, then use `Test Connection` to validate format.

## 6. Bot Integration Tab

Purpose: let employees query DeepRoot from Slack or Microsoft Teams.

Main sections:

- `Slack Integration`
- `Microsoft Teams Integration`
- `Dev Tunnel (Inbound Webhooks)`
- `Active Conversation Threads`

Sample:

```text
Enable Slack Bot: On
Bot Token: xoxb-demo-token
Signing Secret: demo-slack-secret

Enable Teams Bot: Off
App ID: 22222222-3333-4444-5555-666666666666
App Password: demo-teams-secret

Enable Tunnel: On
Active Tunnel URL: https://blue-river-1234.devtunnels.ms
Slack Request URL: https://blue-river-1234.devtunnels.ms/api/chat/slack
```

Beginner advice:

- For local development, enable the tunnel.
- If the tunnel does not connect, run `devtunnel login` once.
- Paste the Slack request URL into Slack Event Subscriptions.

## 7. Notion Tab

Purpose: connect a Notion internal integration and sync pages or databases.

Main fields:

- `Notion API Token`
- `Workspace Filter`
- `Test Connection`
- `Sync Now`
- `Sync Status`

Sample:

```text
Notion API Token: secret_demo_token
Workspace Filter: 12ab34cd56ef7890abcd1234ef567890, 98fe76dc54ba3210abcd1234ef567890
Default Schedule: 0 */6 * * *
Collection: notion
```

Beginner advice:

- Create an internal integration at `https://www.notion.so/my-integrations`.
- Copy the token that starts with `secret_`.
- Share the pages with the integration in Notion.
- Save, test the connection, then run `Sync Now`.

## Pruning Insights Page

The pruning page lives at `/pruning-insights`.

Its job is to show how the system reduces context before sending anything to the cloud model. This helps save tokens, reduce cost, and protect sensitive data.

## What the page shows

The page has four main panels:

- `Pruning Engine`
- `Local Processing`
- `Savings & Efficiency`
- `Context Snippets`

There are also two page-level actions:

- `Simulate Event`
- `Reset Session`

## Pruning Insights for a Complete Beginner

Think of pruning like this:

1. The app finds many possible text chunks related to a question.
2. It scores them locally.
3. It removes or masks sensitive information.
4. It keeps only the best chunks that fit the token budget.
5. It sends the smaller, safer result to the cloud model.

That is what the page visualizes.

## Step-by-Step: How It Works

### Step 1. A user asks a question

Example:

```text
How do I deploy to Azure?
```

The pruning state starts processing and the page enters the first phase.

### Step 2. Local Ranking

The system checks local candidate chunks and scores how relevant they are to the question.

On the page:

- `Local Processing` shows the first progress state.
- `Context Snippets` will later show which chunks won.

Simple meaning:

- The system is sorting the available evidence.

### Step 3. PII Masking

Sensitive content is checked and redacted.

Examples of values that can be masked:

- email addresses
- API keys
- IP addresses
- tokens

Example:

```text
Contact admin@company.com
```

becomes:

```text
Contact [REDACTED_EMAIL]
```

Simple meaning:

- The page is confirming the outgoing context is safer.

### Step 4. Budget Selection

Now the app decides how much content can fit into the configured token budget.

The `Pruning Engine` panel controls this behavior:

- `Relevance Threshold`: how strict selection is
- `Max Chunks`: how many chunks can be included
- `Token Budget`: total token allowance

Simple meaning:

- Even if many chunks are good, only the best ones that fit the limit are kept.

### Step 5. Event Recording

When pruning finishes, the app records a `PruningEvent`.

That event stores:

- tool name
- query
- source attribution
- timestamp
- original tokens
- pruned tokens
- chunks evaluated
- chunks selected
- whether pruning happened
- whether PII was detected
- chosen snippets

### Step 6. Metrics Update

The `Savings & Efficiency` panel updates:

- `Tokens Saved`
- `Est. Savings`
- `Tool Calls`
- `Reduction`

The donut chart compares:

- `Kept Local`
- `Sent to Cloud`

Simple meaning:

- You immediately see how much data stayed local and how much was actually sent.

### Step 7. Snippet Review

The `Context Snippets` panel shows:

- which fragments were selected
- each fragment's score
- source file or source system
- redacted final text

Simple meaning:

- This is the evidence screen.
- It helps you understand why the model received the context it received.

## Example Event Explained

Example values from the page simulation:

```text
Query: How do I deploy to Azure?
Original Tokens: 4200
Pruned Tokens: 1100
Chunks Evaluated: 12
Chunks Selected: 3
PII Detected: Yes
Tokens Saved: 3100
```

Meaning in plain English:

- The app found 12 candidate chunks.
- It kept the best 3.
- It reduced the payload from 4,200 tokens to 1,100 tokens.
- It saved 3,100 tokens.
- It detected sensitive content and redacted it before sending.

## How to Read Each Panel

### Pruning Engine

Use this to control pruning behavior.

- `Disabled`: send more raw context behavior
- `Broad`: lower threshold, includes more content
- `Balanced`: middle ground
- `Precise`: stricter selection, smaller result set

Best beginner setting:

- Leave pruning enabled
- Start with `Balanced`
- Keep `Max Chunks` small, such as `3`
- Use a moderate token budget like `2000`

### Local Processing

This shows the current phase:

- `Local Ranking`
- `PII Masking`
- `Ready for Cloud`

When finished, the badge changes to:

- `Safe to Send`

Simple meaning:

- The progress bar shows where the system is in the safety-and-selection pipeline.

### Savings & Efficiency

This shows the business result:

- fewer tokens sent
- lower estimated cost
- measurable reduction percentage

Simple meaning:

- The higher the reduction, the more context was kept local.

### Context Snippets

This shows the actual chosen context.

You can inspect:

- the selected tool call
- how many chunks were selected
- whether PII was redacted
- fragment scores such as high, medium, or low

Simple meaning:

- This is the easiest place to verify the pruning engine is behaving correctly.

## Best Beginner Workflow

1. Open `/settings` and review the Tutorial tab.
2. Configure only the integrations you actively use.
3. Open `/pruning-insights`.
4. Click `Simulate Event`.
5. Watch the phase changes in `Local Processing`.
6. Review token savings in `Savings & Efficiency`.
7. Inspect the selected chunks in `Context Snippets`.
8. Adjust threshold, max chunks, or token budget if results are too broad or too narrow.

## Quick Troubleshooting

- No snippets appear:
  Run a search or click `Simulate Event`.
- Tunnel does not connect:
  Run `devtunnel login` and restart the app.
- Notion sync finds nothing:
  Make sure the pages are shared with the integration.
- SharePoint validation fails:
  Check that `Client ID` and `Tenant ID` are valid GUIDs.
- PII is not being masked:
  Confirm `Enable PII Redaction` is on in the Security tab.

