# Util Keys

**Live Mac system stats on a Logitech MX Creative Keypad, like iStat Menus on your desk.**

<img src="keypad.svg" width="400" alt="The keypad: CPU 23%, Memory 21 GB of 32, SSD 412 GB free, Network down 12.4 MB/s and up 820 KB/s, Battery 86%, CPU temp 58°, Chrome as the busiest app, Internet 14 ms on Wi-Fi at 907 Mb/s, and GPU 31%">

Nine keys, one number each, updated every second. Tapping a key does nothing yet.

## Keys

| Key | Shows | Colour |
|---|---|---|
| CPU | Load across all cores, and the last minute as a graph | Green, amber over 70%, red over 90% |
| Memory | GB used of total, as Activity Monitor counts it. The bar is memory pressure | The bar follows pressure: green, amber, red |
| SSD | Free space on the startup disk, as Finder shows it (purgeable space counts as free). The bar is space used | Blue, amber under 15% free, red under 5% |
| Network | Download and upload speeds, each with a graph of the last minute | Blue down, purple up |
| Busiest app | The app using the most CPU, with its CPU and memory. Helpers count toward their app, so Chrome Helper counts as Chrome | Amber over 100% CPU |
| Battery | Charge, and time left or time to full | Green, amber under 20%, red under 10% |
| CPU temp | CPU temperature and fan speed, with a graph | Green, amber from 80°, red from 95° |
| Internet | Ping time to 1.1.1.1, and Ethernet or Wi-Fi with its link speed. On Wi-Fi the bars are signal above noise, so a noisy room costs bars | Green under 60 ms, amber under 150 ms, red above or offline |
| GPU | GPU load, and the last minute as a graph | Purple, amber over 70%, red over 90% |

The keys only read the system while one of them is on the keypad.

## Install

You need a Mac, Logi Options+ (signed in to a Logi account) and an MX Creative Keypad.

### 1. Build the plugin

Clone this repo somewhere it can stay: the built plugin runs from this folder.

1. `brew install --cask dotnet-sdk`
2. `dotnet tool install --global LogiPluginTool`
3. `cd UtilKeysPlugin && dotnet build`

Or open this folder in Claude Code and say "install this for me". It reads `AGENTS.md` and does the rest.

### 2. Place the keys

1. Open Logi Options+ and select your keypad.
2. Add a page, or use one with free keys.
3. Open **All actions**, select **Util Keys** and drag the keys you want onto the keypad.

## Limits

- Mac only.
- CPU is the share of all cores, so 100% means every core is busy. The Busiest app key uses one core as 100%,
  like Activity Monitor, so a busy app can show over 100%.
- Busiest app is checked every 5 seconds, so its graph covers 5 minutes. Its memory adds up each process's
  resident memory, so it can be higher than Activity Monitor's figure.
- Network counts the physical interfaces (Wi-Fi and Ethernet), not VPN tunnels, so VPN traffic isn't counted twice.
- CPU temp averages the CPU die sensors through a private macOS API (the one Apple's own tools use), so a macOS
  update could break it. Macs without fans show "no fans".
- Internet pings Cloudflare's 1.1.1.1 every 5 seconds while the key shows.
- Battery shows "no battery" on desktop Macs. It has only been tried on a Mac mini so far.
