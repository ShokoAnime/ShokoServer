#### A lot of these are standard programming tips, but APIv2 was a mess because they weren't followed
---
- Modules/Controllers must be split into smaller files based on what they provide for. Series is its own. Episode is its own. Etc.
- Use Data Validation on Models. `[Required]` for example

---
- Models that have shared functionality, such as Image, Title, etc. should go into Common
- Models that are specific types for Shoko, such as Series, Group, etc. should go into Shoko
- Models that are specific to only a certain type, such as Filter.Condition should be inner classes at the bottom of the class

---
- Shared helper classes such as API extensions or other API specific methods should go into Helpers
- Models and Controllers should never couple/be dependent on one another. If there is shared code, make a helper.

---
- Controller names should end with "Controller"
- Helper names should end with "Helper"
- Everything that isn't named with a suffix is assumed to be a Model

---
- Often things can be done with the endpoints that exist in a full-featured API.
If a group of specialized endpoints are useful for a Utility, for example, then make a separate Controller for it.
- Even though File Rename/Move might be logical to go in File, put it in utility/renamer, as we don't want to clutter File
- Such Controllers should have their own base route like `/apiv3/utility/multiplefiles/deletewithpreferences`
- Another good example is Calendars. It's faster to build a clean response in the server, rather than to make a client build it.

---
Feel free to add guidelines to this if you deem it helpful (I'm sure cazzar and avael can think of a few)