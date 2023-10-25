# CS2-AutoAccept
A program to automatically find and press "ACCEPT" for you, when entering a competitive match in CS2.
![](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/blob/main/rgb_divider.gif)![](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/blob/main/rgb_divider.gif)

**Live demo**
<br>
![](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/blob/main/VID_20230907215625.gif)
<br>

## FAQ

**How does it work?**
<br>
It works by capturing a tiny region where the accept button will appear, on the screen that CS2 is running on every second, and using [OCR](https://aws.amazon.com/what-is/ocr/) to read the text of the buttons.<br>
If it finds the accept button, it takes over your mouse, points it at the button and clicks it, accepting your match for you.

**Does this work with Panorama UI?**
<br>
Not sure, but try it out and report errors :)

**Does it work with VAC?**
<br>
The program is undetected by VAC, as it makes no changes to the actual game.

**Is it safe?**
<br>
Yes, it's completely safe to use.

**Can this be abused?**
<br>
Sure, but only to join a match, when you're not there.
<br>
The program won't help you stay afk, once you are in a match.

**How do I report an issue?**
<br>
To report any issues or bugs, go to my Github page and create an issue for the project, or ping me on [Discord](https://discord.gg/Cddu5aJ).

## Where can I download this?

You should be able to download it [HERE](https://download-directory.github.io/?url=https://github.com/tsgsOFFICIAL/CS2-AutoAccept/tree/main/CS2-AutoAccept/bin/Release/net6.0-windows/publish/win-x86).
Simply extract it from the zip file, and you should be ready to go.
Extract it to "%appdata%\CS2 AutoAccept", to do this follow these steps:
1. Press Windows key + R key at the same time (opens RUN).
2. Type "%appdata%" and press ENTER key.
3. Create a new folder called "CS2 AutoAccept".
4. Extract the ZIP file inside this folder.
5. Create a shortcut to the application by dragging the "CS2-AutoAccept.exe" file somewhere else, like your desktop and holding CTRL and SHIFT while letting it go.
6. Enjoy :)
