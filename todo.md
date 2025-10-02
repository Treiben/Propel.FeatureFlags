# Bugs:

~~1.  **MAJOR, API:** filtering: filters only when 1 filter present; - try filtering targeting rules~~

~~2.  **MAJOR, API:** evaluation: throws invalid exception when user id or tenant id required (should be bad request, not 500).  Try evaluation white-label-branding without tenant id, or any other flags~~

~~3.  **MEDIUM, API:** evaluation message on success: (All [Propel.FeatureFlags.Domain.ModeSet] conditions met for feature flag activation) <- need to fix message~~

4. **MAJOR, API**: Filtering Expires in Days not working (verfication pending UI bug #17)

~~5. **MAJOR, API** filtering by tag not working~~

~~6. **MAJOR, API** incorrect number of flags per page due to left join with audit table~~

~~7.  **MAJOR, API:** Targeting rules evalution not working and default variation is always defaults to Off instead of actual variation~~

---

8. **MAJOR, UI:** on user access control CLEAR - sets user access to 0% which is full blockage; must be 100%; 100% always means no access is set (everyone is welcome)

9.  **MAJOR, UI:** same as above for tenant access CLEAR

10.  **MAJOR, UI:** information on each panel is a weird looking black column and hard to read. 

11.  **MAJOR, UI:** Application name and application version not shown on flag

12.  **MEDIUM, UI:** Flag card is way off to edges when there's a long list of evaluation modes. For example, ultimate-premium-experience flag that has Scheduling+TargetingRules+Percentage+TimeWindow list of modes that don't fit to the size of card

14. **MEDUM, UI**: unable to add a tag in CREATE FLAG dialog. Column ':' or space ' ' or comma ',' are not allowed by UI.

15. **MINOR, UI:** change text in create flag dialog to something like this: 'Note: You only can create global flags from this site. All application flags must be created from the application invoking them, directly from the code base.

The global flag you're creating is set as disabled and permanent by default. You can change these settings and add additional settings after you create the flag.' Check spelling, grammar, decrease verbosity.

16. **MAJOR, UI:** expiration date filter does not work or not implemented in React

17. **MAJOR, UI:**: expiration date is shown incorrectly: API returns 10/12/2025 00:00:00 but UI shows 10/11/1025 7:00:00 am

18. **MEDIUM, UI:**: variations and default variations are not shown for flags

19. **MEDIUM, UI:**: bad request message on evaluation when field is required (tenantid, userid) instead on showing error message
	
20. **MEDIUM, UI:** when filter by tag key applied, the UI sets Tags field of api request instead of TagKeys field

# DASHBOARD TO DO LIST

1. UI: Propel icon and proper page title

2. UI, API: Security and login screen

3. UI, API: Search by flag name or flag key

4. UI, API Add filtering by application name

5. UI, API: E2E test with Sql Server backend

# CLI TO DO LIST

1. Add CLI commands (maybe)

2. Add CLI documentation

3. Add CLI tests
