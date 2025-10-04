
# DASHBOARD API
## BUGS

~~1.  **MAJOR, API:** filtering: filters only when 1 filter present; - try filtering targeting rules~~

~~2.  **MAJOR, API:** evaluation: throws invalid exception when user id or tenant id required (should be bad request, not 500).  Try evaluation white-label-branding without tenant id, or any other flags~~

~~3.  **MEDIUM, API:** evaluation message on success: (All [Propel.FeatureFlags.Domain.ModeSet] conditions met for feature flag activation) <- need to fix message~~

~~4. **MAJOR, API**: Filtering Expires in Days not working - it's an API BUG~~

~~5. **MAJOR, API** filtering by tag not working~~

~~6. **MAJOR, API** incorrect number of flags per page due to left join with audit table~~

~~7.  **MAJOR, API:** Targeting rules evalution not working and default variation is always defaults to Off instead of actual variation~~


# DASHBOARD UI 
## BUGS

~~8. **MAJOR, UI:** on user access control CLEAR - error: sets user access to 0% which is full blockage; expected: must be 100%; 100% always means no access is set (everyone is welcome)~~

~~9.  **MAJOR, UI:** same as above for tenant access CLEAR~~

~~10.  **MAJOR, UI:** information on each panel is a weird looking black column and hard to read.~~

~~11.  **MAJOR, UI:** Application name and application version not shown on flag~~

12.  **MEDIUM, UI:** Flag card is way off to edges when there's a long list of evaluation modes. For example, ultimate-premium-experience flag that has Scheduling+TargetingRules+Percentage+TimeWindow list of modes that don't fit to the size of card

~~14. **MEDUM, UI:** unable to add a tag in CREATE FLAG dialog. Column ':' or space ' ' or comma ',' are not allowed by UI.~~

~~15. **MINOR, UI:** change text in create flag dialog to something like this: 'Note: You only can create global flags from this site. All application flags must be created from the application invoking them, directly from the code base.~~

~~The global flag you're creating is set as disabled and permanent by default. You can change these settings and add additional settings after you create the flag.' Check spelling, grammar, decrease verbosity.~~

~~16. **MAJOR, UI:** expiration date filter does not work or not implemented in React~~

~~17. **MAJOR, UI:** expiration date is shown incorrectly: API returns 10/12/2025 00:00:00 but UI shows 10/11/1025 7:00:00 am~~

~~18. **MEDIUM, UI:**: variations and default variations are not shown for flag~~s

~~19. **MEDIUM, UI:** bad request message on evaluation when field is required (tenantid, userid) instead on showing error message~~
	
~~20. **MEDIUM, UI:** when filter by tag key applied, the UI sets Tags field of api request instead of TagKeys field~~

~~21. **MEDIUM, UI:** when user (tenant) percentage rollout is set to 100%, the UI should show percentage as "No user restrictions" ("No tenant restrictions") (because 100% means no restriction, everyone is allowed)~~

~~22.**MEDIUM, UI:** when Clear user (tenant) access control, the rollout must be set to 100% and shown as "No user restrictions"~~

~~23. **NEW BUG, MEDIUM, UI:** when scope is Global, no application name or version must be shown because it's pointless.~~

~~24. **MEDIUM, UI:** Duplicated page header with page title.~~

~~25. **MINOR, UI:** Close search panel (add x button)~~
	
~~26. **MEDIUM, UI:** Both tenant and user access control slider shows 50% instead of 100%. Is it because of the changes in style?~~

27. **MINOR, UI:** Tenant rules: adding rules after first syncronizes some fields and makes it impossible to enter unique values. There's a workaround: add one target rule, save, then add another one.

~~28 **MEDIUM, UI:** No error message on create flag 409 error~~

## BUG FIX VERIFICATION REPORT

- BUG #8: CLOSED
- BUG #9: CLOSED
- BUG 10: CLOSED
- BUG 11: CLOSED
- BUG 12: NOT FIXED: FLAG CARD IS STILL WAY OFF TO EDGES WHEN THERE'S A LONG LIST OF EVALUATION MODES. FOR EXAMPLE, ULTIMATE-PREMIUM-EXPERIENCE FLAG THAT HAS SCHEDULING+TARGETINGRULES+PERCENTAGE+TIMEWINDOW LIST OF MODES THAT DON'T FIT TO THE SIZE OF CARD
- BUG 14: CLOSED
- BUG 15: CLOSED
- BUG 16: CLOSED
- BUG 17: CLOSED. 
- BUG 18: CLOSED
- BUG 19: CLOSED
- BUG 20: CLOSED
- BUG 21: CLOSED
- BUG 22: CLOSED
- BUG 23: CLOSED
- BUG 24: CLOSED
- BUG 25: CLOSED
- BUG 26: CLOSED
- BUG 27: NOT FIXED
- BUG 28: CLOSED

## NEW FEATURES

1. ~~Propel icon and proper page title~~

- ~~Add propel icon and find a good page title to dashboard~~

2. ~~Search by flag name or flag key~~

- ~~Add search box to search flags by flag name or flag key~~

- ~~Add api support for search by flag name or flag key~~

3. ~~Add filtering by application name and scope (global or application)~~
- ~~Add filtering by application name and scope (global or application) to UI~~

- ~~Add api support for filtering by application name and scope (global or application)~~

4. E2E test with Sql Server backend
- Add e2e test with Sql Server backend to github actions
- Add sql server test db to github actions

5. ~~Add Variations and Default Variation to flag card~~
- ~~Add variations and default variation to flag card in UI~~

- ~~Add api support for variations and default variation~~

6. Bring back IsPermanent flag
- Add api support
- Add UI support



# PROPEL CLI

## NEW FEATURES
1. Add CLI commands (maybe)

2. Improve CLI migration: 
	- embed up/down scripts per db provider
	- add indexes

2. Add CLI documentation

3. Add CLI tests
