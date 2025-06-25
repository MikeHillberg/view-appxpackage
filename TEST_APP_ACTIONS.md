# App Actions Testing Guide

This document provides guidance for testing the newly implemented App Actions functionality.

## Prerequisites
- Windows 10/11 machine with Windows App SDK runtime
- Built and deployed view-appxpackage application
- Some MSIX/AppX packages installed on the system

## App Actions to Test

### 1. List Package Family Names

**Command Line Testing:**
```cmd
# Using -action parameter
view-appxpackage.exe -action list

# Using alias command (after deployment)
view-appx-list.exe

# Help
view-appxpackage.exe -help
```

**Protocol Testing:**
```cmd
# From Run dialog or command line
start view-appxpackage-list://
```

**Expected Output:** List of package family names, one per line

### 2. Get Package Properties

**Command Line Testing:**
```cmd
# Using -action parameter
view-appxpackage.exe -action properties -param:packageFamilyName=Microsoft.WindowsCalculator_8wekyb3d8bbwe

# Using alias command (after deployment)
view-appx-properties.exe Microsoft.WindowsCalculator_8wekyb3d8bbwe
```

**Protocol Testing:**
```cmd
# From Run dialog or command line
start "view-appxpackage-properties://?packageFamilyName=Microsoft.WindowsCalculator_8wekyb3d8bbwe"
```

**Expected Output:** Package properties in format (Property=Value), one per line

### 3. Find Packages by Property

**Command Line Testing:**
```cmd
# Using -action parameter
view-appxpackage.exe -action find -param:propertyName=Name -param:propertyValue=Calculator

# Using alias command (after deployment)
view-appx-find.exe Name Calculator
```

**Protocol Testing:**
```cmd
# From Run dialog or command line
start "view-appxpackage-find://?propertyName=Name&propertyValue=Calculator"
```

**Expected Output:** List of package family names that match the criteria

## Windows Search Integration Testing

After deployment and registration:

1. Open Windows Search (Win + S)
2. Type "view-appx" - should show the alias commands as suggestions
3. Try searching for "List Package" or "Get Package Properties" - should find the app actions
4. Click on suggested commands to execute them

## Context Menu Testing

After deployment:
1. Right-click on an MSIX/AppX file
2. Look for "Open with" options that include the app actions
3. Test protocol activation from file associations

## Error Handling Testing

Test error conditions:
```cmd
# Missing parameters
view-appxpackage.exe -action properties
view-appxpackage.exe -action find -param:propertyName=Name

# Invalid action
view-appxpackage.exe -action invalid

# Invalid package family name
view-appxpackage.exe -action properties -param:packageFamilyName=NonExistentPackage
```

## Validation Checklist

- [ ] Command line App Actions work with -action parameter
- [ ] Command aliases work (view-appx-*.exe)
- [ ] Protocol activation works
- [ ] Help text displays correctly
- [ ] Error handling works for missing parameters
- [ ] Error handling works for invalid actions
- [ ] Output matches MCP tool output for same operations
- [ ] Windows Search finds App Actions
- [ ] Context menu integration works (if applicable)
- [ ] App exits properly after executing App Actions

## Notes

- App Actions should produce the same output as corresponding MCP tools
- All App Actions exit the application after completion (no UI shown)
- Protocol activation requires proper URL encoding for special characters
- Command aliases require app deployment and registration with Windows