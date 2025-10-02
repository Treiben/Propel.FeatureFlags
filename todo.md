
# DASHBOARD API
## BUGS

~~1.  **MAJOR, API:** filtering: filters only when 1 filter present; - try filtering targeting rules~~

~~2.  **MAJOR, API:** evaluation: throws invalid exception when user id or tenant id required (should be bad request, not 500).  Try evaluation white-label-branding without tenant id, or any other flags~~

~~3.  **MEDIUM, API:** evaluation message on success: (All [Propel.FeatureFlags.Domain.ModeSet] conditions met for feature flag activation) <- need to fix message~~

4. **MAJOR, API**: Filtering Expires in Days not working - it's an API BUG

~~5. **MAJOR, API** filtering by tag not working~~

~~6. **MAJOR, API** incorrect number of flags per page due to left join with audit table~~

~~7.  **MAJOR, API:** Targeting rules evalution not working and default variation is always defaults to Off instead of actual variation~~


# DASHBOARD UI 
## BUGS

8. **MAJOR, UI:** on user access control CLEAR - error: sets user access to 0% which is full blockage; expected: must be 100%; 100% always means no access is set (everyone is welcome)

9.  **MAJOR, UI:** same as above for tenant access CLEAR

10.  **MAJOR, UI:** information on each panel is a weird looking black column and hard to read. 

~~11.  **MAJOR, UI:** Application name and application version not shown on flag~~

12.  **MEDIUM, UI:** Flag card is way off to edges when there's a long list of evaluation modes. For example, ultimate-premium-experience flag that has Scheduling+TargetingRules+Percentage+TimeWindow list of modes that don't fit to the size of card

14. **MEDUM, UI**: unable to add a tag in CREATE FLAG dialog. Column ':' or space ' ' or comma ',' are not allowed by UI.

~~15. **MINOR, UI:** change text in create flag dialog to something like this: 'Note: You only can create global flags from this site. All application flags must be created from the application invoking them, directly from the code base.~~

~~The global flag you're creating is set as disabled and permanent by default. You can change these settings and add additional settings after you create the flag.' Check spelling, grammar, decrease verbosity.~~

~~16. **MAJOR, UI:** expiration date filter does not work or not implemented in React~~

~~17. **MAJOR, UI:**: expiration date is shown incorrectly: API returns 10/12/2025 00:00:00 but UI shows 10/11/1025 7:00:00 am~~

18. **MEDIUM, UI:**: variations and default variations are not shown for flags

19. **MEDIUM, UI:**: bad request message on evaluation when field is required (tenantid, userid) instead on showing error message
	
20. **MEDIUM, UI:** when filter by tag key applied, the UI sets Tags field of api request instead of TagKeys field

21. **MEDIUM, UI:**: when user (tenant) percentage rollout is set to 100%, the UI should show percentage as "No user restrictions" ("No tenant restrictions") (because 100% means no restriction, everyone is allowed)

22.**MEDIUM, UI:**: when Clear user (tenant) access control, the rollout must be set to 100% and shown as "No user restrictions"

23. **NEW BUG, MEDIUM, UI:**: when scope is Global, no application name or version must be shown because it's pointless.

## BUG FIX VERIFICATION REPORT

- BUG #8: DiD NOT FIX IT: CLICKING ON CLEAR BUTTON RESULTS IN FULLY BLOCKED USER ACCESS (0%). 0% IS A SETTING SHOULD BE DONE BY USER. IT IS NOT A DEFAULT BEHAVIOR. DEFAULT BEHAVIOR IS 100% ON CLICKING CLEAR BUTTON.
- BUG #9: DID NOT FIX IT: SAME AS BUG 8
- BUG 10: DID NOT FIX IT: INSTEAD OF LONG VERTICAL BLACK BOX IT NOW SHOWS THE SAME LONG VERTICAL WHITE BOX. IN SOME CASES THERE ARE NO MORE THAN 1 WORD PER HORIZONTAL LINE. IT'S UNREADABLE!
- BUG 11: CLOSED BUT INTRODUCED BUG #23
- BUG 12: UNABLE TO VERIFY AS THE PROVIDED INSTRUCTIONS ON FILE MODIFICATION INCREDIBLY POOR.
- BUG 14: DID NOT FIX IT: I STILL ONLY ABLE TO ENTER TAG KEY BUT NOT ABLE TO ENTER TAG VALUE
- BUG 15: CLOSED
- BUG 16: CLOSED
- BUG 17: CLOSED. 
- BUG 18: DID NOT FIX IT. VARIATIONS MUST BE AT THE END OF THE PAGE, BELOW 'CUSTOM TARGETING RULES' AND SHOWN ONLY WHEN THEY ARE NOT ON/OFF. RIGHT NOW THEY ARE AT THE EXPIRATION WARNING SECTION WHICH DOES NOT MAKE ANY SENSE WHATSOEVER AND THEY DON'T SHOW ANY VALUES!
- BUG 19: NOT FIXED: NOW BAD REQUEST SHOWS IN 2 PLACES AND STILL AS 400 INSTEAD OF FRIENDLY MESSAGE EXPLAINING WHAT'S MISSING
- BUG 20: DID NOT FIXED IT. FILTERING by multiple tag keys, such as "team, system" DOES NOT PROVIDE ANY RESULTS
- BUG 21: NOT FIXED. ROLLOUT 100% IS TREATED AS A USER SETTING INSTEAD OF AS 'NOT SET' DEFAULT
- BUG 22: NOT FIXED. ON CLEAR, IT DEFAULTS TO 0% WHICH EFFECTIVELY BLOCKS ANY FLAG EVALUATION 

## NEW FEATURES

1. UI: Propel icon and proper page title

2. UI, API: Search by flag name or flag key

3. UI, API Add filtering by application name

4. UI, API: E2E test with Sql Server backend

# PROPEL MIGRATION CLI TO DO LIST

1. Add CLI commands (maybe)

2. Add CLI documentation

3. Add CLI tests
