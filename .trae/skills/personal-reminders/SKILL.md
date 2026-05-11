---
name: "personal-reminders"
description: "Personal reminders for common mistakes. Invoke at the start of EVERY task to prevent repeated errors."
---

# Personal Reminders

## Windows System Rules

**CRITICAL**: The user is working on **Windows** system!

- **NEVER use `&&`** to chain commands in PowerShell/CMD
- **USE `;`** to chain multiple commands in a single line
- Example correct usage: `command1 ; command2 ; command3`
- Example WRONG: `command1 && command2 && command3` (will fail!)

## Why This Matters

Using `&&` in Windows shells (PowerShell/CMD) will cause command failures. Always use `;` for sequential command execution on Windows.

## Reminder

Before executing any command or providing command examples, always verify:
1. Are you on Windows system? (Yes)
2. Should you use `;` instead of `&&`? (Yes)
3. Double-check all command chains use proper Windows syntax

**Always keep this rule in mind and apply it without needing to be reminded repeatedly.**

## Code Writing Rules

**CRITICAL**: When writing or modifying code, follow this strict workflow:

1. **Write complete, working code** - Never provide incomplete or placeholder code
2. **Execute build/compile** - Run the appropriate build command (e.g., `dotnet build`, `npm run build`, etc.)
3. **Verify no errors** - Check the build output for any compilation errors
4. **Fix all errors** - If errors exist, fix them and rebuild until successful
5. **Only then return code** - Only present code to user after it has been verified to build without errors

**Why this matters:**
- Prevents shipping broken code
- Saves time on debugging trivial issues
- Ensures code quality before review

**Reminder:** "One-pass" code delivery means:
- Code must be complete and compilable
- All syntax errors resolved
- All type errors resolved  
- No missing dependencies
- Build succeeds completely

**This rule applies to ALL code writing tasks.**

## Explicit References Rule

**CRITICAL**: Never use ambiguous or incomplete references that will require later correction.

### What to AVOID:
- ❌ "As mentioned earlier" without restating the reference
- ❌ "Use the appropriate X" without specifying which X
- ❌ "Configure as needed" without providing the actual configuration
- ❌ Placeholder references like "same as before", "the mentioned Y", "the above Z"
- ❌ Incomplete method signatures or partial implementations

### What to DO:
- ✅ Always provide complete, explicit references at the point of use
- ✅ Specify exact file paths, function names, variable names, etc.
- ✅ Provide full context: don't assume "user will understand"
- ✅ Include all necessary details in the current response, not "to be added later"
- ✅ Use complete implementations, never partial or "to be completed" code

### Why This Matters:
- **NO ITERATIVE FIXING**: Code must be correct on first attempt
- **NO "JUST ADD X"**: Everything must be provided upfront
- **COMPLETE CONTEXT**: Never rely on previous messages for critical information

**Remember:** You must always provide explicit, complete references. Never assume the user can fill in gaps or refer back to earlier messages.