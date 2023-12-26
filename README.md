# Eliza Constraint Editor

Made for Eliza (and whomever she shares this with) by Silvainius

I heavily referenced code and design from Dreadrith's fork of s-m-k's Animation Heirarchy:
https://github.com/Dreadrith/Unity-Animation-Hierarchy-Editor

Sure, my stuff is completely different now, but I copied how they did some stuff so its mentioned.

If you're wondering what "helpbox", "in bigtitle", and other magic strings used in the Scopes are, well so was I! Here is what I found on them:
    - Maybe Checkout UnityEngine.GUISKin.BuildStyleCache(), it has a few built-in engine styles.
        - Engine "skins" also have their own styles (and presumably style overrides), so that is at best VERY incomplete.
    - https://stackoverflow.com/a/43730992 Just a list of random ones. No sources for them provided.
    - https://discussions.unity.com/t/what-are-the-editor-resources-by-name-for-editorguiutility-load-method/116914/2 Found them in decompiled code?

Some resources on custom editor tab icons (in case I forget):
    - https://forum.unity.com/threads/how-to-add-the-icon-in-editorwindow-tab.29075/#post-2442771