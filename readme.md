# LGS Tray Battery GHUB DUMP
Helper program to retrieve missing power model `.xml` files for use with [LGS Tray Battery](https://github.com/andyvorld/LGSTrayBattery).

A separate program to keep original LGS Tray Battery light weight.

Logitech G HUB is not needed after extraction of `.xml` files.

## Usage
1. Ensure Logitech G HUB is running in the background
2. Ensure all devices are detected by Logitech G HUB
3. Launch `LGSTrayBattery_GHUB_dump.exe`
4. Wait for the extraction to complete and press any key
5. Copy all `.xml` files to the `power model` subfolder of LGS Tray Battery

## Known issues
 - The extracted `.xml` file may have illegal characters within its comments, you may need to double check the validity of the file with another tool like vscode. (Refer to, https://github.com/andyvorld/LGSTrayBattery/issues/11)

## Tested Devices
 - G403
