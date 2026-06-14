# Copilot Instructions

## General Guidelines
- Always use curly braces on if/else blocks, even when they contain a single line (no braceless single-line if statements).

## Azure Rules
- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool, ask the user to enable it.
