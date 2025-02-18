![CS2-AutoAccept](https://socialify.git.ci/tsgsOFFICIAL/CS2-AutoAccept/image?description=1&font=Source%20Code%20Pro&forks=1&issues=1&language=1&name=1&owner=1&pattern=Transparent&pulls=1&stargazers=1&theme=Dark)

# CS2-AutoAccept  
**Automatically accept competitive matches in CS2 with zero game modifications.**  
*The #1 AutoAccept solution since May 16, 2021!*

---

## Features  
- 🖱️ Auto-detects and clicks the "ACCEPT" button using screen capture and OCR.  
- ✅ Compatible with CS2 & CS:GO.  
- 🔒 VAC-Undetected (no game files altered).  
- 🚀 Lightweight and easy to set up.  

---

## Live Demo  
![Demo](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/blob/main/VID_20230907215625.gif)  
*The program scans for the "ACCEPT" button and clicks it instantly when detected.*

---

## FAQ  

### **How does it work?**  
The program captures a small region of your screen where the "ACCEPT" button appears in CS2. It uses [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) to detect the button text. Once found, it moves your mouse to the button's location and clicks it automatically. Scans occur every second.

### **Is it safe to use?**  
Yes! This tool does **not** modify game files, interact with CS2's memory, or require internet access. It operates purely through screen analysis and mouse simulation, making it VAC-safe.

### **System Requirements**  
- Windows 10/11 (64-bit or 32-bit).  
- CS2

### **Can this be abused?**  
While it can accept matches automatically, it won’t prevent AFK penalties once in-game. Use responsibly.

### **How do I report an issue?**  
Open a [GitHub Issue](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/issues) or contact me on [Discord](https://discord.gg/Cddu5aJ). Include:  
- OS version  
- CS2 resolution/window mode  
- Steps to reproduce the bug  

---

## Installation  
1. **Download** the latest release [here](https://github.com/tsgsOFFICIAL/CS2-AutoAccept/releases/latest) (*preferred*) or via [direct link](https://download-directory.github.io/?url=https://github.com/tsgsOFFICIAL/CS2-AutoAccept/tree/main/CS2-AutoAccept/bin/Release/net6.0-windows10.0.17763.0/publish/win-x86).  
2. **Extract** the ZIP to `%appdata%\CS2 AutoAccept`:  
   - Press `Win + R`, type `%appdata%`, then press Enter.  
   - Create a folder named `CS2 AutoAccept` and extract the files here.  
3. **Create a shortcut**:  
   - Right-click `CS2-AutoAccept.exe` > `Send to` > `Desktop (create shortcut)`.  
4. Run the program **before queuing** for a match.  

---

## Limitations  
- Requires CS2 to be visible on your screen (minimized games won’t work).  
- Best results at 1280x720 resolution or higher.  

---

## Contributing  
Pull requests are welcome! See the [source code](https://github.com/tsgsOFFICIAL/CS2-AutoAccept) for details.  

---

**License**: [MIT](https://choosealicense.com/licenses/mit/)  
**Disclaimer**: Use at your own risk. Not affiliated with Valve or CS2.  
