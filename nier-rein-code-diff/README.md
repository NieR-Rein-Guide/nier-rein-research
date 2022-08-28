# nier-rein-code-diff
<b>This tool is meant for internal research and is not designed to be easily understandable.</b>

This tool creates a difference between a dump.cs by Il2CppDumper of the game and the DarkMasterMemoryDatabase class implemented in nier-rein-api.

It can update the offsets commented in DarkMasterMemoryDatabase.cs as well as add new table properties and their initialization in the method ``Init``.

Additionally it adds and updates EntityM and EntityM_Table classes, according with the updates for DarkMasterMemoryDatabase.
